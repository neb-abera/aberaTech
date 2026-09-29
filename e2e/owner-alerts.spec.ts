import {
  type APIRequestContext,
  expect,
  type Page,
  test,
} from "@playwright/test";

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
  repeatSeconds: 60,
  stopAfterMinutes: 180,
  sound: "",
  defaultLeadMinutes: 10,
  pollMinutes: 5,
  lookaheadHours: 48,
  includeAllDay: false,
  timeZone: "",
  ownerEmails: [],
  notificationPriority: 0,
  notificationSound: "",
  defaultType: "none",
  backupDelaySeconds: 0,
};

/**
 * Whatever an earlier engine's run left: unmuted, nothing skipped, no type
 * set on any event, no phone paired, the default settings. The
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
  const devices = await (await page.request.get("/api/alerts/devices")).json();
  for (const device of devices as { id: string }[])
    await page.request.delete(`/api/alerts/devices/${device.id}`);
  const status = await (await page.request.get("/api/alerts/status")).json();
  for (const alert of status.alerts ?? []) {
    if (alert.skipped)
      await page.request.post("/api/alerts/unskip", {
        data: { key: alert.key },
      });
    if (alert.typeFrom === "set")
      await page.request.put("/api/alerts/event-type", {
        data: { key: alert.key, type: "default" },
      });
  }
}

/** The key of one listed alert, by title. */
async function keyOf(page: Page, title: string): Promise<string> {
  const status = await (await page.request.get("/api/alerts/status")).json();
  return status.alerts.find((alert: { title: string }) => alert.title === title)
    .key;
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
    // The development calendar marks the review #critical: it is flagged,
    // and the mark itself is left off the title.
    const review = list.getByRole("listitem").filter({ hasText: "E2E review" });
    await expect(review.getByText("Critical", { exact: true })).toBeVisible();
    await expect(
      review.getByText("Alarm: from #critical in the calendar."),
    ).toBeVisible();
    await expect(list.getByText("E2E review #critical")).toHaveCount(0);
    // The standup is unmarked: it sends nothing and is still listed.
    const standupItem = list
      .getByRole("listitem")
      .filter({ hasText: "E2E standup" });
    await expect(
      standupItem.getByText("Critical", { exact: true }),
    ).toHaveCount(0);
    await expect(
      standupItem.getByText(/^Sends nothing: the default for unmarked events/),
    ).toBeVisible();
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

  test("an alarm always repeats: the owner changes the repeat and stop, and the test alert follows them", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    const save = page.getByRole("button", { name: "Save settings" });
    const repeat = page.getByLabel("Pushover repeats every", { exact: true });
    const stop = page.getByLabel("Pushover stops after", { exact: true });
    await expect(repeat).toHaveValue("60");
    await expect(repeat).toBeEnabled();
    await expect(stop).toBeEnabled();
    await expect(page.getByRole("button", { name: "Emergency" })).toHaveCount(
      0,
    );
    await expect(
      page.getByText(
        "Pushover does not repeat faster than every 30 s. A paired phone rings as an iPhone alarm until you press Stop. Ring in this browser beeps every second until you acknowledge it.",
      ),
    ).toBeVisible();
    await expect(save).toBeDisabled();

    await page.getByRole("button", { name: "Repeat every 2 min" }).click();
    await expect(repeat).toHaveValue("120");
    await stop.fill("30");
    await expect(
      page.getByText(
        "Stops after 30 min: the limit comes before 50 sounds × 120 s = 100 min.",
      ),
    ).toBeVisible();
    await save.click();
    await expect(page.getByText("Settings saved.")).toBeVisible();

    await page.reload();
    await expect(repeat).toHaveValue("120");
    await expect(
      page.getByText(
        "Alarm: rings every 120 s until you acknowledge it on the phone or here, and stops after 30 min.",
      ),
    ).toBeVisible();
    await page.getByRole("button", { name: "Send test alert" }).click();
    await expect(
      page.getByText(
        "Test alert sent as an alarm. It rings every 120 s until you acknowledge it on the phone or here, and stops after 30 min.",
      ),
    ).toBeVisible();
    const sent = await (await page.request.get("/api/alerts/fake/sent")).json();
    expect(sent).toMatchObject({
      priority: "2",
      retry: "120",
      expire: "1800",
      sound: null,
    });
    expect(sent.message).toContain(
      "It rings every 2 minutes until you acknowledge it.",
    );

    await reset(page);
  });

  test("the page says Emergency nowhere, and a priority in a saved body is ignored", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    await expect(page.getByRole("heading", { name: "Settings" })).toBeVisible();
    expect(await page.locator("body").innerText()).not.toMatch(/emergency/i);

    const saved = await page.request.put("/api/alerts/settings", {
      data: {
        ...defaults,
        priority: 0,
        repeatSeconds: 45,
        stopAfterMinutes: 20,
      },
    });
    expect(saved.status()).toBe(200);
    expect((await saved.json()).settings).not.toHaveProperty("priority");
    await page.reload();
    expect(await page.locator("body").innerText()).not.toMatch(/emergency/i);
    await page.getByRole("button", { name: "Send test alert" }).click();
    await expect(page.getByText(/Test alert sent as an alarm/)).toBeVisible();
    const sent = await (await page.request.get("/api/alerts/fake/sent")).json();
    expect(sent).toMatchObject({ priority: "2", retry: "45", expire: "1200" });

    await reset(page);
  });

  test("a sound uploaded to the Pushover account is listed first, saves and is sent", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    const sound = page.getByLabel("Sound", { exact: true });
    const yours = sound.locator('optgroup[label="Your sounds"] option');
    await expect(yours).toHaveCount(1);
    await expect(yours).toHaveText("Abera alarm (29.5 s) (aberaalarm)");
    await sound.selectOption("aberaalarm");
    await page.getByRole("button", { name: "Save settings" }).click();
    await expect(page.getByText("Settings saved.")).toBeVisible();

    await page.reload();
    await expect(sound).toHaveValue("aberaalarm");
    await page.getByRole("button", { name: "Send test alert" }).click();
    await expect(page.getByText(/Test alert sent as an alarm/)).toBeVisible();
    const sent = await (await page.request.get("/api/alerts/fake/sent")).json();
    expect(sent).toMatchObject({ priority: "2", sound: "aberaalarm" });

    await reset(page);
  });

  test("Send test on a listed alarm sends that event's text, titled as a test, with the alarm settings", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    await page.getByRole("button", { name: "Nonstop" }).click();
    await expect(
      page.getByLabel("Pushover repeats every", { exact: true }),
    ).toHaveValue("30");
    await expect(page.getByLabel("Sound", { exact: true })).toHaveValue(
      "persistent",
    );
    await expect(
      page.getByText(/Pushover does not repeat faster than every 30 s\./),
    ).toBeVisible();
    await page.getByRole("button", { name: "Save settings" }).click();
    await expect(page.getByText("Settings saved.")).toBeVisible();

    // Skipped and muted: the test goes anyway, and both stay.
    await page.getByRole("button", { name: /^Skip E2E review at / }).click();
    await page.getByRole("button", { name: "Mute 1 hour" }).click();
    await expect(page.getByLabel("Alert state: muted")).toBeVisible();
    await page
      .getByRole("button", { name: /^Send test of E2E review at / })
      .click();
    await expect(
      page.getByText(
        'Test of E2E review sent, titled "Test: E2E review", as an alarm.',
      ),
    ).toBeVisible();
    const sent = await (await page.request.get("/api/alerts/fake/sent")).json();
    expect(sent).toMatchObject({
      title: "Test: E2E review",
      priority: "2",
      retry: "30",
      sound: "persistent",
    });
    expect(sent.message).toMatch(/^Starts /);
    await expect(page.getByLabel("Alert state: muted")).toBeVisible();
    await expect(
      page.getByRole("button", { name: /^Undo skip of E2E review at / }),
    ).toBeVisible();

    await reset(page);
  });

  test("Send test alert sends an alarm and Send test notification sends one plain notification", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    await page.getByRole("button", { name: "Send test alert" }).click();
    await expect(page.getByText(/Test alert sent as an alarm/)).toBeVisible();
    const alarm = await (
      await page.request.get("/api/alerts/fake/sent")
    ).json();
    expect(alarm).toMatchObject({
      title: "Test alert",
      priority: "2",
      retry: "60",
      expire: "10800",
    });

    await page.getByRole("button", { name: "Send test notification" }).click();
    await expect(page.getByText(/Test notification sent/)).toBeVisible();
    const plain = await (
      await page.request.get("/api/alerts/fake/sent")
    ).json();
    expect(plain).toMatchObject({
      title: "Test notification",
      priority: "0",
      retry: null,
      expire: null,
    });
  });

  test("the owner sets an event's type, it survives a reload, and its test goes as that type", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    const list = page.getByRole("list", { name: "Next alerts" });
    const standup = list
      .getByRole("listitem")
      .filter({ hasText: "E2E standup" });
    const test = page.getByRole("button", {
      name: /^Send test of E2E standup at /,
    });
    const notification = page.getByRole("button", {
      name: /^Set E2E standup at .* to Notification$/,
    });
    await expect(test).toBeDisabled();

    await notification.click();
    await expect(
      page.getByText("E2E standup is set to Notification, every occurrence."),
    ).toBeVisible();
    await page.reload();
    await expect(notification).toHaveAttribute("aria-pressed", "true");
    await expect(
      standup.getByText(
        "Notification: set here. The notification settings apply.",
      ),
    ).toBeVisible();

    await test.click();
    await expect(
      page.getByText(
        'Test of E2E standup sent, titled "Test: E2E standup", as a notification.',
      ),
    ).toBeVisible();
    const sent = await (await page.request.get("/api/alerts/fake/sent")).json();
    expect(sent).toMatchObject({
      title: "Test: E2E standup",
      priority: "0",
      retry: null,
      expire: null,
    });

    await page
      .getByRole("button", {
        name: /^Use the default type for E2E standup at /,
      })
      .click();
    await expect(
      page.getByText("E2E standup follows the calendar and the default again."),
    ).toBeVisible();
    await page.reload();
    await expect(
      page.getByRole("button", { name: /^Set E2E standup at .* to None$/ }),
    ).toHaveAttribute("aria-pressed", "true");
    await expect(test).toBeDisabled();
  });

  test("the page explains how events become alarms, with the saved values", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    const how = page.getByRole("region", { name: "How events become alarms" });
    await expect(how).toContainText(
      "Every event with a start time in the next 48 hours is planned and listed above.",
    );
    await expect(how).toContainText(
      "an event becomes an alarm when its title or description in Google Calendar has #critical",
    );
    await expect(how).toContainText(
      "Every other event sends nothing, the default for unmarked events under Settings.",
    );
    await expect(how).toContainText("Settings for my calendars");
    await expect(
      page.getByText(
        "An event with no notification of its own alerts 10 minutes before it starts.",
      ),
    ).toBeVisible();
    await expect(
      page.getByText(/The server reads the calendar every 5 minutes\./),
    ).toBeVisible();
  });

  test("a calendar that cannot be read is the first thing on the page", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    const failed = await page.request.post("/api/alerts/fake/fail");
    expect(failed.status()).toBe(200);
    await page.reload();

    const banner = page
      .getByRole("alert")
      .filter({ hasText: "The calendar cannot be read" });
    await expect(banner).toBeVisible();
    await expect(banner).toContainText("failed: HTTP 404.");
    await expect(banner).toContainText("The alerts below are from that read.");
    const bannerBox = await banner.boundingBox();
    const muteBox = await page
      .getByRole("button", { name: "Mute 1 hour" })
      .boundingBox();
    expect(bannerBox?.y ?? 1e9).toBeLessThan(muteBox?.y ?? 0);
    await expect(
      page.getByRole("list", { name: "Next alerts" }).getByText("E2E standup"),
    ).toBeVisible();

    await reset(page);
    await page.reload();
    await expect(page.getByText("The calendar cannot be read")).toHaveCount(0);
  });

  test("pairing a phone shows a QR code, a link and the token once, and Revoke stops the token", async ({
    page,
    request,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    const pair = page.getByRole("button", { name: "Pair a phone" });
    await expect(pair).toBeDisabled();
    await page.getByLabel("Phone name").fill("E2E phone");
    await pair.click();

    const pairing = page.getByRole("region", { name: "Pairing E2E phone" });
    await expect(
      pairing.getByRole("img", { name: "QR code that pairs a phone" }),
    ).toBeVisible();
    const box = await pairing
      .getByRole("img", { name: "QR code that pairs a phone" })
      .boundingBox();
    expect(box?.width).toBe(224);
    const href = await pairing
      .getByRole("link", { name: "Open on this phone" })
      .getAttribute("href");
    // Not abera.tech, so the link names this server for the phone.
    expect(href).toMatch(
      /^aberaalarms:\/\/pair#token=aat_[A-Za-z0-9_-]{43}&server=http%3A%2F%2F/,
    );
    const token = (href ?? "").slice(
      "aberaalarms://pair#token=".length,
      "aberaalarms://pair#token=".length + 47,
    );
    await expect(pairing.getByLabel("Token")).toHaveText(token);
    await expect(pairing.getByText(/cannot be shown again/)).toBeVisible();

    const phones = page.getByRole("list", { name: "Paired phones" });
    await expect(phones.getByText("E2E phone")).toBeVisible();
    await expect(phones.getByText(/Not seen yet/)).toBeVisible();
    const listed = await (await page.request.get("/api/alerts/devices")).text();
    expect(listed).not.toContain(token);

    // The phone's own request: the token and no cookie.
    const phone = (path: string, request: APIRequestContext) =>
      request.get(path, { headers: { Authorization: `Bearer ${token}` } });
    expect((await phone("/api/alerts/status", request)).status()).toBe(200);
    expect((await phone("/api/alerts/devices", request)).status()).toBe(403);
    await page.reload();
    await expect(phones.getByText(/Last seen /)).toBeVisible();

    await page.getByRole("button", { name: "Revoke E2E phone" }).click();
    await page.getByRole("button", { name: "Yes, revoke E2E phone" }).click();
    await expect(page.getByText("No phones paired.")).toBeVisible();
    expect((await phone("/api/alerts/status", request)).status()).toBe(401);
  });

  test("ringing in this browser rings for a due alarm, and Acknowledge stops it and marks it acknowledged", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    const cancelledBefore = (
      await (await page.request.get("/api/alerts/fake/cancelled")).json()
    ).length;
    // An alarm due now: the worker sends it to the fake Pushover at
    // priority 2, with a receipt.
    const due = await page.request.post("/api/alerts/fake/due");
    expect(due.status()).toBe(200);
    await page.reload();
    await expect(
      page.getByText(/Rings only while this tab is open/),
    ).toBeVisible();
    const ringing = page.getByRole("alertdialog", {
      name: "Ringing: E2E drill",
    });
    await expect(ringing).toHaveCount(0);

    await page.getByRole("switch", { name: "Ring in this browser" }).click();

    await expect(ringing).toBeVisible();
    await expect(page).toHaveTitle("Alarm: E2E drill");
    await ringing.getByRole("button", { name: "Acknowledge" }).click();

    await expect(ringing).toHaveCount(0);
    await expect(page).not.toHaveTitle(/Alarm:/);
    const drill = page
      .getByRole("list", { name: "Next alerts" })
      .getByRole("listitem")
      .filter({ hasText: "E2E drill" });
    await expect(
      drill.getByText(/^Acknowledged in a browser at \d\d:\d\d$/),
    ).toBeVisible();
    const status = await (await page.request.get("/api/alerts/status")).json();
    const acknowledged = status.alerts.find(
      (alert: { title: string }) => alert.title === "E2E drill",
    );
    expect(acknowledged).toMatchObject({
      acknowledged: true,
      acknowledgedVia: "browser",
    });
    // Pushover's repeats for it were cancelled.
    const cancelled = await (
      await page.request.get("/api/alerts/fake/cancelled")
    ).json();
    expect(cancelled.length).toBe(cancelledBefore + 1);

    // Still on, and nothing rings: the one due alarm is answered.
    await page.reload();
    await expect(
      page.getByRole("switch", { name: "Ring in this browser" }),
    ).toBeChecked();
    await expect(ringing).toHaveCount(0);
    await reset(page);
  });

  test("the backup delay saves with the settings and comes back after a reload", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    const delay = page.getByLabel("Pushover backup after");
    await expect(delay).toHaveValue("0");
    await delay.fill("120");
    await expect(
      page.getByText(
        /A paired phone rings first\. Pushover follows 120 seconds later/,
      ),
    ).toBeVisible();
    await page.getByRole("button", { name: "Save settings" }).click();
    await expect(page.getByText("Settings saved.")).toBeVisible();

    await page.reload();
    await expect(delay).toHaveValue("120");
    const status = await (await page.request.get("/api/alerts/status")).json();
    expect(status.settings.backupDelaySeconds).toBe(120);
    expect(status.bounds.backupDelaySeconds).toEqual({ min: 0, max: 900 });
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
      "/api/alerts/test-event",
      "/api/alerts/test-notification",
      "/api/alerts/mute",
      "/api/alerts/skip",
      "/api/alerts/ack",
      "/api/alerts/devices",
    ]) {
      const response = await page.request.post(path, { data: {} });
      expect(response.status(), path).toBe(401);
    }
    const settings = await page.request.put("/api/alerts/settings", {
      data: defaults,
    });
    expect(settings.status()).toBe(401);
    const type = await page.request.put("/api/alerts/event-type", {
      data: { key: "e2e-standup", type: "alarm" },
    });
    expect(type.status()).toBe(401);
    await visitor.close();
  });
});

/** One run of text inside a button, and its contrast with what is behind it. */
interface Measured {
  button: string;
  text: string;
  disabled: boolean;
  color: string;
  background: string;
  ratio: number;
}

/**
 * The contrast of the text in every button on the page, as painted: the
 * text colour composited over each ancestor's background colour, with each
 * ancestor's opacity applied as a group, as the browser paints it. A
 * disabled button counts. It still has to be read to know what it is for.
 */
function buttonContrast(page: Page): Promise<Measured[]> {
  return page.evaluate(() => {
    type Rgba = [number, number, number, number];
    const canvas = document.createElement("canvas");
    canvas.width = 1;
    canvas.height = 1;
    const context = canvas.getContext("2d", { willReadFrequently: true });
    if (!context) throw new Error("no 2d canvas");
    // The browser parses the colour, whatever syntax the style uses.
    const parse = (css: string): Rgba => {
      context.clearRect(0, 0, 1, 1);
      context.fillStyle = "#000";
      context.fillStyle = css;
      context.fillRect(0, 0, 1, 1);
      const [r, g, b, a] = context.getImageData(0, 0, 1, 1).data;
      return [r, g, b, a / 255];
    };
    const over = (top: Rgba, under: Rgba): Rgba => {
      const a = top[3];
      return [
        top[0] * a + under[0] * (1 - a),
        top[1] * a + under[1] * (1 - a),
        top[2] * a + under[2] * (1 - a),
        1,
      ];
    };
    const mix = (a: Rgba, b: Rgba, t: number): Rgba => [
      a[0] * (1 - t) + b[0] * t,
      a[1] * (1 - t) + b[1] * t,
      a[2] * (1 - t) + b[2] * t,
      1,
    ];
    const luminance = (c: Rgba) => {
      const [r, g, b] = c.slice(0, 3).map((v) => {
        const s = v / 255;
        return s <= 0.04045 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
      });
      return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    };
    const ratio = (a: Rgba, b: Rgba) => {
      const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
      return (hi + 0.05) / (lo + 0.05);
    };
    const show = (c: Rgba) =>
      `rgb(${c.slice(0, 3).map(Math.round).join(", ")})`;

    // The canvas colour is what shows through a page with no background.
    const canvasColour = parse(
      getComputedStyle(document.documentElement).backgroundColor,
    );
    const ground: Rgba =
      canvasColour[3] === 0 ? [255, 255, 255, 1] : canvasColour;

    /** Each colour stop of a gradient background, or none. */
    const stops = (image: string): Rgba[] =>
      image === "none"
        ? []
        : (image.match(/(rgba?|color)\([^()]*\)|transparent/g) ?? []).map(
            parse,
          );

    /**
     * The pixel under the text and the pixel of the text, painted root
     * first, and the lowest contrast of the two. Where an ancestor has a
     * gradient, every stop is tried in turn: the disabled Save button once
     * kept its light gradient under faint white text.
     */
    const paint = (
      element: Element,
    ): { back: Rgba; fore: Rgba; ratio: number } => {
      const chain: Element[] = [];
      for (let node: Element | null = element; node; node = node.parentElement)
        chain.unshift(node);
      const styles = chain.map((node) => getComputedStyle(node));
      const text = parse(styles[styles.length - 1].color);
      const choices = styles.map((style) => [
        null,
        ...stops(style.backgroundImage),
      ]);
      const render = (
        index: number,
        under: Rgba,
        withText: boolean,
        picks: (Rgba | null)[],
      ): Rgba => {
        if (index === chain.length) return withText ? over(text, under) : under;
        const style = styles[index];
        let layer = over(parse(style.backgroundColor), under);
        const pick = picks[index];
        if (pick) layer = over(pick, layer);
        const inside = render(index + 1, layer, withText, picks);
        return mix(under, inside, Number(style.opacity));
      };
      // Every combination of one stop (or none) per gradient ancestor.
      let combos: (Rgba | null)[][] = [[]];
      for (const options of choices) {
        const next: (Rgba | null)[][] = [];
        for (const combo of combos)
          for (const option of options.length > 1 ? options.slice(1) : options)
            next.push([...combo, option]);
        combos = next.slice(0, 256);
      }
      let worst = {
        back: ground,
        fore: ground,
        ratio: Number.POSITIVE_INFINITY,
      };
      for (const picks of combos) {
        const back = render(0, ground, false, picks);
        const fore = render(0, ground, true, picks);
        const r = ratio(fore, back);
        if (r < worst.ratio) worst = { back, fore, ratio: r };
      }
      return worst;
    };

    const found: Measured[] = [];
    for (const button of Array.from(
      document.querySelectorAll<HTMLElement>('button, [role="button"]'),
    )) {
      const box = button.getBoundingClientRect();
      if (box.width === 0 || box.height === 0) continue;
      const name = button.getAttribute("aria-label") ?? button.innerText.trim();
      const walker = document.createTreeWalker(button, NodeFilter.SHOW_TEXT);
      const seen = new Set<Element>();
      for (let node = walker.nextNode(); node; node = walker.nextNode()) {
        const holder = node.parentElement;
        if (!holder || seen.has(holder) || !node.textContent?.trim()) continue;
        seen.add(holder);
        const { back, fore, ratio: measured } = paint(holder);
        found.push({
          button: name,
          text: node.textContent.trim(),
          disabled:
            button.matches(":disabled") ||
            button.getAttribute("aria-disabled") === "true" ||
            button.classList.contains("Mui-disabled"),
          color: show(fore),
          background: show(back),
          ratio: Math.round(measured * 100) / 100,
        });
      }
    }
    return found;
  });
}

test.describe("/alerts button contrast", () => {
  test("every button's text reaches 4.5:1 in both schemes, at desktop and phone widths", async ({
    page,
    browserName,
  }) => {
    await signIn(page);
    await reset(page);
    // An item skipped shows Undo skip, the one text button in the list.
    const status = await (await page.request.get("/api/alerts/status")).json();
    const standup = status.alerts.find(
      (alert: { title: string }) => alert.title === "E2E standup",
    );
    await page.request.post("/api/alerts/skip", {
      data: { key: standup.key },
    });
    // A type set here shows Use default, a text button of its own.
    await page.request.put("/api/alerts/event-type", {
      data: { key: await keyOf(page, "E2E review"), type: "notification" },
    });

    const failures: string[] = [];
    const rows: string[] = [];
    for (const scheme of ["dark", "light"] as const) {
      for (const width of [1280, 390]) {
        await page.setViewportSize({ width, height: 900 });
        await page.addInitScript((mode) => {
          localStorage.setItem("mui-mode", mode);
        }, scheme);
        await page.reload();
        await expect(page.locator("html")).toHaveAttribute(
          "data-mui-color-scheme",
          scheme,
        );
        await expect(
          page.getByRole("button", { name: /^Undo skip of E2E standup/ }),
        ).toBeVisible();

        // Two states: as loaded, with Save off, and with an edit waiting,
        // which turns Save on and presses another repeat preset.
        for (const state of ["loaded", "edited"] as const) {
          if (state === "edited")
            await page
              .getByRole("button", { name: "Repeat every 2 min" })
              .click();
          await page.mouse.move(0, 0);
          await page.evaluate(() =>
            Promise.all(
              document
                .getAnimations()
                .map((a) => a.finished.catch(() => undefined)),
            ),
          );
          for (const m of await buttonContrast(page)) {
            const row = `${browserName} ${scheme} ${width}px ${state}: "${m.button}" "${m.text}"${m.disabled ? " (disabled)" : ""} ${m.color} on ${m.background} = ${m.ratio}:1`;
            rows.push(row);
            if (m.ratio < 4.5) failures.push(row);
          }
        }
      }
    }
    console.log(rows.join("\n"));
    await reset(page);
    expect(failures).toEqual([]);
  });
});
