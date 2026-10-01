import { expect, test } from "@playwright/test";

// /dates as a visitor, in every engine: the calculator answers from the
// browser alone, and the countdowns ask the visitor to sign in without
// the page ever asking for them.

test("a visitor counts the days between two dates and never asks for the countdowns", async ({
  page,
}) => {
  const asked: string[] = [];
  page.on("request", (request) => {
    if (new URL(request.url()).pathname.startsWith("/api/alerts"))
      asked.push(request.url());
  });
  await page.goto("/dates");
  await expect(
    page.getByRole("heading", { level: 1, name: "Dates and countdowns" }),
  ).toBeVisible();

  const section = page.getByRole("region", { name: "Days between two dates" });
  // The page is prerendered. Today arrives once the script runs, and a
  // date typed before then would be replaced by it.
  await expect(section.getByLabel("Start date", { exact: true })).not.toHaveValue("");
  await section.getByLabel("Start date", { exact: true }).fill("2026-10-01");
  await section.getByLabel("End date", { exact: true }).fill("2026-11-15");
  const result = section.getByRole("status", { name: "Days between" });
  await expect(result).toContainText("45 days");
  await expect(result).toContainText("0 years, 1 month, 14 days");
  await expect(result).toContainText("6 weeks, 3 days");
  await expect(result).toContainText("32 weekdays");
  await expect(result).toContainText("1,080 hours");

  await section.getByLabel("Include the end date").check();
  await expect(result).toContainText("46 days");

  await section.getByLabel("End date", { exact: true }).fill("2026-08-01");
  await expect(result).toContainText("62 days before the start");

  await expect(
    page.getByRole("link", { name: "Sign in with Google" }),
  ).toBeVisible();
  expect(asked).toEqual([]);
});

test("a visitor adds and subtracts a span, held to the month's last day", async ({
  page,
}) => {
  await page.goto("/dates");
  const section = page.getByRole("region", {
    name: "Add to or subtract from a date",
  });
  await expect(section.getByLabel("Date", { exact: true })).not.toHaveValue("");
  await section.getByLabel("Date", { exact: true }).fill("2026-01-31");
  await section.getByLabel("Months").fill("1");
  const result = section.getByRole("status", { name: "Resulting date" });
  await expect(result).toContainText("Saturday 28 February 2026");

  await section.getByLabel("Date", { exact: true }).fill("2026-10-01");
  await section.getByLabel("Months").fill("");
  await section.getByLabel("Weeks").fill("6");
  await section.getByLabel("Days").fill("3");
  await expect(result).toContainText("Sunday 15 November 2026");

  await section.getByRole("button", { name: "Subtract" }).click();
  await section.getByLabel("Weeks").fill("");
  await section.getByLabel("Days").fill("365");
  await expect(result).toContainText("Wednesday 1 October 2025");
});
