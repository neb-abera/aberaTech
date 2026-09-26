import { expect, test } from "@playwright/test";

// /fitness as its owner uses it: the tabs live in the address, a Garmin
// export goes in through the Data tab and shows up as an activity, and a
// wrong file can be taken out again. Under `make e2e` the owner is signed in
// without Google (compose.yaml, Fitness__DevelopmentOwner). The activity
// uploaded here is removed before the test ends.

test("the open tab is in the address and survives a reload", async ({
  page,
}) => {
  await page.goto("/fitness");
  const tabs = page.getByRole("tablist", { name: "Fitness sections" });
  await expect(tabs).toBeVisible({ timeout: 15_000 });

  await tabs.getByRole("tab", { name: "Readiness" }).click();
  await expect(page).toHaveURL(/[?&]tab=readiness/);
  await page.reload();
  await expect(tabs.getByRole("tab", { name: "Readiness" })).toHaveAttribute(
    "aria-selected",
    "true",
  );

  await page.goBack();
  await expect(tabs.getByRole("tab", { name: "Dashboard" })).toHaveAttribute(
    "aria-selected",
    "true",
  );
});

test("a Garmin export becomes an activity, and a wrong one can be removed", async ({
  page,
}, info) => {
  // Three days back at a time nobody trains, so it cannot collide with a
  // real row, and a name that says where it came from.
  const day = new Date(Date.now() - 3 * 86_400_000).toISOString().slice(0, 10);
  const name = `E2E run ${info.project.name}`;
  const csv = [
    "Activity Type,Date,Title,Distance,Time,Avg HR,Max HR",
    `Running,${day} 03:17:00,${name},5.00,00:25:00,150,170`,
  ].join("\n");

  await page.goto("/fitness?tab=data");
  // Through the button, as the owner does. setInputFiles on the hidden
  // input passed while the button itself sent nothing in Chromium.
  const chooser = page.waitForEvent("filechooser");
  await page.getByRole("button", { name: "Choose files" }).click();
  await (await chooser).setFiles({
    name: "e2e-activities.csv",
    mimeType: "text/csv",
    buffer: Buffer.from(csv),
  });

  await expect(
    page.getByRole("alert").filter({ hasText: "e2e-activities.csv" }),
  ).toContainText(/1 activities, 1 new/);

  const remove = page.getByRole("button", { name: `Remove ${name} of ${day}` });
  await expect(remove).toBeVisible();
  const row = page.getByRole("row").filter({ hasText: name });
  await expect(row).toContainText("run");
  await expect(row).toContainText("5.00 km");
  await expect(row).toContainText("150");

  page.once("dialog", (dialog) => dialog.accept());
  await remove.click();
  await expect(remove).toHaveCount(0);
});
