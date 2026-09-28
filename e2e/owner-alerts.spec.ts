import { expect, type Page, test } from "@playwright/test";

// /alerts end to end, against the compose app: sign in by the real button
// (a Development route issues the cookie), then every button on the page
// as the owner presses it, and the settings form. The calendar and
// Pushover behind it are the in-memory ones (Alerts__Fake in compose.yaml).
// The worker, the database and the rate limit are real. Serial: mute, skip
// and the settings are rows the next test reads.

test.describe.configure({ mode: "serial" });

async function signIn(page: Page) {
  await page.goto("/alerts");
  const settled =
    'a:has-text("Sign in with Google"), button:has-text("Send test alert")';
  await page.locator(settled).first().waitFor({ timeout: 15_000 });
  const button = page.getByRole("link", { name: "Sign in with Google" });
  if (await button.count()) {
    await button.click();
    await page.waitForURL("**/alerts");
    await page.locator(settled).first().waitFor({ timeout: 15_000 });
  }
  await expect(
    page.getByRole("button", { name: "Send test alert" }),
  ).toBeVisible();
}

/** The settings form at the configuration's defaults. */
const defaults = {
  priority: 2,
  repeatSeconds: 60,
  stopAfterMinutes: 180,
  sound: "",
  defaultLeadMinutes: 10,
  pollMinutes: 5,
  lookaheadHours: 48,
  includeAllDay: false,
  timeZone: "",
  ownerEmails: [],
};

/**
 * Whatever an earlier engine's run left: unmuted, nothing skipped, the
 * default settings. The
 * development calendar placed its events from the app's start, so after
 * 3 h of uptime the standup had begun and this spec failed. The reset
 * places them from now and has the worker read them at once.
 */
async function reset(page: Page) {
  const calendar = await page.request.post("/api/alerts/fake/reset");
  expect(calendar.status()).toBe(200);
  await page.request.post("/api/alerts/unmute");
  const settings = await page.request.put("/api/alerts/settings", {
    data: defaults,
  });
  expect(settings.status()).toBe(200);
  const status = await (await page.request.get("/api/alerts/status")).json();
  for (const alert of status.alerts ?? []) {
    if (alert.skipped)
      await page.request.post("/api/alerts/unskip", {
        data: { key: alert.key },
      });
  }
}

test.describe("/alerts", () => {
  test("the owner reads the next alerts, mutes, unmutes, skips and sends a test", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);

    const refresh = page.getByRole("button", { name: "Refresh" });
    const list = page.getByRole("list", { name: "Next alerts" });
    await refresh.click();
    await expect(list.getByText("E2E standup")).toBeVisible();

    await expect(page.getByLabel("Alert state: active")).toBeVisible();
    await expect(list.getByText("E2E review")).toBeVisible();
    await expect(list.getByText("Room 4")).toBeVisible();
    // All-day and cancelled events do not alert.
    await expect(page.getByText("E2E holiday")).toHaveCount(0);
    await expect(page.getByText("E2E cancelled")).toHaveCount(0);
    await expect(
      page.getByText(/Calendar read .*, every 5 minutes/),
    ).toBeVisible();

    await page.getByRole("button", { name: "Mute 1 hour" }).click();
    await expect(page.getByLabel("Alert state: muted")).toContainText(
      "Muted until",
    );
    await page.getByRole("button", { name: "Unmute" }).click();
    await expect(page.getByLabel("Alert state: active")).toBeVisible();

    await page
      .getByRole("button", { name: "Mute until tomorrow 06:00" })
      .click();
    await expect(page.getByLabel("Alert state: muted")).toContainText(
      "6:00 AM",
    );
    await page.getByRole("button", { name: "Unmute" }).click();
    await expect(page.getByLabel("Alert state: active")).toBeVisible();

    await page.getByRole("button", { name: /^Skip E2E standup at / }).click();
    const standup = list
      .getByRole("listitem")
      .filter({ hasText: "E2E standup" });
    await expect(standup.getByText("Skipped", { exact: true })).toBeVisible();
    await refresh.click();
    await expect(standup.getByText("Skipped", { exact: true })).toBeVisible();
    await page
      .getByRole("button", { name: /^Undo skip of E2E standup at / })
      .click();
    await expect(
      page.getByRole("button", { name: /^Skip E2E standup at / }),
    ).toBeVisible();

    await page.getByRole("button", { name: "Send test alert" }).click();
    await expect(page.getByText(/Test alert sent/)).toBeVisible();
    await refresh.click();
    await expect(
      page.getByText(/Last send: Test alert, .*, sent\./),
    ).toBeVisible();
  });

  test("the owner changes the repeat and the priority, and the test alert follows them", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    const save = page.getByRole("button", { name: "Save settings" });
    const repeat = page.getByLabel("Repeat every", { exact: true });
    const emergency = page.getByRole("button", { name: "Emergency" });
    const high = page.getByRole("button", { name: "High" });
    await expect(repeat).toHaveValue("60");
    await expect(emergency).toHaveAttribute("aria-pressed", "true");
    await expect(save).toBeDisabled();

    await page.getByRole("button", { name: "Repeat every 2 min" }).click();
    await expect(repeat).toHaveValue("120");
    await expect(
      page.getByText(
        "Stops after 100 min: 50 sounds × 120 s = 100 min, before the 180 min limit.",
      ),
    ).toBeVisible();
    await save.click();
    await expect(page.getByText("Settings saved.")).toBeVisible();

    await page.reload();
    await expect(repeat).toHaveValue("120");
    await expect(
      page.getByText(/again every 2 minutes until you acknowledge it/),
    ).toBeVisible();
    await page.getByRole("button", { name: "Send test alert" }).click();
    await expect(page.getByText(/Test alert sent/)).toBeVisible();
    const emergencySent = await (
      await page.request.get("/api/alerts/fake/sent")
    ).json();
    expect(emergencySent).toMatchObject({
      priority: "2",
      retry: "120",
      expire: "10800",
      sound: null,
    });

    await high.click();
    await expect(repeat).toBeDisabled();
    await save.click();
    await expect(page.getByText("Settings saved.")).toBeVisible();

    await page.reload();
    await expect(high).toHaveAttribute("aria-pressed", "true");
    await expect(emergency).toHaveAttribute("aria-pressed", "false");
    await expect(
      page.getByText(/one sound that plays through Pushover's quiet hours/),
    ).toBeVisible();
    await page.getByRole("button", { name: "Send test alert" }).click();
    await expect(page.getByText(/Test alert sent/)).toBeVisible();
    const highSent = await (
      await page.request.get("/api/alerts/fake/sent")
    ).json();
    expect(highSent).toMatchObject({
      priority: "1",
      retry: null,
      expire: null,
    });

    await reset(page);
  });

  test("a visitor is sent to sign in and every button is refused", async ({
    browser,
  }) => {
    const visitor = await browser.newContext();
    const page = await visitor.newPage();
    await page.goto("/alerts");
    await expect(
      page.getByRole("link", { name: "Sign in with Google" }),
    ).toBeVisible();
    await expect(page.getByRole("button", { name: "Mute 1 hour" })).toHaveCount(
      0,
    );

    for (const path of [
      "/api/alerts/test",
      "/api/alerts/mute",
      "/api/alerts/skip",
    ]) {
      const response = await page.request.post(path, { data: {} });
      expect(response.status(), path).toBe(401);
    }
    const settings = await page.request.put("/api/alerts/settings", {
      data: defaults,
    });
    expect(settings.status()).toBe(401);
    await visitor.close();
  });
});
