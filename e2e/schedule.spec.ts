import { expect, type Page, test } from "@playwright/test";

// The one public write path, as a visitor meets it: pick a day, pick a
// time, give a name, opt in to texts, book. Under `make e2e` no SMS
// provider is configured (compose.yaml sets no Twilio keys), so the
// confirmation goes to the app's LoggingMessageSender and nothing leaves
// the box. queue.owner.spec.ts reads those messages back from the owner's
// page.
//
// Every visitor project books here at once, on one database. Each takes a
// day of its own, by its place in this list, so no booking moves the times
// another project is choosing from.
const lanes = [
  "chromium",
  "firefox",
  "webkit",
  "phone-chromium",
  "phone-webkit",
];

// "9:15 AM UTC": the time, in the visitor's own zone, named.
const time = /^\d{1,2}:\d{2}\s?(AM|PM)\b/i;

test.beforeEach(async ({ request }) => {
  const state = await (await request.get("/api/scheduling/state")).json();
  expect(state.mode, "an open queue replaces the slots").toBe("slots");
});

/** A full day: the second one offered, since the first may be today. */
async function openDay(page: Page) {
  await page.goto("/schedule");
  const days = page
    .locator("button:not([disabled])")
    .filter({ hasText: /^\d{1,2}$/ });
  await expect(days.first()).toBeVisible({ timeout: 15_000 });
  await days.nth((await days.count()) > 1 ? 1 : 0).click();
  const slots = page.getByRole("button", { name: time });
  await expect(slots.first()).toBeVisible();
  return slots;
}

test("texts are opt-in, and the number is asked for only once they are", async ({
  page,
  request,
}) => {
  const slots = await openDay(page);
  await slots.first().click();

  const dialog = page.getByRole("dialog");
  await expect(dialog).toBeVisible();
  const consent = dialog.getByRole("checkbox", {
    name: "Yes, text me about this appointment",
  });
  // A ticked box is not consent. The carriers' review looks for this first.
  await expect(consent).not.toBeChecked();
  await expect(dialog.getByLabel("Mobile number")).toHaveCount(0);

  await consent.check();
  await expect(dialog.getByLabel("Mobile number")).toBeVisible();
  await consent.uncheck();
  await expect(dialog.getByLabel("Mobile number")).toHaveCount(0);

  // The disclosures the consent sits beside, and the pages they link to.
  for (const [name, path] of [
    ["Text message terms", "/sms-terms"],
    ["Privacy policy", "/sms-privacy"],
  ]) {
    await expect(dialog.getByRole("link", { name })).toHaveAttribute(
      "href",
      path,
    );
    expect((await request.get(path)).status(), path).toBe(200);
  }

  await dialog.getByRole("button", { name: "Cancel" }).click();
  await expect(dialog).toBeHidden();
});

test("a visitor books a time with texts and the time is taken", async ({
  page,
  request,
}, info) => {
  const lane = Math.max(0, lanes.indexOf(info.project.name));
  await page.goto("/schedule");
  const zone = await page.evaluate(
    () => Intl.DateTimeFormat().resolvedOptions().timeZone,
  );
  const { availableDates } = await (
    await request.get(`/api/scheduling/state?zone=${encodeURIComponent(zone)}`)
  ).json();
  expect(availableDates.length, "days on offer").toBeGreaterThan(lanes.length);
  // Day one may be today and half gone. Each lane takes a later one.
  const day: string = availableDates[1 + lane];
  const date = new Date(`${day}T12:00:00Z`);
  const month = date.toLocaleString("en-US", {
    month: "long",
    year: "numeric",
    timeZone: "UTC",
  });

  const dayLabel = date.toLocaleDateString("en-US", {
    weekday: "long",
    month: "long",
    day: "numeric",
    timeZone: "UTC",
  });

  // The calendar first, so the month is read once it is drawn.
  await expect(page.getByRole("button", { name: "Next month" })).toBeVisible({
    timeout: 15_000,
  });
  const heading = page.getByRole("heading", { name: month, exact: true });
  for (let step = 0; step < 3 && !(await heading.isVisible()); step++) {
    await page.getByRole("button", { name: "Next month" }).click();
  }
  await expect(heading).toBeVisible();
  await page
    .getByRole("button", { name: String(date.getUTCDate()), exact: true })
    .click();

  // The times shown are the chosen day's only once its heading is.
  await expect(
    page.getByRole("heading", { name: dayLabel, exact: true }),
  ).toBeVisible();
  const first = page.getByRole("button", { name: time }).first();
  await expect(first).toBeVisible();
  const label = (await first.textContent())?.trim() ?? "";
  const slot = page.getByRole("button", { name: label, exact: true });
  await slot.click();

  const dialog = page.getByRole("dialog");
  await expect(dialog.getByRole("heading")).toHaveText(
    `${dayLabel} at ${label}`,
  );
  await dialog.getByLabel("Your name").fill(`E2E ${info.project.name}`);
  await dialog
    .getByRole("checkbox", { name: "Yes, text me about this appointment" })
    .check();
  await dialog.getByLabel("Mobile number").fill(`(202) 555-01${40 + lane}`);
  await dialog.getByRole("button", { name: "Book it" }).click();

  await expect(dialog).toBeHidden();
  const booked = page.getByRole("alert").filter({ hasText: "Booked for" });
  await expect(booked).toContainText(label);
  await expect(booked).toContainText("A confirmation is on its way");
  // The page asks again after a booking, and the time is gone from the day.
  await expect(slot).toHaveCount(0);
});
