import { expect, test } from "@playwright/test";
import { openAsOwner } from "./owner-session";

// The queue from both sides: the owner opens it on /schedule/admin, a
// visitor joins it from /schedule with texts on, the owner works the line
// and closes it, and /schedule goes back to offering times. The texts go to
// the app's LoggingMessageSender, which compose selects by configuring no
// SMS provider; the owner's Messages card shows each one it took.

test.describe.configure({ mode: "serial" });

const phone = "(202) 555-0147";
const e164 = "+12025550147";

test("the owner opens a queue, a visitor joins with texts, the owner works the line and closes it", async ({
  page,
  browser,
}, info) => {
  const session = `E2E queue ${info.project.name}`;
  await openAsOwner(page, "/schedule/admin", "text=Signed in as");

  // A run that failed halfway leaves its queue open. Close it first.
  await page.request.post("/api/scheduling/admin/session/close");
  await page.reload();

  try {
    await page.getByLabel("What is this session?").fill(session);
    await page.getByRole("button", { name: "Open the queue" }).click();
    await expect(page.getByText(session)).toBeVisible();
    await expect(page.getByText("0 waiting")).toBeVisible();

    const visitorContext = await browser.newContext();
    const visitor = await visitorContext.newPage();
    await visitor.goto("/schedule");
    await expect(visitor.getByText(session)).toBeVisible({ timeout: 15_000 });
    await expect(
      visitor.getByText("Nobody is waiting. You would be first."),
    ).toBeVisible();
    // A name the owner sees; the next visitor never does.
    await visitor.getByLabel("Your name").fill("E2E Visitor");
    const consent = visitor.getByRole("checkbox", {
      name: "Yes, text me about this appointment",
    });
    await expect(consent).not.toBeChecked();
    await consent.check();
    await visitor.getByLabel("Mobile number").fill(phone);
    await visitor.getByRole("button", { name: "Join the queue" }).click();
    await expect(visitor.getByText("Position 1")).toBeVisible();
    await expect(visitor.getByText("You are next.")).toBeVisible();

    // The owner's page shows the name and the number, and the welcome text
    // the fake sender took.
    await page.reload();
    const entry = page
      .locator("div")
      .filter({ has: page.getByText("E2E Visitor", { exact: true }) })
      .filter({ has: page.getByRole("button", { name: "Start" }) })
      .last();
    await expect(entry).toContainText(e164);
    await expect(page.getByText(`Queue welcome · ${e164}`)).toBeVisible({
      timeout: 15_000,
    });

    await entry.getByRole("button", { name: "Start" }).click();
    await expect(page.getByText(/with E2E Visitor now/)).toBeVisible();
    await page.getByRole("button", { name: "Done" }).click();
    await expect(page.getByText("0 waiting.")).toBeVisible();

    await page.getByRole("button", { name: "Close the queue" }).click();
    await expect(page.getByText("No queue is open")).toBeVisible();

    // The same link offers times again, and the visitor's place is gone.
    await visitor.reload();
    await expect(visitor.getByText(session)).toHaveCount(0);
    await expect(
      visitor
        .getByRole("button", { name: /^\d{1,2}:\d{2}\s?(AM|PM)\b/i })
        .first(),
    ).toBeVisible({ timeout: 15_000 });
    await visitorContext.close();
  } finally {
    await page.request.post("/api/scheduling/admin/session/close");
  }
});

test("a visitor who leaves the queue is off the owner's list", async ({
  page,
  browser,
}, info) => {
  const session = `E2E leave ${info.project.name}`;
  await openAsOwner(page, "/schedule/admin", "text=Signed in as");
  await page.request.post("/api/scheduling/admin/session/close");
  await page.reload();

  try {
    await page.getByLabel("What is this session?").fill(session);
    await page.getByRole("button", { name: "Open the queue" }).click();
    await expect(page.getByText(session)).toBeVisible();

    const visitorContext = await browser.newContext();
    const visitor = await visitorContext.newPage();
    await visitor.goto("/schedule");
    // Without texts: no number is asked for, and the page says why to stay.
    await visitor.getByLabel("Your name").fill("E2E Leaver");
    await expect(visitor.getByLabel("Mobile number")).toHaveCount(0);
    await expect(
      visitor.getByText(/keep this page open to see your place move/),
    ).toBeVisible();
    await visitor.getByRole("button", { name: "Join the queue" }).click();
    await expect(visitor.getByText("Position 1")).toBeVisible();

    await page.reload();
    await expect(page.getByText("1 waiting")).toBeVisible();

    await visitor.getByRole("button", { name: "Leave the queue" }).click();
    await expect(
      visitor.getByRole("button", { name: "Join the queue" }),
    ).toBeVisible();

    await page.reload();
    await expect(page.getByText("0 waiting")).toBeVisible();
    await visitorContext.close();
  } finally {
    await page.request.post("/api/scheduling/admin/session/close");
  }
});
