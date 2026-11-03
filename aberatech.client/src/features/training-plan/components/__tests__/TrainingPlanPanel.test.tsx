// @vitest-environment jsdom
/**
 * The training plan page from three chairs. A visitor gets a sign-in button
 * that returns here. A deployment without the plan says what to set. The
 * owner opens on the week that holds today, moves between weeks without a
 * reload, opens a workout and a note, and gets a download link per file.
 */

import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { respond } from "../../../../test/fakeFetch";
import type { TrainingPlan } from "../../core/api";
import TrainingPlanPanel from "../TrainingPlanPanel";

const plan: TrainingPlan = {
  id: "plan-id",
  title: "Sample plan",
  url: "https://www.notion.so/planid",
  sections: [{ heading: "About the plan", paragraphs: ["Two weeks."] }],
  files: [{ id: "file-id", name: "guide.pdf" }],
  pages: [{ id: "page-id", title: "Plan analysis" }],
  workouts: [
    {
      id: "run",
      title: "Aerobic base run",
      place: {
        date: "2026-11-02",
        week: 1,
        weekday: "Mon",
        block: "Block 1",
        weekLabel: "Block 1 Week 1 Notes",
      },
      sport: "Run",
      order: 1,
      minutes: 45,
      tss: 33,
      description: "Run easy.\nSee https://example.com/maf now.",
      comments: "Nose breathe.",
      structure: "",
      changed: true,
      url: "https://www.notion.so/run",
    },
    {
      id: "test",
      title: "Drift test",
      place: { date: "2026-11-09", week: 2 },
      sport: "Other",
      minutes: 75,
      tss: 57.5,
      intensityFactor: 0.68,
      description: "",
      comments: "",
      structure: "Warm up: 15:00 at 1-3 RPE",
      changed: false,
    },
  ],
  notes: [
    {
      id: "intro",
      title: "Intro",
      place: { date: "2026-11-02", week: 1 },
      description: "Read this first.",
      files: [{ id: "intro:0", name: "intro.pdf" }],
      changed: false,
    },
  ],
};

let fetchMock: ReturnType<typeof vi.fn>;

const settle = async () => {
  await act(async () => {
    for (let i = 0; i < 6; i++) await Promise.resolve();
  });
};

function mount(
  route: string,
  answer: (url: string) => ReturnType<typeof respond>,
  today = "2026-11-10",
) {
  fetchMock = vi.fn(async (url: string) => answer(String(url)));
  vi.stubGlobal("fetch", fetchMock);
  render(
    <MemoryRouter initialEntries={[route]}>
      <TrainingPlanPanel today={today} />
    </MemoryRouter>,
  );
}

const owner = () => respond(200, { configured: true, plan });

beforeAll(async () => {
  // The first MUI mount in a worker pays for the theme, once.
  mount("/training-plan", owner);
  await settle();
  cleanup();
  vi.unstubAllGlobals();
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("a visitor", () => {
  it("is offered sign-in that comes back here", async () => {
    mount("/training-plan", () => respond(401));
    await settle();
    expect(
      screen
        .getByRole("link", { name: "Sign in with Google" })
        .getAttribute("href"),
    ).toBe("/api/scheduling/admin/sign-in?returnUrl=/training-plan");
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});

describe("a deployment without the plan", () => {
  it("says what to set", async () => {
    mount("/training-plan", () =>
      respond(200, {
        configured: false,
        message: "Set Notion__TrainingPlanPageId.",
      }),
    );
    await settle();
    expect(screen.getByText("Set Notion__TrainingPlanPageId.")).toBeTruthy();
  });

  it("says Notion is busy on a 503", async () => {
    mount("/training-plan", () => respond(503));
    await settle();
    expect(
      screen.getByText("Notion is busy. Reload in a minute."),
    ).toBeTruthy();
  });
});

describe("the owner", () => {
  it("opens on the week that holds today, with its totals", async () => {
    mount("/training-plan", owner, "2026-11-10");
    await settle();
    const week = screen.getByRole("region", { name: "Week 2" });
    expect(
      within(week).getByRole("heading", { name: "Week 2 of 2" }),
    ).toBeTruthy();
    expect(
      within(week).getByText("1 h 15 min, 57.5 TSS. Other 1 h 15 min."),
    ).toBeTruthy();
    expect(
      screen
        .getByRole("button", { name: "Week 2" })
        .getAttribute("aria-current"),
    ).toBe("true");
    expect(screen.getByRole("button", { name: "Next week" })).toHaveProperty(
      "disabled",
      true,
    );
    expect(
      screen.getByRole("link", { name: "Edit in Notion" }).getAttribute("href"),
    ).toBe("https://www.notion.so/planid");
  });

  it("moves between weeks and shows a workout's and a note's full text and files", async () => {
    mount("/training-plan?week=2", owner);
    await settle();
    fireEvent.click(screen.getByRole("button", { name: "Previous week" }));
    await settle();
    const week = screen.getByRole("region", { name: "Week 1" });
    expect(
      within(week).getByText(
        "Block 1. Block 1 Week 1 Notes. 2 Nov to 2 Nov 2026",
      ),
    ).toBeTruthy();
    const monday = within(week).getByRole("region", { name: "Mon 2 Nov 2026" });
    expect(within(monday).getByText("Changed")).toBeTruthy();

    fireEvent.click(within(monday).getByText("Aerobic base run"));
    await settle();
    expect(within(monday).getByText("Nose breathe.")).toBeTruthy();
    const link = within(monday).getByRole("link", {
      name: "https://example.com/maf",
    });
    expect(link.getAttribute("target")).toBe("_blank");

    fireEvent.click(within(monday).getByText("Intro"));
    await settle();
    expect(within(monday).getByText("Read this first.")).toBeTruthy();
    expect(
      within(monday)
        .getByRole("link", { name: "Download intro.pdf" })
        .getAttribute("href"),
    ).toBe("/api/training-plan/files/intro%3A0");
    expect(
      screen
        .getByRole("link", { name: "Download guide.pdf" })
        .getAttribute("href"),
    ).toBe("/api/training-plan/files/file-id");
  });

  it("shows a week the plan leaves empty and counts it", async () => {
    const gap = {
      ...plan,
      workouts: [
        ...plan.workouts,
        {
          ...plan.workouts[1],
          id: "w4",
          title: "Later",
          place: { date: "2026-11-23", week: 4 },
        },
      ],
    };
    mount("/training-plan?week=3", () =>
      respond(200, { configured: true, plan: gap }),
    );
    await settle();
    const week = screen.getByRole("region", { name: "Week 3" });
    expect(
      within(week).getByRole("heading", { name: "Week 3 of 4" }),
    ).toBeTruthy();
    expect(
      within(week).getByText("Nothing is planned this week."),
    ).toBeTruthy();
    expect(screen.getByText(/^4 weeks, 3 workouts and 1 notes\./)).toBeTruthy();
  });

  it("lists the pages the plan links and opens one in place", async () => {
    mount("/training-plan", (url) =>
      url === "/api/training-plan/pages/page-id"
        ? respond(200, {
            id: "page-id",
            title: "Plan analysis",
            blocks: [
              { type: "paragraph", text: [{ text: "Three build weeks." }] },
            ],
          })
        : owner(),
    );
    await settle();
    const nav = screen.getByRole("navigation", { name: "Related pages" });
    fireEvent.click(within(nav).getByRole("link", { name: "Plan analysis" }));
    await settle();
    const article = screen.getByRole("article", { name: "Linked page" });
    expect(
      within(article).getByRole("heading", { name: "Plan analysis" }),
    ).toBeTruthy();
    expect(within(article).getByText("Three build weeks.")).toBeTruthy();
    expect(
      within(article)
        .getByRole("link", { name: "Back to the plan" })
        .getAttribute("href"),
    ).toBe("/training-plan");
  });

  it("says so when a linked page is gone", async () => {
    mount("/training-plan?page=gone", (url) =>
      url.includes("/pages/") ? respond(404) : owner(),
    );
    await settle();
    expect(screen.getByText("The plan does not link that page.")).toBeTruthy();
  });

  it("opens the first week before the plan starts", async () => {
    mount("/training-plan", owner, "2026-01-01");
    await settle();
    expect(screen.getByRole("region", { name: "Week 1" })).toBeTruthy();
    expect(
      screen.getByRole("button", { name: "Previous week" }),
    ).toHaveProperty("disabled", true);
  });

  it("shows the structure and planned IF of a workout", async () => {
    mount("/training-plan?week=2", owner);
    await settle();
    fireEvent.click(screen.getByText("Drift test"));
    await settle();
    expect(screen.getByText("Warm up: 15:00 at 1-3 RPE")).toBeTruthy();
    expect(screen.getByText("Planned IF 0.68")).toBeTruthy();
  });
});
