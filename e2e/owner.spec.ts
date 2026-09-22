import { expect, type Page, test } from "@playwright/test";

// The owner's pages, end to end, against the compose app: sign in by the
// real button (a Development route issues the cookie, see compose.yaml),
// then every control on /devbox and /links as the owner presses it. The
// dev box behind /devbox is the in-memory one; the agent is this suite,
// reporting with the token compose sets. Serial: the pages hold state on
// the server that each test builds on and the last one clears.

test.describe.configure({ mode: "serial" });

const heartbeatToken = "development-heartbeat-token";

async function signIn(page: Page, path: string) {
  await page.goto(path);
  // The page shows a spinner until the account probe answers, then either
  // the sign-in button or the owner's controls. Wait for one of those, not
  // for the spinner, which may not have rendered yet.
  const settled =
    'a:has-text("Sign in with Google"), button:has-text("Refresh"), button:has-text("Upload")';
  await page.locator(settled).first().waitFor({ timeout: 15_000 });
  const button = page.getByRole("link", { name: "Sign in with Google" });
  if (await button.count()) {
    await button.click();
    await page.waitForURL(`**${path}`);
    await page.locator(settled).first().waitFor({ timeout: 15_000 });
    // Say plainly when the sign-in did not take, rather than failing later
    // on a control that is only there for the owner. (/links under the
    // compose app is the owner's without a cookie: Fitness__DevelopmentOwner
    // opens the document routes, so its page shows no button.)
    const me = await (
      await page.request.get("/api/scheduling/admin/me")
    ).json();
    expect(
      me.signedIn,
      `signed in after the button: ${JSON.stringify(me)}`,
    ).toBe(true);
  }
}

test.describe("/devbox", () => {
  test("Start brings the box up, the agent's report shows the link, Hold and Park reach the agent", async ({
    page,
    request,
  }) => {
    await signIn(page, "/devbox");
    await expect(page.locator('[aria-label^="Power state:"]')).toBeVisible();

    // Whatever an earlier run left: park first. page.request carries the
    // owner cookie, and the fake box parks on the order at once.
    await page.request.post("/api/devbox/park");
    // That order sits in the queue until an agent takes it. Take it now, so
    // the reports below see only what this test queues.
    await request.post("/api/devbox/heartbeat", {
      headers: { Authorization: `Bearer ${heartbeatToken}` },
      data: {
        remoteControl: "inactive",
        sessions: 0,
        load: 0,
        uptimeSeconds: 1,
      },
    });
    await page.getByRole("button", { name: "Refresh" }).click();
    const start = page.getByRole("button", { name: "Start dev box" });
    await expect(start).toBeEnabled();
    await start.click();

    await expect(page.getByText(/Azure is starting the box/)).toBeVisible();
    await expect(
      page.locator('[aria-label="Power state: running"]'),
    ).toBeVisible({ timeout: 20_000 });
    await expect(start).toBeDisabled();

    // The last report said the service was down; the page says so.
    await expect(
      page.getByText(/Remote Control service is inactive/),
    ).toBeVisible();

    // The agent reports, as the box does once a minute.
    const report = await request.post("/api/devbox/heartbeat", {
      headers: { Authorization: `Bearer ${heartbeatToken}` },
      data: {
        remoteControl: "active",
        sessions: 2,
        load: 0.7,
        uptimeSeconds: 120,
        holdUntil: null,
        environmentUrl: "https://claude.ai/code?environment=env_e2e",
      },
    });
    expect(report.status()).toBe(200);
    expect(await report.json()).toEqual({ holdMinutes: null, park: false });

    await page.getByRole("button", { name: "Refresh" }).click();
    await expect(
      page.getByText(/Remote Control is up with 2 sessions/),
    ).toBeVisible();
    const open = page.getByRole("link", { name: "Open devbox in Claude" });
    await expect(open).toHaveAttribute(
      "href",
      "https://claude.ai/code?environment=env_e2e",
    );

    // Hold, then Park: both queued for the agent, and handed over once.
    await page.getByRole("button", { name: "Hold 2 h" }).click();
    await expect(page.getByText(/Hold for 2 hours queued/)).toBeVisible();
    page.once("dialog", (dialog) => dialog.accept());
    await page.getByRole("button", { name: "Park now" }).click();
    await expect(page.getByText(/Park queued/)).toBeVisible();

    const orders = await request.post("/api/devbox/heartbeat", {
      headers: { Authorization: `Bearer ${heartbeatToken}` },
      data: {
        remoteControl: "active",
        sessions: 0,
        load: 0.1,
        uptimeSeconds: 200,
      },
    });
    expect(await orders.json()).toEqual({ holdMinutes: 120, park: true });
    const again = await request.post("/api/devbox/heartbeat", {
      headers: { Authorization: `Bearer ${heartbeatToken}` },
      data: {
        remoteControl: "active",
        sessions: 0,
        load: 0.1,
        uptimeSeconds: 260,
      },
    });
    expect(await again.json()).toEqual({ holdMinutes: null, park: false });

    // The fake box parked itself on the order, as the real one does.
    await page.getByRole("button", { name: "Refresh" }).click();
    await expect(
      page.locator('[aria-label="Power state: deallocated"]'),
    ).toBeVisible();
  });

  test("a wrong agent token is refused and a visitor is sent to sign in", async ({
    request,
    browser,
  }) => {
    const wrong = await request.post("/api/devbox/heartbeat", {
      headers: { Authorization: "Bearer not-the-token" },
      data: { sessions: 1 },
    });
    expect(wrong.status()).toBe(401);

    const visitor = await browser.newContext();
    const page = await visitor.newPage();
    await page.goto("/devbox");
    await expect(
      page.getByRole("link", { name: "Sign in with Google" }),
    ).toBeVisible();
    await visitor.close();
  });
});

test.describe("/links", () => {
  const first = `<!DOCTYPE NETSCAPE-Bookmark-file-1>
<DL><p>
    <DT><H3>Bookmarks bar</H3>
    <DL><p>
        <DT><H3>E2E MITRE</H3>
        <DL><p>
            <DT><A HREF="https://e2e.example/mitre" TAGS="e2e-mitre">MITRE home (e2e)</A>
            <DT><H3>Crypto</H3>
            <DL><p>
                <DT><A HREF="https://e2e.example/pqc" TAGS="e2e-mitre">PQC (e2e)</A>
                <DD>Two deep.
            </DL><p>
        </DL><p>
        <DT><A HREF="https://e2e.example/army" TAGS="e2e-army">HRC (e2e)</A>
    </DL><p>
</DL><p>
`;
  const second = `<!DOCTYPE NETSCAPE-Bookmark-file-1>
<DL><p>
    <DT><A HREF="https://e2e.example/mitre">MITRE, renamed (e2e)</A>
    <DT><A HREF="https://e2e.example/army">HRC (e2e)</A>
    <DD>A note from the second file.
</DL><p>
`;

  const upload = async (page: Page, name: string, body: string) => {
    await page
      .locator('input[aria-label="Bookmark file to upload"]')
      .setInputFiles({
        name,
        mimeType: "text/html",
        buffer: Buffer.from(body),
      });
    return page.locator('[role="alert"]').first();
  };

  test.afterAll(async ({ request }) => {
    // Leave the list as it was: drop everything this suite added.
    const doc = await (await request.get("/api/progress/links"))
      .json()
      .catch(() => null);
    if (!doc?.links) return;
    const kept = doc.links.filter(
      (l: { url: string }) => !l.url.startsWith("https://e2e.example/"),
    );
    const ids = new Set(kept.map((l: { id: string }) => l.id));
    await request.put("/api/progress/links", {
      data: {
        version: 1,
        links: kept,
        conflicts: (doc.conflicts ?? []).filter((c: { linkId: string }) =>
          ids.has(c.linkId),
        ),
      },
    });
  });

  test("an export keeps its folders and tags, a second one asks before changing anything", async ({
    page,
  }) => {
    await signIn(page, "/links");
    await expect(page.getByRole("button", { name: "Upload" })).toBeVisible();

    const report = await upload(page, "first.html", first);
    await expect(report).toContainText("first.html: 3 added");
    await expect(
      page.getByRole("list", { name: "Links under E2E MITRE / Crypto" }),
    ).toBeVisible();
    await expect(page.getByRole("link", { name: "PQC (e2e)" })).toBeVisible();

    const again = await upload(page, "second.html", second);
    await expect(again).toContainText("1 to resolve");
    // The title stayed; the note that was empty was filled without a question.
    await expect(
      page.getByRole("link", { name: "MITRE home (e2e)" }),
    ).toBeVisible();
    await expect(page.getByText(/A note from the second file/)).toBeVisible();

    const card = page.getByRole("region", {
      name: "Conflict on MITRE home (e2e)",
    });
    await expect(card).toContainText("MITRE, renamed (e2e)");
    await card.getByRole("button", { name: "Take the file's" }).click();
    await expect(
      page.getByRole("link", { name: "MITRE, renamed (e2e)" }),
    ).toBeVisible();
    await expect(page.getByRole("region", { name: /Conflict on/ })).toHaveCount(
      0,
    );
    // The save is a beat behind each change; leave once it has landed.
    await expect(page.getByText("Saved", { exact: true })).toBeVisible({
      timeout: 10_000,
    });
  });

  test("a tag narrows the page and the download carries just that tag", async ({
    page,
  }) => {
    await signIn(page, "/links");
    const tags = page.getByRole("group", { name: "Tags" });
    await expect(tags).toBeVisible({ timeout: 15_000 });
    await tags.getByText("e2e-army").click();
    await expect(page.getByRole("link", { name: "HRC (e2e)" })).toBeVisible();
    await expect(page.getByRole("link", { name: "PQC (e2e)" })).toHaveCount(0);

    const download = page.waitForEvent("download");
    await page.getByRole("button", { name: "Download" }).click();
    const file = await download;
    expect(file.suggestedFilename()).toMatch(
      /^links-e2e-army-\d{4}-\d{2}-\d{2}\.html$/,
    );
    const text = (await (await file.createReadStream()).toArray()).join("");
    expect(text).toContain("https://e2e.example/army");
    expect(text).not.toContain("https://e2e.example/pqc");
    expect(text).toContain('TAGS="e2e-army"');
  });

  test("Email opens a mail with the file in the body where there is no share sheet", async ({
    page,
  }) => {
    await signIn(page, "/links");
    // Chromium on Linux has no share sheet, so the page takes the mailto
    // path. Record the address the anchor is told to open.
    await page.evaluate(() => {
      (window as unknown as { __hrefs: string[] }).__hrefs = [];
      const click = HTMLAnchorElement.prototype.click;
      HTMLAnchorElement.prototype.click = function (this: HTMLAnchorElement) {
        if (this.href.startsWith("mailto:")) {
          (window as unknown as { __hrefs: string[] }).__hrefs.push(this.href);
          return;
        }
        return click.call(this);
      };
    });
    await page.getByRole("button", { name: "Email" }).click();
    const hrefs = await page.evaluate(
      () => (window as unknown as { __hrefs: string[] }).__hrefs,
    );
    expect(hrefs).toHaveLength(1);
    expect(hrefs[0]).toMatch(/^mailto:\?subject=Links%20/);
    const body = decodeURIComponent(hrefs[0].split("&body=")[1]);
    expect(body).toContain("save it");
    expect(body).toContain("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
  });
});
