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

  await page.getByRole("button", { name: "New link", exact: true }).click();
  await card
    .getByLabel("Name, seen only by you")
    .fill(`E2E interviews ${info.project.name}`);
  await card.getByLabel("First day").fill(firstDay);
  await card.getByRole("button", { name: "Make the link" }).click();

  // The new link is listed with its address to copy, and keeps it on a reload.
  const row = page
    .getByRole("group", {
      name: `Rules for E2E interviews ${info.project.name}`,
    })
    .last();
  const address = row.getByRole("textbox", { name: "Link" });
  await expect(address).toHaveValue(/\/schedule#k=[A-Za-z0-9_-]{22}$/);
  const url = await address.inputValue();
  await expect(row.getByText(/^Issued /)).toBeVisible();
  await expect(row.getByRole("button", { name: "Copy" })).toBeVisible();
  await page.reload();
  await expect(address).toHaveValue(url);

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
  const on = row.getByRole("switch", { name: "On" });
  await on.uncheck();
  await expect(on).not.toBeChecked();
  await expect(row.getByRole("status")).toHaveText("Saved");

  // Revoke: the address is gone for good and the row says so.
  await row.getByRole("button", { name: "Revoke" }).click();
  await expect(row.getByText(/^Revoked /)).toBeVisible();
  await expect(row.getByRole("textbox", { name: "Link" })).toHaveCount(0);
  await expect(row.getByRole("switch", { name: "On" })).toBeDisabled();
  await expect(
    row.getByRole("button", { name: "Issue a new link" }),
  ).toBeVisible();
  const key = url.split("#k=")[1];
  const revoked = await page.request.get("/api/scheduling/state?zone=UTC", {
    headers: { "X-Booking-Link": key },
  });
  expect((await revoked.json()).availableDates[0]).toBe(plainFirst);
});
