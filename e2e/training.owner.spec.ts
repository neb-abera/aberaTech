import { expect, type Page, test } from "@playwright/test";
import { openAsOwner } from "./owner-session";

// /training as the owner, against the compose app: the owner document in
// its database and the judges in memory (FakeJudges.cs, which says
// Nebikinizer solved Codeforces 2263B and was wrong on 2254B). The clock is
// fixed at Tue 10 Nov 2026, a programming day in Phase 0. Every change is
// undone before the test ends, since the three engines share one server.

const ready = '[aria-label="Overall progress"]';

// One document holds all of /training's state, and each save sends the
// whole of it. Two tests saving at once would put one's copy over the
// other's, so this file runs its tests one at a time.
test.describe.configure({ mode: "serial" });

async function open(page: Page, path: string) {
  await page.clock.setFixedTime(new Date("2026-11-10T10:00:00"));
  await openAsOwner(page, path, ready);
}

function saved(page: Page) {
  return page.waitForResponse(
    (response) =>
      new URL(response.url()).pathname === "/api/progress/training" &&
      response.request().method() === "PUT",
  );
}

test("the owner reaches /training from the app bar and works ahead past a session", async ({
  page,
}) => {
  await open(page, "/training");
  await page.goto("/guides");
  await page
    .getByRole("banner")
    .getByRole("link", { name: "Training", exact: true })
    .click();
  await expect(page).toHaveURL(/\/training$/);
  const next = page.getByRole("region", { name: "Next up" });
  await expect(
    next.getByText(/Tue 10 Nov 2026\. Phase 0, week 1 of 8/),
  ).toBeVisible();
  await expect(next.getByRole("region", { name: "Pace" })).toBeVisible();
  await expect(next.getByRole("region", { name: "Scheduled" })).toContainText(
    "Codeforces Round (Div. 3)",
  );

  const list = next.getByRole("list").first();
  const before = await list.getByRole("listitem").count();
  await next.getByRole("button", { name: "Keep going" }).click();
  await expect(list.getByRole("listitem")).toHaveCount(before + 1);
});

test("the owner ticks an item and writes a note, and both survive a reload", async ({
  page,
}) => {
  await open(page, "/training?lesson=cp-3");
  const box = page.getByRole("checkbox", {
    name: /Done: Introduction to Counting & Probability, chapter 3/,
  });
  const note = page.getByRole("textbox", { name: "Lesson notes" });
  const text = `e2e ${test.info().project.name}`;
  // A run stopped halfway can leave the tick on a long-lived database.
  if (await box.isChecked()) {
    const cleared = saved(page);
    await box.uncheck();
    await cleared;
  }

  let put = saved(page);
  await box.check();
  expect((await put).ok()).toBe(true);
  put = saved(page);
  await note.fill(text);
  expect((await put).ok()).toBe(true);

  await page.reload();
  await expect(box).toBeChecked({ timeout: 15_000 });
  await expect(note).toHaveValue(text);

  put = saved(page);
  await box.uncheck();
  expect((await put).ok()).toBe(true);
  put = saved(page);
  await note.fill("");
  expect((await put).ok()).toBe(true);
  await page.reload();
  await expect(box).not.toBeChecked({ timeout: 15_000 });
  await expect(note).toHaveValue("");
});

test("a problem Codeforces reports solved shows solved, and an unsolved one stays open", async ({
  page,
}) => {
  await open(page, "/training?lesson=band-900-1");
  const solved = page.getByRole("article", { name: /Codeforces 2263B/ });
  await expect(solved.getByTestId("item-status")).toContainText(
    "(Codeforces API)",
    { timeout: 15_000 },
  );
  await expect(solved.getByRole("checkbox")).toHaveCount(0);
  const open2254 = page.getByRole("article", { name: /Codeforces 2254B/ });
  await expect(open2254.getByTestId("item-status")).toContainText(
    "counts when the Codeforces API sees it solved",
  );
  await expect(open2254.getByRole("checkbox")).toHaveCount(0);
});

test("a drill runs against its tests and shows which failed", async ({
  page,
}) => {
  await open(page, "/training?lesson=cpp-drills");
  const drill = page.getByRole("region", {
    name: "Drill: Read two numbers, print their sum",
  });
  const editor = drill.getByRole("textbox", { name: "Your C++" });

  // Under `make e2e` the drill goes through the Rust grader to a fake
  // Compiler Explorer (grader/tests/common/fake.rs): "// fake: sum" prints
  // the sum of the input, anything else prints nothing.
  await editor.fill("int main() {}");
  await drill.getByRole("button", { name: "Run 3 tests" }).click();
  await expect(drill.getByText("0 of 3 tests passed.")).toBeVisible();
  await expect(drill.getByText("Test 1: failed")).toBeVisible();

  await editor.fill("#error broken");
  await drill.getByRole("button", { name: "Run 3 tests" }).click();
  await expect(drill.getByText("No test ran.")).toBeVisible();

  await editor.fill("int main() {} // fake: sum");
  await drill.getByRole("button", { name: "Run 3 tests" }).click();
  await expect(drill.getByText("3 of 3 tests passed.")).toBeVisible();
  await expect(
    page
      .getByRole("article", { name: "Read two numbers, print their sum" })
      .getByTestId("item-status"),
  ).toContainText("every test passed on the page");
});

/**
 * Replace the owner's training document for one test and put the original
 * back after, whatever happens in between.
 */
async function withDocument(
  page: Page,
  change: (current: Record<string, unknown>) => Record<string, unknown>,
  body: () => Promise<void>,
) {
  const url = "/api/progress/training";
  const got = await page.request.get(url);
  const original = got.status() === 200 ? await got.text() : null;
  const current = original ? JSON.parse(original) : {};
  const put = (data: string) =>
    page.request.put(url, {
      data,
      headers: { "Content-Type": "application/json" },
    });
  expect((await put(JSON.stringify(change(current)))).ok()).toBe(true);
  try {
    await body();
  } finally {
    expect((await put(original ?? "{}")).ok()).toBe(true);
  }
}

/** A card overdue since 1 Nov 2026, as FSRS would keep it. */
const overdueCard = {
  due: "2026-11-01T12:00:00.000Z",
  stability: 3.2,
  difficulty: 5,
  scheduled_days: 3,
  learning_steps: 0,
  reps: 1,
  lapses: 0,
  state: 2,
  last_review: "2026-10-29T12:00:00.000Z",
};

test("an overdue review backlog holds new material back until it is cleared", async ({
  page,
}) => {
  await open(page, "/training");
  // Thirteen lesson reviews of 5 min: 65 min overdue, over the 60 min session.
  const lessons = [
    "cpp-language",
    "cpp-compile",
    "cpp-drills",
    "cpp-debug",
    "cpp-toolkit-files",
    "first-contest-lesson",
    "lesson-time-comp",
    "lesson-intro-ds",
    "lesson-simulation",
    "band-900-1",
    "band-900-2",
    "band-900-3",
    "lesson-intro-complete",
  ];
  await withDocument(
    page,
    (current) => ({
      ...current,
      cards: Object.fromEntries(
        lessons.map((id) => [`lesson:${id}`, overdueCard]),
      ),
    }),
    async () => {
      await page.reload();
      const next = page.getByRole("region", { name: "Next up" });
      await expect(
        next.getByText(/New material waits\. 1 h 5 min of review is overdue/),
      ).toBeVisible({ timeout: 15_000 });
      const rows = next.getByRole("list").first().getByRole("listitem");
      await expect(rows.first()).toHaveAccessibleName("1. Review");
      const names = await rows.evaluateAll((items) =>
        items.map((item) => item.getAttribute("aria-label") ?? ""),
      );
      expect(names.every((name) => name.endsWith("Review"))).toBe(true);

      // Clear one review: it leaves the queue and the backlog drops to 60 min,
      // which is no more than a session, so new material comes back.
      await page.getByRole("tab", { name: "Review (13)" }).click();
      const review = page.getByRole("region", { name: "Review" });
      const answered = saved(page);
      await review.getByRole("button", { name: /^Good: right\./ }).click();
      expect((await answered).ok()).toBe(true);
      await expect(
        page.getByRole("tab", { name: "Review (12)" }),
      ).toBeVisible();
      await page.getByRole("tab", { name: "Next up" }).click();
      await expect(next.getByText(/New material waits/)).toHaveCount(0);
      // The twelve reviews fill the 60 min session. Keep going reaches new
      // material, which the backlog held back before.
      await next.getByRole("button", { name: "Keep going" }).click();
      await expect(rows.nth(12)).not.toHaveAccessibleName(/Review$/);
    },
  );
});

test("notes are searchable", async ({ page }) => {
  await open(page, "/training");
  await withDocument(
    page,
    (current) => ({
      ...current,
      notes: {
        ...((current.notes as object) ?? {}),
        "cp-3": {
          text: "Stars and bars for e2e.",
          at: "2026-11-09T10:00:00.000Z",
        },
      },
    }),
    async () => {
      await page.goto("/training?view=notes");
      const notes = page.getByRole("region", { name: "Notes" });
      await notes
        .getByRole("searchbox", { name: "Search notes" })
        .fill("bars e2e");
      await expect(notes.getByText("Stars and bars for e2e.")).toBeVisible({
        timeout: 15_000,
      });
      await notes.getByRole("link", { name: /Chapter 3/ }).click();
      await expect(page).toHaveURL(/\/training\?lesson=cp-3$/);
    },
  );
});

test("a failed mastery check shows each wrong answer's reference and opens again 2 days later", async ({
  page,
}) => {
  // Under `make e2e` the server sends two sample questions for the C++
  // refresher's mastery check (FakeAssessment.cs). Every item of the unit
  // is ticked, so the check is next.
  const items = [
    "refresh-learncpp-ints",
    "drill-mod-product",
    "drill-read-eof",
    "refresh-learncpp-refs",
    "drill-reverse-words",
    "refresh-learncpp-lambdas",
    "drill-struct-sort",
  ];
  const ticked = "2026-11-09T10:00:00.000Z";
  await open(page, "/training");
  await withDocument(
    page,
    () => ({
      items: Object.fromEntries(items.map((id) => [id, { at: ticked }])),
    }),
    async () => {
      await page.reload();
      const next = page.getByRole("region", { name: "Next up" });
      const row = next.getByRole("listitem", { name: /\. Mastery check$/ });
      await expect(row).toContainText(
        "Mastery check: Refresher: C++ for contests",
        { timeout: 15_000 },
      );
      await row.getByRole("link", { name: "Start" }).click();

      const check = page.getByRole("region", {
        name: "Mastery check: Refresher: C++ for contests",
      });
      // Wrong on the first.
      const first = check.getByRole("article", { name: /^Question 1:/ });
      await first.getByRole("radio", { name: "Their sum" }).check();
      await first.getByRole("button", { name: "Check" }).click();
      const feedback = first.getByRole("alert", { name: "Wrong" });
      await expect(feedback).toContainText(
        "The right answer: Undefined behavior, often a negative number",
      );
      await expect(feedback).toContainText(
        "Why: Signed overflow is undefined.",
      );
      await expect(
        feedback.getByRole("link", {
          name: "USACO Guide: Debugging checklist",
        }),
      ).toHaveAttribute(
        "href",
        "https://usaco.guide/general/debugging-checklist",
      );

      // Right on the second, by value: 2**64 is 2^64.
      const second = check.getByRole("article", { name: /^Question 2:/ });
      const put = saved(page);
      await second.getByRole("textbox", { name: "Your answer" }).fill("2**64");
      await second.getByRole("button", { name: "Check" }).click();
      await expect(second.getByRole("alert", { name: "Right" })).toBeVisible();
      expect((await put).ok()).toBe(true);
      await expect(check.getByRole("alert", { name: "Result" })).toContainText(
        "1 of 2 right (50%). Not mastered. The retest opens Thu 12 Nov 2026.",
      );

      // The next day: no retest yet, and the missed question is in review.
      await page.clock.setFixedTime(new Date("2026-11-11T10:00:00"));
      await page.goto("/training");
      await expect(
        next.getByText(/the mastery check retest opens Thu 12 Nov 2026/),
      ).toBeVisible({ timeout: 15_000 });
      await expect(
        next.getByRole("listitem", { name: /\. Mastery check$/ }),
      ).toHaveCount(0);
      await expect(
        next.getByRole("listitem", { name: /\. Review$/ }).first(),
      ).toContainText("Two ints near 2e9 are added.");

      // Two days after: the retest is back in Next up.
      await page.clock.setFixedTime(new Date("2026-11-12T10:00:00"));
      await page.goto("/training");
      await expect(
        next.getByRole("listitem", { name: /\. Mastery check$/ }),
      ).toHaveCount(1, { timeout: 15_000 });
    },
  );
});
