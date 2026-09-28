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

  test("Send test on a listed alert sends that event's text, titled as a test, with the saved settings", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    await page.getByRole("button", { name: "Nonstop" }).click();
    await expect(page.getByLabel("Repeat every", { exact: true })).toHaveValue(
      "30",
    );
    await expect(page.getByLabel("Sound")).toHaveValue("persistent");
    await expect(
      page.getByText(/Pushover repeats no faster than every 30 s/),
    ).toBeVisible();
    await page.getByRole("button", { name: "Save settings" }).click();
    await expect(page.getByText("Settings saved.")).toBeVisible();

    // Skipped and muted: the test goes anyway, and both stay.
    await page.getByRole("button", { name: /^Skip E2E standup at / }).click();
    await page.getByRole("button", { name: "Mute 1 hour" }).click();
    await expect(page.getByLabel("Alert state: muted")).toBeVisible();
    await page
      .getByRole("button", { name: /^Send test of E2E standup at / })
      .click();
    await expect(
      page.getByText(
        'Test of E2E standup sent, titled "Test: E2E standup", with the saved settings.',
      ),
    ).toBeVisible();
    const sent = await (await page.request.get("/api/alerts/fake/sent")).json();
    expect(sent).toMatchObject({
      title: "Test: E2E standup",
      priority: "2",
      retry: "30",
      sound: "persistent",
    });
    expect(sent.message).toMatch(/^Starts .*\nRoom 4$/);
    await expect(page.getByLabel("Alert state: muted")).toBeVisible();
    await expect(
      page.getByRole("button", { name: /^Undo skip of E2E standup at / }),
    ).toBeVisible();

    await reset(page);
  });

  test("the page explains how events become alarms, with the saved values", async ({
    page,
  }) => {
    await signIn(page);
    await reset(page);
    await page.reload();

    const how = page.getByRole("region", { name: "How events become alarms" });
    await expect(how).toContainText(
      "Every event with a start time in the next 48 hours alerts.",
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
        // which turns Save on and the repeat presets off.
        for (const state of ["loaded", "edited"] as const) {
          if (state === "edited")
            await page.getByRole("button", { name: "High" }).click();
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
