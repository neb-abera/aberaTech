import { readFile } from "node:fs/promises";
import { expect, test } from "@playwright/test";
import { openAsOwner } from "./owner-session";

// /playbook as the owner, against the compose app and its Notion in memory
// (FakeNotion.cs, made-up content): reach it from the app bar, read the
// root page, open a child page and a database page from the tree, and
// download a file with its own name.

const ready = 'nav[aria-label="Playbook pages"]';

test("the owner reaches /playbook from the app bar", async ({ page }) => {
  await openAsOwner(page, "/playbook", ready);
  await page.goto("/guides");
  await page
    .getByRole("banner")
    .getByRole("link", { name: "Playbook", exact: true })
    .click();
  await expect(page).toHaveURL(/\/playbook$/);
  await expect(page.locator(ready)).toBeVisible();
});

test("the owner reads the root page, opens pages from the tree, and downloads files", async ({
  page,
}) => {
  await openAsOwner(page, "/playbook", ready);
  const nav = page.getByRole("navigation", { name: "Playbook pages" });
  const article = page.getByRole("article", { name: "Page" });

  await expect(
    article.getByRole("heading", { level: 2, name: "Field notes" }),
  ).toBeVisible();
  await expect(
    article.getByRole("heading", { name: "How this works" }),
  ).toBeVisible();
  await expect(article.locator("strong", { hasText: "before" })).toBeVisible();
  await expect(
    article.locator("code", { hasText: "make check" }),
  ).toBeVisible();
  await expect(
    article.getByRole("columnheader", { name: "Owner" }),
  ).toBeVisible();
  await expect(
    article.getByRole("checkbox", { name: "Book the room" }),
  ).toBeChecked();
  await expect(
    article.getByRole("checkbox", { name: "Send the agenda" }),
  ).not.toBeChecked();

  const [text] = await Promise.all([
    page.waitForEvent("download"),
    article.getByRole("link", { name: "Download example.txt" }).click(),
  ]);
  expect(text.suggestedFilename()).toBe("example.txt");
  expect(await readFile(await text.path(), "utf8")).toBe("An example file.\n");

  await nav.getByRole("link", { name: "Checklists" }).click();
  await expect(page).toHaveURL(/\/playbook\?page=/);
  await expect(article.getByText("Print these.")).toBeVisible();
  const [pdf] = await Promise.all([
    page.waitForEvent("download"),
    article.getByRole("link", { name: "Download packing-list.pdf" }).click(),
  ]);
  expect(pdf.suggestedFilename()).toBe("packing-list.pdf");
  expect((await readFile(await pdf.path(), "utf8")).startsWith("%PDF")).toBe(
    true,
  );

  await nav.getByRole("link", { name: "Letter template" }).click();
  await expect(article.getByText("Dear reader,")).toBeVisible();

  await page.reload();
  await expect(article.getByText("Dear reader,")).toBeVisible();
});
