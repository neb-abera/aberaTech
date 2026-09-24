import { expect, test } from "@playwright/test";

// Five guides in three columns left a hole in the second row, at the right,
// which reads as a card that failed to load. The rows are centred now, so a
// short last row reads as the end of the list.
//
// Measured on the cards themselves, in a viewport wide enough for three
// columns. A container that centres its contents proves nothing: the cards
// are what a reader sees sitting off to one side.

test.use({ viewport: { width: 1280, height: 900 } });

const guides = [
  "/transition",
  "/technical",
  "/rf-training",
  "/signal-processing",
  "/quantum-cryptography",
];

test("/guides: every row of cards is centred, including a short one", async ({
  page,
}) => {
  await page.goto("/guides");

  // The card comes before the footer's link to the same page.
  const boxes = [];
  for (const href of guides) {
    const box = await page.locator(`a[href="${href}"]`).first().boundingBox();
    expect(box, `no card for ${href}`).not.toBeNull();
    boxes.push(box as { x: number; y: number; width: number; height: number });
  }

  const rows = new Map<number, typeof boxes>();
  for (const box of boxes) {
    const top = Math.round(box.y);
    rows.set(top, [...(rows.get(top) ?? []), box]);
  }
  expect(rows.size, "the cards did not wrap into rows").toBeGreaterThan(1);

  const left = Math.min(...boxes.map((box) => box.x));
  const right = Math.max(...boxes.map((box) => box.x + box.width));
  const centre = (left + right) / 2;

  for (const [top, row] of rows) {
    const rowLeft = Math.min(...row.map((box) => box.x));
    const rowRight = Math.max(...row.map((box) => box.x + box.width));
    expect(
      Math.abs((rowLeft + rowRight) / 2 - centre),
      `the row at y=${top} spans ${rowLeft} to ${rowRight}, centre ${centre}`,
    ).toBeLessThanOrEqual(1);
  }
});
