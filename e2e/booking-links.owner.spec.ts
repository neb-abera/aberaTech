import { expect, test } from "@playwright/test";
import { openAsOwner } from "./owner-session";

// Booking links from both sides. The owner makes a link on /schedule/admin
// with a first day a few days out and copies it. A visitor who opens it is
// offered nothing before that day, and a visitor on plain /schedule is
// offered earlier days. The link is turned off at the end, so a rerun on
// the same database starts from links that are all off.

test.describe.configure({ mode: "serial" });

test("a link offers times from its first day, and plain /schedule keeps its own rules", async ({
  page,
  browser,
}, info) => {
  await openAsOwner(page, "/schedule/admin", "text=Signed in as");
  const card = page.getByRole("group", { name: "Rules for new link" });

  const state = await (
    await page.request.get("/api/scheduling/state?zone=UTC")
  ).json();
  expect(state.mode, "an open queue replaces the slots").toBe("slots");
  const plainFirst: string = state.availableDates[0];
  // Three days after the first day plain /schedule offers.
  const first = new Date(`${plainFirst}T12:00:00Z`);
  first.setUTCDate(first.getUTCDate() + 3);
  const firstDay = first.toISOString().slice(0, 10);

  await page.getByRole("button", { name: "New link" }).click();
  await card
    .getByLabel("Name, seen only by you")
    .fill(`E2E interviews ${info.project.name}`);
  await card.getByLabel("First day").fill(firstDay);
  await card.getByRole("button", { name: "Make the link" }).click();

  const shown = page.getByRole("alert").filter({ hasText: "Copy it now" });
  await expect(shown).toBeVisible();
  const url = await shown.getByLabel("Link").inputValue();
  expect(url).toMatch(/\/schedule#k=[A-Za-z0-9_-]{22}$/);
  await shown.getByRole("button", { name: "Done" }).click();

  // The list never shows the key again.
  await expect(page.getByText(url)).toHaveCount(0);

  const context = await browser.newContext({ timezoneId: "UTC" });
  const visitor = await context.newPage();
  const asked = visitor.waitForResponse(
    (response) =>
      response.url().includes("/api/scheduling/state") &&
      response.request().headers()["x-booking-link"] !== undefined,
  );
  await visitor.goto(url);
  const linked = await (await asked).json();
  const offered: string[] = linked.availableDates;
  // Nothing before the first day. The first day itself may fall outside the
  // hours the development data opens, so the first offered is on or after it.
  expect(offered.length).toBeGreaterThan(0);
  for (const day of offered) expect(day >= firstDay, day).toBe(true);

  // The page opens on the first day the link offers.
  const opened = new Date(`${offered[0]}T12:00:00Z`);
  const label = opened.toLocaleDateString("en-US", {
    weekday: "long",
    month: "long",
    day: "numeric",
    timeZone: "UTC",
  });
  await expect(
    visitor.getByRole("heading", { name: label, exact: true }),
  ).toBeVisible({
    timeout: 15_000,
  });

  // Plain /schedule in a fresh tab still offers the earlier day.
  const plain = await (await context.newPage()).request.get(
    "/api/scheduling/state?zone=UTC",
  );
  expect((await plain.json()).availableDates[0]).toBe(plainFirst);
  expect(plainFirst < firstDay).toBe(true);
  await context.close();

  // Off: the link books under the default rules, like an unknown key.
  const row = page
    .getByRole("group", {
      name: `Rules for E2E interviews ${info.project.name}`,
    })
    .last();
  const on = row.getByRole("switch", { name: "On" });
  await on.uncheck();
  await expect(on).not.toBeChecked();
  await expect(row.getByRole("status")).toHaveText("Saved");
});
