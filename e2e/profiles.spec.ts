import { expect, test } from "@playwright/test";

// Every profile the site links to is in three places: the hero, the footer
// and the Person sameAs list search engines read (components/Hero.tsx,
// components/Footer.tsx, site/meta.ts). One left out is a profile half the
// site does not know about.

const goodreads = "https://www.goodreads.com/user/show/195486486-neb";

test("the hero, the footer and sameAs carry the Goodreads profile", async ({
  page,
}) => {
  await page.goto("/");

  const links = page.getByRole("link", { name: "Goodreads" });
  await expect(links).toHaveCount(2);
  for (const link of await links.all()) {
    await expect(link).toHaveAttribute("href", goodreads);
    await expect(link).toBeVisible();
  }
  await expect(
    page.locator("footer").getByRole("link", { name: "Goodreads" }),
  ).toHaveCount(1);

  const blocks = await page
    .locator('script[type="application/ld+json"]')
    .allTextContents();
  const person = blocks
    .flatMap((text) => [JSON.parse(text)].flat())
    .find((entry) => entry["@type"] === "Person");
  expect(person?.sameAs).toContain(goodreads);
});
