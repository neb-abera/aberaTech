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
    'a:has-text("Sign in with Google"), button:has-text("Test: ring until stopped")';
  await page.locator(settled).first().waitFor({ timeout: 15_000 });
  const button = page.getByRole("link", { name: "Sign in with Google" });
  if (await button.count()) {
    await button.click();
    await page.waitForURL("**/alerts");
    await page.locator(settled).first().waitFor({ timeout: 15_000 });
  }
  await expect(
    page.getByRole("button", { name: "Test: ring until stopped" }),
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
  phoneSound: "default",
  phoneSnoozeMinutes: 9,
};

/**
 * Whatever an earlier engine's run left: unmuted, nothing skipped, no type
 * set on any event, no phone paired, no routine alarm, the default settings. The
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
  for (const routine of (status.routines ?? []) as { id: string }[])
    await page.request.delete(`/api/alerts/routines/${routine.id}`);
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

/** Every listed alert with this title, soonest first. */
async function alertsTitled(
  page: Page,
  title: string,
): Promise<{ key: string; startsAt: string }[]> {
  const status = await (await page.request.get("/api/alerts/status")).json();
  return status.alerts.filter(
    (alert: { title: string }) => alert.title === title,
  );
}

/** An instant moved by some minutes, as a datetime-local input takes it in the browser's zone. */
async function localPlus(
  page: Page,
  iso: string,
  minutes: number,
): Promise<string> {
  return page.evaluate(
    ([at, plus]) => {
      const moved = new Date(
        Date.parse(at as string) + (plus as number) * 60_000,
      );
      const pad = (value: number) => String(value).padStart(2, "0");
      return `${moved.getFullYear()}-${pad(moved.getMonth() + 1)}-${pad(moved.getDate())}T${pad(moved.getHours())}:${pad(moved.getMinutes())}`;
    },
    [iso, minutes],
  );
}

/** What the fake Google Calendar took since the last reset. */
async function googleWrites(page: Page) {
  return (await page.request.get("/api/alerts/fake/google")).json();
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
      review.getByText("Ring until stopped: from #critical in the calendar."),
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

    await page
      .getByRole("button", { name: "Test: ring until stopped" })
      .click();
    await expect(page.getByText(/Ring until stopped test sent/)).toBeVisible();
    await refresh.click();
    await expect(
      page.getByText(/Last send: Test: ring until stopped, .*, sent\./),
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
        "Ring until stopped: rings every 120 s until you acknowledge it on the phone or here, and stops after 30 min.",
      ),
    ).toBeVisible();
    await page
      .getByRole("button", { name: "Test: ring until stopped" })
      .click();
    await expect(
      page.getByText(
        "Ring until stopped test sent. It rings every 120 s until you acknowledge it on the phone or here, and stops after 30 min.",
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

    await expect(
      page.getByRole("heading", { name: "Settings", exact: true }),
    ).toBeVisible();
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
    await page
      .getByRole("button", { name: "Test: ring until stopped" })
      .click();
    await expect(page.getByText(/Ring until stopped test sent/)).toBeVisible();
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
    await page
      .getByRole("button", { name: "Test: ring until stopped" })
      .click();
    await expect(page.getByText(/Ring until stopped test sent/)).toBeVisible();
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
        'Test of E2E review sent, titled "Test: E2E review", set to ring until stopped.',
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

    await page
      .getByRole("button", { name: "Test: ring until stopped" })
      .click();
    await expect(page.getByText(/Ring until stopped test sent/)).toBeVisible();
    const alarm = await (
      await page.request.get("/api/alerts/fake/sent")
    ).json();
    expect(alarm).toMatchObject({
      title: "Test: ring until stopped",
      priority: "2",
      retry: "60",
      expire: "10800",
    });

    await page.getByRole("button", { name: "Test: ring once" }).click();
    await expect(page.getByText(/Ring once test sent/)).toBeVisible();
    const plain = await (
      await page.request.get("/api/alerts/fake/sent")
    ).json();
    expect(plain).toMatchObject({
      title: "Test: ring once",
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
      name: /^Set E2E standup at .* to Ring once$/,
    });
    await expect(test).toBeDisabled();

    await notification.click();
    await expect(
      page.getByText("E2E standup is set to Ring once, every occurrence."),
    ).toBeVisible();
    await page.reload();
    await expect(notification).toHaveAttribute("aria-pressed", "true");
    await expect(
      standup.getByText("Ring once: set here. The Ring once settings apply."),
    ).toBeVisible();

    await test.click();
    await expect(
      page.getByText(
        'Test of E2E standup sent, titled "Test: E2E standup", set to ring once.',
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
      page.getByRole("button", { name: /^Set E2E standup at .* to Off$/ }),
    ).toHaveAttribute("aria-pressed", "true");
    await expect(test).toBeDisabled();
  });

  test("a new event made on the page is listed at once, and a type change is written to Google Calendar", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    const form = page.getByRole("form", { name: "New event" });
    await form.getByLabel(/^Title/).fill("E2E dentist");
    // Two hours from now, in the browser's zone, as the input takes it.
    const start = await page.evaluate(() => {
      const at = new Date(Date.now() + 2 * 60 * 60 * 1000);
      at.setSeconds(0, 0);
      const pad = (value: number) => String(value).padStart(2, "0");
      return `${at.getFullYear()}-${pad(at.getMonth() + 1)}-${pad(at.getDate())}T${pad(at.getHours())}:${pad(at.getMinutes())}`;
    });
    await form.getByLabel(/^Starts/).fill(start);
    await form.getByLabel(/^Duration in minutes/).fill("45");
    await form.getByLabel(/^Location/).fill("Main St");
    await form.getByLabel(/^Reminder in minutes before/).fill("20");
    await expect(
      form.getByRole("button", { name: "New event type Ring until stopped" }),
    ).toHaveAttribute("aria-pressed", "true");
    await form.getByRole("button", { name: "Add event" }).click();

    await expect(
      page.getByText("E2E dentist is on the calendar and listed below."),
    ).toBeVisible();
    const list = page.getByRole("list", { name: "Next alerts" });
    const dentist = list
      .getByRole("listitem")
      .filter({ hasText: "E2E dentist" });
    await expect(dentist).toBeVisible();
    await expect(dentist.getByText("Main St")).toBeVisible();
    await expect(
      dentist.getByText(
        "Ring until stopped: set here. The Ring until stopped settings apply.",
      ),
    ).toBeVisible();
    // Still listed after a reload: the server keeps it until the feed has it.
    await page.reload();
    await expect(dentist).toBeVisible();

    await page
      .getByRole("button", {
        name: /^Set E2E standup at .* to Ring until stopped$/,
      })
      .click();
    await expect(
      page.getByText(
        "E2E standup is set to Ring until stopped, every occurrence.",
      ),
    ).toBeVisible();
    await expect(page.getByText("Google Calendar was not changed")).toHaveCount(
      0,
    );

    const writes = await (
      await page.request.get("/api/alerts/fake/google")
    ).json();
    expect(writes).toMatchObject([
      {
        kind: "insert",
        eventId: expect.any(String),
        summary: "E2E dentist",
        description: "#critical",
        popupMinutes: 20,
      },
      {
        kind: "patch",
        eventId: "e2e-standup",
        summary: null,
        description: "Dial-in: room 4\n#critical",
        popupMinutes: null,
      },
    ]);
  });

  test("the owner edits one event, and the change survives a reload and reached Google Calendar", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();
    const [standup] = await alertsTitled(page, "E2E standup");

    await page.getByRole("button", { name: /^Edit E2E standup at / }).click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.getByLabel(/^Title/)).toHaveValue("E2E standup");
    await expect(dialog.getByLabel(/^Location/)).toHaveValue("Room 4");
    // Not a repeating event: no choice of which.
    await expect(dialog.getByText("All events")).toHaveCount(0);
    await dialog.getByLabel(/^Title/).fill("E2E standup moved");
    await dialog
      .getByLabel(/^Starts/)
      .fill(await localPlus(page, standup.startsAt, 30));
    await dialog.getByRole("button", { name: "Save" }).click();

    await expect(page.getByText("E2E standup moved is changed.")).toBeVisible();
    await expect(dialog).toHaveCount(0);
    await page.reload();
    const list = page.getByRole("list", { name: "Next alerts" });
    await expect(list.getByText("E2E standup moved")).toBeVisible();
    const [moved] = await alertsTitled(page, "E2E standup moved");
    expect(Date.parse(moved.startsAt) - Date.parse(standup.startsAt)).toBe(
      30 * 60_000,
    );
    expect(await alertsTitled(page, "E2E standup")).toEqual([]);

    const writes = await googleWrites(page);
    expect(writes).toMatchObject([
      {
        kind: "patch-master",
        eventId: "e2e-standup",
        summary: "E2E standup moved",
        description: null,
      },
    ]);
    expect(JSON.parse(writes[0].body)).not.toHaveProperty("description");
  });

  test("All events moves every occurrence of the daily event, and it survives a reload", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();
    const before = await alertsTitled(page, "E2E daily");
    expect(before).toHaveLength(2);

    await page
      .getByRole("button", { name: /^Edit E2E daily at / })
      .first()
      .click();
    const dialog = page.getByRole("dialog");
    await dialog.getByLabel("All events").check();
    await dialog
      .getByLabel(/^Starts/)
      .fill(await localPlus(page, before[0].startsAt, 60));
    await dialog.getByRole("button", { name: "Save" }).click();

    await expect(
      page.getByText("E2E daily is changed, every occurrence."),
    ).toBeVisible();
    await page.reload();
    await expect(
      page.getByRole("button", { name: /^Edit E2E daily at / }),
    ).toHaveCount(2);
    const after = await alertsTitled(page, "E2E daily");
    expect(after.map((alert) => Date.parse(alert.startsAt))).toEqual(
      before.map((alert) => Date.parse(alert.startsAt) + 60 * 60_000),
    );

    const writes = await googleWrites(page);
    expect(writes).toMatchObject([
      {
        kind: "patch-master",
        eventId: "e2e-daily",
        timeZone: "UTC",
        recurrence: null,
      },
    ]);
    expect(Date.parse(writes[0].start)).toBe(
      Date.parse(before[0].startsAt) + 60 * 60_000,
    );
  });

  test("All events moved a day later rewrites the series' weekdays in Google", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();
    const before = await alertsTitled(page, "E2E daily");
    expect(before).toHaveLength(2);

    await page
      .getByRole("button", { name: /^Edit E2E daily at / })
      .first()
      .click();
    const dialog = page.getByRole("dialog");
    await dialog.getByLabel("All events").check();
    await dialog
      .getByLabel(/^Starts/)
      .fill(await localPlus(page, before[0].startsAt, 24 * 60));
    await dialog.getByRole("button", { name: "Save" }).click();

    await expect(
      page.getByText("E2E daily is changed, every occurrence."),
    ).toBeVisible();

    // The development series is weekly on its first day and the next two,
    // in UTC. A day later it is the three days after.
    const days = ["SU", "MO", "TU", "WE", "TH", "FR", "SA"];
    const first = new Date(before[0].startsAt).getUTCDay();
    const moved = [1, 2, 3].map((day) => days[(first + day) % 7]).join(",");
    const writes = await googleWrites(page);
    expect(writes).toMatchObject([
      {
        kind: "patch-master",
        eventId: "e2e-daily",
        timeZone: "UTC",
        recurrence: [`RRULE:FREQ=WEEKLY;BYDAY=${moved};COUNT=3`],
      },
    ]);
    expect(Date.parse(writes[0].start)).toBe(
      Date.parse(before[0].startsAt) + 24 * 60 * 60_000,
    );
    expect(JSON.parse(writes[0].body)).not.toHaveProperty("description");
  });

  test("Delete of one occurrence keeps the rest of the series, and it survives a reload", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();
    const [first, second] = await alertsTitled(page, "E2E daily");

    await page
      .getByRole("button", { name: /^Delete E2E daily at / })
      .first()
      .click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.getByLabel("This event")).toBeChecked();
    await dialog.getByRole("button", { name: "Delete" }).click();

    await expect(page.getByText("E2E daily is deleted.")).toBeVisible();
    await page.reload();
    await expect(
      page.getByRole("button", { name: /^Delete E2E daily at / }),
    ).toHaveCount(1);
    expect(await alertsTitled(page, "E2E daily")).toMatchObject([
      { key: second.key },
    ]);

    const writes = await googleWrites(page);
    expect(writes).toMatchObject([
      { kind: "delete-instance", eventId: "e2e-daily" },
    ]);
    const utc = new Date(first.startsAt)
      .toISOString()
      .replace(/[-:]/g, "")
      .replace(/\.\d+Z$/, "Z");
    expect(writes[0].target).toMatch(new RegExp(`_${utc}$`));
  });

  test("Delete of a whole event takes it off the list, and it survives a reload", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    await page.getByRole("button", { name: /^Delete E2E review at / }).click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.getByText("Delete E2E review?")).toBeVisible();
    await dialog.getByRole("button", { name: "Delete" }).click();

    await expect(page.getByText("E2E review is deleted.")).toBeVisible();
    await page.reload();
    const list = page.getByRole("list", { name: "Next alerts" });
    await expect(list.getByText("E2E standup")).toBeVisible();
    await expect(list.getByText("E2E review")).toHaveCount(0);

    expect(await googleWrites(page)).toMatchObject([
      { kind: "delete-master", eventId: "e2e-review" },
    ]);
    // The next engine starts from the whole calendar again.
    await reset(page);
    expect(await alertsTitled(page, "E2E review")).toHaveLength(1);
  });

  test("the page explains how events become alarms, with the saved values", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    const how = page.getByRole("region", { name: "How events alert" });
    await expect(how).toContainText(
      "Every event with a start time in the next 48 hours is planned and listed above.",
    );
    await expect(how).toContainText(
      "an event rings until stopped when its title or description in Google Calendar has #critical",
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

  test("a fresh phone says Push off, and Push on once it registers a push token with its own token", async ({
    page,
    request,
  }) => {
    await signIn(page);
    await reset(page);
    const paired = await page.request.post("/api/alerts/devices", {
      data: { name: "E2E push phone" },
    });
    expect(paired.status()).toBe(201);
    const { token } = (await paired.json()) as { token: string };
    await page.reload();

    const row = page
      .getByRole("list", { name: "Paired phones" })
      .getByRole("listitem")
      .filter({ hasText: "E2E push phone" });
    await expect(row.getByText("Push off")).toBeVisible();
    await expect(
      page.getByText(
        "A push asks the phone to update its alarms at once. iOS can delay or drop it, most of all after the app is swiped away, so the phone also updates when opened and in the background.",
      ),
    ).toBeVisible();

    const apnsToken = "e2e0".repeat(16);
    const registration = { apnsToken, environment: "sandbox" };
    // The owner's cookie is not a phone.
    expect(
      (
        await page.request.put("/api/alerts/devices/me/push", {
          data: registration,
        })
      ).status(),
    ).toBe(403);
    const phone = { Authorization: `Bearer ${token}` };
    expect(
      (
        await request.put("/api/alerts/devices/me/push", {
          data: { apnsToken: "ABC", environment: "sandbox" },
          headers: phone,
        })
      ).status(),
    ).toBe(400);
    expect(
      (
        await request.put("/api/alerts/devices/me/push", {
          data: registration,
          headers: phone,
        })
      ).status(),
    ).toBe(204);

    await page.reload();
    await expect(row.getByText("Push on")).toBeVisible();
    const listed = await (await page.request.get("/api/alerts/devices")).text();
    expect(listed).not.toContain(apnsToken);

    // A mute from the phone pushes it, through the fake Apple.
    const before = (
      await (await page.request.get("/api/alerts/fake/pushes")).json()
    ).length;
    expect(
      (
        await request.post("/api/alerts/mute", {
          data: { until: "hour" },
          headers: phone,
        })
      ).status(),
    ).toBe(200);
    await expect
      .poll(
        async () =>
          (await (await page.request.get("/api/alerts/fake/pushes")).json())
            .length,
      )
      .toBeGreaterThan(before);
    await page.request.post("/api/alerts/unmute");
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
    await expect(page).toHaveTitle("Ringing: E2E drill");
    await ringing.getByRole("button", { name: "Acknowledge" }).click();

    await expect(ringing).toHaveCount(0);
    await expect(page).not.toHaveTitle(/Ringing:/);
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

  test("a routine alarm rings in this browser with its label and goes to Pushover, and Acknowledge stops both", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    const cancelledBefore = (
      await (await page.request.get("/api/alerts/fake/cancelled")).json()
    ).length;
    // A routine due this minute: the worker sends it to the fake Pushover
    // at priority 2, titled with its label, with a receipt.
    const due = await page.request.post("/api/alerts/fake/routine-due");
    expect(due.status()).toBe(200);
    const ring = (
      (await due.json()).routineRings as {
        key: string;
        label: string;
        alertAt: string;
        startsAt: string;
      }[]
    ).find((item) => item.label === "E2E routine");
    expect(ring?.key).toMatch(
      /^routine:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}:\d{4}-\d\d-\d\dT\d\d:\d\d$/,
    );
    const sent = await (await page.request.get("/api/alerts/fake/sent")).json();
    expect(sent).toMatchObject({
      title: "E2E routine",
      priority: "2",
      callback: expect.stringMatching(/\/api\/alerts\/pushover\/acknowledged$/),
    });

    await page.reload();
    const ringing = page.getByRole("alertdialog", {
      name: "Ringing: E2E routine",
    });
    await expect(ringing).toHaveCount(0);
    await page.getByRole("switch", { name: "Ring in this browser" }).click();

    await expect(ringing).toBeVisible();
    await expect(ringing).toContainText("Routine alarm,");
    await expect(page).toHaveTitle("Ringing: E2E routine");
    await ringing.getByRole("button", { name: "Acknowledge" }).click();

    await expect(ringing).toHaveCount(0);
    await expect(page).not.toHaveTitle(/Ringing:/);
    const status = await (await page.request.get("/api/alerts/status")).json();
    expect(
      status.routineRings.find(
        (item: { key: string }) => item.key === ring?.key,
      ),
    ).toMatchObject({ acknowledged: true, acknowledgedVia: "browser" });
    // Pushover's repeats for it were cancelled.
    const cancelled = await (
      await page.request.get("/api/alerts/fake/cancelled")
    ).json();
    expect(cancelled.length).toBe(cancelledBefore + 1);

    await page.reload();
    await expect(ringing).toHaveCount(0);
    await reset(page);
  });

  test("an alarm acknowledged in the Pushover app is acknowledged on the page, and a callback Pushover does not confirm is refused", async ({
    page,
    request,
  }) => {
    await signIn(page);
    await reset(page);
    expect((await page.request.post("/api/alerts/fake/due")).status()).toBe(
      200,
    );
    const messages = (await (
      await page.request.get("/api/alerts/fake/sent-all")
    ).json()) as { title: string; receipt: string | null; callback: string }[];
    const drill = messages
      .filter((message) => message.title === "E2E drill")
      .at(-1);
    expect(drill?.receipt).toMatch(/^development[0-9a-f]{19}$/);
    expect(drill?.callback).toMatch(/\/api\/alerts\/pushover\/acknowledged$/);
    const receipt = drill?.receipt ?? "";
    const form = {
      receipt,
      acknowledged: "1",
      acknowledged_at: String(Math.floor(Date.now() / 1000)),
      acknowledged_by: "user",
      acknowledged_by_device: "iphone",
    };

    // Pushover's servers post with no session. Before the owner pressed
    // Acknowledge in the app, Pushover's receipt says it is not acknowledged.
    const forged = await request.post("/api/alerts/pushover/acknowledged", {
      form,
    });
    expect(forged.status()).toBe(403);

    expect(
      (
        await page.request.post("/api/alerts/fake/pushover-acknowledge", {
          data: { key: receipt },
        })
      ).status(),
    ).toBe(204);
    const callback = await request.post("/api/alerts/pushover/acknowledged", {
      form,
    });
    expect(callback.status()).toBe(200);
    expect(await callback.json()).toEqual({ acknowledged: true });

    await page.reload();
    const item = page
      .getByRole("list", { name: "Next alerts" })
      .getByRole("listitem")
      .filter({ hasText: "E2E drill" });
    await expect(
      item.getByText(/^Acknowledged in Pushover at \d\d:\d\d$/),
    ).toBeVisible();
    const status = await (await page.request.get("/api/alerts/status")).json();
    expect(
      status.alerts.find(
        (alert: { title: string }) => alert.title === "E2E drill",
      ),
    ).toMatchObject({ acknowledged: true, acknowledgedVia: "pushover" });

    // Ringing in this browser does not ring for it.
    await page.getByRole("switch", { name: "Ring in this browser" }).click();
    await expect(
      page.getByRole("alertdialog", { name: "Ringing: E2E drill" }),
    ).toHaveCount(0);

    // Pushover posting again changes nothing.
    const again = await request.post("/api/alerts/pushover/acknowledged", {
      form,
    });
    expect(again.status()).toBe(200);
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

  test("the phone's alarm sound and snooze save with the settings and come back after a reload", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    await expect(
      page.getByRole("heading", { name: "On the phone" }),
    ).toBeVisible();
    const sound = page.getByLabel("Alarm sound", { exact: true });
    const snooze = page.getByLabel("Snooze", { exact: true });
    await expect(sound).toHaveValue("default");
    await expect(snooze).toHaveValue("9");
    await sound.selectOption({ label: "Beacon" });
    await snooze.fill("15");
    await page.getByRole("button", { name: "Save settings" }).click();
    await expect(page.getByText("Settings saved.")).toBeVisible();

    await page.reload();
    await expect(sound).toHaveValue("beacon");
    await expect(sound.locator("option:checked")).toHaveText("Beacon");
    await expect(snooze).toHaveValue("15");
    const status = await (await page.request.get("/api/alerts/status")).json();
    expect(status.settings.phoneSound).toBe("beacon");
    expect(status.settings.phoneSnoozeMinutes).toBe(15);
    expect(status.bounds.phoneSnoozeMinutes).toEqual({ min: 1, max: 30 });
    await reset(page);
  });

  test("the owner adds a routine alarm, switches it off, edits it and deletes it, and each survives a reload", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    const section = page.getByRole("region", { name: "Routine alarms" });
    await expect(section.getByText("No routine alarms.")).toBeVisible();
    await expect(
      section.getByText(
        "Routine alarms ring on the paired phone like Clock alarms. They also ring through Pushover, and here when Ring in this browser is on. Acknowledging one anywhere stops it everywhere.",
      ),
    ).toBeVisible();

    // Add: 06:30 on Monday, Wednesday and Friday, labelled E2E wake.
    const add = page.getByRole("form", { name: "Add routine alarm" });
    await add.getByLabel("Time").fill("06:30");
    for (const day of ["Monday", "Wednesday", "Friday"])
      await add.getByRole("button", { name: `Repeat on ${day}` }).click();
    await add.getByLabel("Label").fill("E2E wake");
    await add.getByLabel("Snooze in minutes").fill("5");
    await add.getByRole("button", { name: "Add routine alarm" }).click();

    const list = page.getByRole("list", { name: "Routine alarms" });
    const item = list.getByRole("listitem").filter({ hasText: "E2E wake" });
    await expect(item.getByText("06:30")).toBeVisible();
    await expect(item.getByText("Mon Wed Fri, E2E wake")).toBeVisible();
    await page.reload();
    await expect(item.getByText("Mon Wed Fri, E2E wake")).toBeVisible();
    await expect(item.getByText(/Snooze 5 min/)).toBeVisible();

    // Stored as the phone reads it: wall-clock time, ISO weekdays.
    let status = await (await page.request.get("/api/alerts/status")).json();
    expect(status.routines).toHaveLength(1);
    expect(status.routines[0]).toMatchObject({
      label: "E2E wake",
      hour: 6,
      minute: 30,
      days: [1, 3, 5],
      enabled: true,
      snoozeMinutes: 5,
    });

    // Switch off.
    const toggle = item.getByRole("switch", { name: "E2E wake at 06:30" });
    await expect(toggle).toBeChecked();
    await toggle.click();
    await expect(page.getByText("E2E wake at 06:30 is off.")).toBeVisible();
    await page.reload();
    await expect(
      item.getByRole("switch", { name: "E2E wake at 06:30" }),
    ).not.toBeChecked();

    // Edit: 07:15 every day.
    await item.getByRole("button", { name: "Edit E2E wake at 06:30" }).click();
    const edit = page.getByRole("form", { name: "Edit routine alarm" });
    await expect(edit.getByLabel("Time")).toHaveValue("06:30");
    await edit.getByLabel("Time").fill("07:15");
    for (const day of ["Tuesday", "Thursday", "Saturday", "Sunday"])
      await edit.getByRole("button", { name: `Repeat on ${day}` }).click();
    await edit.getByRole("button", { name: "Save routine alarm" }).click();
    await expect(item.getByText("Every day, E2E wake")).toBeVisible();
    await page.reload();
    await expect(item.getByText("07:15")).toBeVisible();
    await expect(item.getByText("Every day, E2E wake")).toBeVisible();
    // The edit kept it off.
    await expect(
      item.getByRole("switch", { name: "E2E wake at 07:15" }),
    ).not.toBeChecked();

    // Delete asks first.
    await item
      .getByRole("button", { name: "Delete E2E wake at 07:15" })
      .click();
    await page
      .getByRole("button", { name: "Yes, delete E2E wake at 07:15" })
      .click();
    await expect(section.getByText("No routine alarms.")).toBeVisible();
    await page.reload();
    await expect(section.getByText("No routine alarms.")).toBeVisible();
    status = await (await page.request.get("/api/alerts/status")).json();
    expect(status.routines).toEqual([]);
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
    const routine = "/api/alerts/routines/0b9c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10";
    expect(
      (
        await page.request.post("/api/alerts/routines", {
          data: { hour: 6, minute: 30, days: [] },
        })
      ).status(),
    ).toBe(401);
    expect(
      (
        await page.request.put(routine, {
          data: {
            hour: 6,
            minute: 30,
            days: [],
            enabled: true,
            snoozeMinutes: 9,
          },
        })
      ).status(),
    ).toBe(401);
    expect((await page.request.delete(routine)).status()).toBe(401);
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
    // A routine alarm shows its Edit and Delete buttons.
    await page.request.post("/api/alerts/routines", {
      data: { label: "E2E contrast", hour: 6, minute: 30, days: [1] },
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
