// @vitest-environment jsdom
import {
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { GoalRow, GoalSection, Goals } from "../../core/api";
import GoalsPanel, {
  ageLabel,
  daysSince,
  forecastDataLine,
  forecastModelLine,
  formatGoalValue,
  goalTag,
  latestDetail,
  splitByRows,
  standardLabel,
  TERMS,
  tagDetail,
} from "../GoalsPanel";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

function row(
  partial: Partial<GoalRow> & Pick<GoalRow, "row" | "label">,
): GoalRow {
  return {
    metric: "m",
    unit: "reps",
    lowerIsBetter: false,
    baseline: null,
    baselineText: null,
    goal: null,
    goalColumn: "F",
    current: null,
    met: null,
    pointsNow: null,
    pointsGoal: null,
    forecast: null,
    note: null,
    ...partial,
  };
}

const sections: GoalSection[] = [
  {
    name: "Run calculator",
    test: null,
    pointsNow: null,
    pointsGoal: null,
    pointsMax: null,
    scoringNote: null,
    rows: [
      row({
        row: 4,
        label: "10 mile run",
        unit: "s",
        lowerIsBetter: true,
        goal: 4254,
        note: "2027 goal inferred from the 5-mile",
        forecast: {
          probability: 0.42,
          projected: 4400,
          method: "trajectory",
          evidence: "Needs VDOT 48.1.",
          hoursToReach: 8.5,
          dataThrough: "2026-11-05",
          needsData:
            "a new time trial, because the VDOT anchor of 2026-04-03 is 217 days old",
        },
      }),
    ],
  },
  {
    name: "Army Fitness Test",
    test: "aft",
    pointsNow: 428,
    pointsGoal: 500,
    pointsMax: 500,
    scoringNote: "Army Fitness Test tables, men, age band 32-36.",
    rows: [
      row({
        row: 8,
        label: "HR pushups",
        goal: 62,
        pointsNow: 87,
        pointsGoal: 100,
        current: {
          metric: "hand-release-push-ups",
          value: 44,
          basis: "Measured",
          evidence: "AFT of 2026-04-03",
          on: "2026-04-03",
        },
      }),
    ],
  },
  {
    name: "FBI Physical Fitness Test",
    test: "fbi",
    pointsNow: null,
    pointsGoal: 37,
    pointsMax: 40,
    scoringNote: null,
    rows: [
      row({ row: 14, label: "Push-ups", goal: 80, pointsGoal: 10 }),
      row({
        row: 13,
        label: "300m sprint",
        unit: "s",
        lowerIsBetter: true,
        goal: 40.9,
        pointsGoal: 10,
        note: "2027 goal set to the FBI PFT maximum",
      }),
    ],
  },
];

function serve(goals: Goals) {
  vi.spyOn(globalThis, "fetch").mockResolvedValue(
    new Response(JSON.stringify(goals), {
      headers: { "Content-Type": "application/json" },
    }),
  );
}

const connected: Goals = {
  source: {
    origin: "OneDrive",
    configured: true,
    connected: true,
    readAt: "2026-11-03T09:00:00Z",
    modified: "2026-11-02T20:00:00Z",
    error: null,
    sheetName: "goals",
  },
  dueOn: "2027-12-31",
  weeklyHours: 7,
  weekBasis: "the hours a week the profile says you can train",
  sections,
  modeledAt: "2026-11-06T09:30:00Z",
  workoutsThrough: "2026-11-05",
};

describe("GoalsPanel", () => {
  it("says when the forecasts were modeled and which forecasts need more data", async () => {
    serve(connected);
    render(<GoalsPanel />);

    const modeled = await screen.findByTestId("modeled-at");
    expect(modeled.textContent).toMatch(
      /^Modeled Nov 6, 2026, \d{2}:\d{2} (AM|PM) from workouts through Nov 5, 2026\.$/,
    );

    const needs = screen.getByLabelText(
      "Needs more data: a new time trial, because the VDOT anchor of 2026-04-03 is 217 days old.",
    );
    expect(needs.dataset.testid).toBe("needs-data");

    fireEvent.click(screen.getByRole("button", { name: /^Forecast for/ }));
    expect(screen.getByTestId("forecast-data").textContent).toBe(
      "Data through Nov 5, 2026. Needs more data: a new time trial, because the VDOT anchor of 2026-04-03 is 217 days old.",
    );
  });

  it("shows no needs-data mark on a forecast whose data are enough", async () => {
    const enough = structuredClone(connected);
    const forecast = enough.sections[0].rows[0].forecast;
    if (forecast) forecast.needsData = null;
    serve(enough);
    render(<GoalsPanel />);

    await screen.findByTestId("modeled-at");
    expect(screen.queryByTestId("needs-data")).toBeNull();
  });

  it("shows each section of the workbook in its order, tests with their points", async () => {
    serve(connected);
    render(<GoalsPanel />);

    const headings = await screen.findAllByRole("heading", { level: 2 });
    expect(headings.map((h) => h.textContent)).toEqual([
      "Run calculator",
      "Army Fitness Test",
      "FBI Physical Fitness Test",
    ]);

    const aft = screen.getByRole("region", { name: "Army Fitness Test" });
    expect(within(aft).getByTestId("points-now").textContent).toBe("428");
    expect(within(aft).getByText(/goal 500 of 500 pts/)).toBeTruthy();
    expect(within(aft).getByText("HR pushups")).toBeTruthy();
    expect(
      within(aft).getByRole("columnheader", { name: "Latest" }),
    ).toBeTruthy();
    expect(within(aft).getByTestId("latest-age").textContent?.trim()).toMatch(
      /^\d+(d|wk|mo|y)$/,
    );
    expect(
      within(aft).getByLabelText(
        /^44, Measured Apr 3, 2026 \(\d+ days ago\): AFT of 2026-04-03\.$/,
      ),
    ).toBeTruthy();
    expect(within(aft).queryByText("Push-ups")).toBeNull();

    const fbi = screen.getByRole("region", {
      name: "FBI Physical Fitness Test",
    });
    expect(within(fbi).getByText("Push-ups")).toBeTruthy();
    expect(within(fbi).getByText("—")).toBeTruthy();
  });

  it("tags an inferred goal and an FBI maximum from the athlete's own note", async () => {
    serve(connected);
    render(<GoalsPanel />);

    expect(await screen.findByText("inferred")).toBeTruthy();
    expect(screen.getByText("FBI max")).toBeTruthy();
    expect(screen.getByText("1:10:54")).toBeTruthy();
    expect(screen.getByText("0:40.9")).toBeTruthy();
  });

  it("opens a forecast to its arithmetic and the sheet row it came from", async () => {
    serve(connected);
    render(<GoalsPanel />);

    const button = await screen.findByRole("button", {
      name: "Forecast for 10 mile run",
    });
    expect(button.getAttribute("aria-expanded")).toBe("false");
    fireEvent.click(button);

    expect(button.getAttribute("aria-expanded")).toBe("true");
    expect(await screen.findByText("Needs VDOT 48.1.")).toBeTruthy();
    expect(screen.getByText(/Sheet row 4/)).toBeTruthy();
    expect(screen.getByText(/Chance of the goal by then: 42%/)).toBeTruthy();
  });

  it("asks to connect OneDrive when the grant is missing", async () => {
    serve({
      ...connected,
      source: { ...connected.source, connected: false, readAt: null },
      sections: [],
    });
    render(<GoalsPanel />);

    const link = await screen.findByRole("link", { name: "Connect OneDrive" });
    expect(link.getAttribute("href")).toBe("/api/fitness/onedrive/connect");
  });

  it("says why a read failed and keeps the last good goals", async () => {
    serve({
      ...connected,
      source: {
        ...connected.source,
        error: "OneDrive did not answer in time.",
      },
    });
    render(<GoalsPanel />);

    expect(
      await screen.findByText(/OneDrive did not answer in time/),
    ).toBeTruthy();
    expect(
      screen.getByRole("region", { name: "Army Fitness Test" }),
    ).toBeTruthy();
  });
});

describe("goal values", () => {
  it("writes each unit the way the sheet does", () => {
    expect(formatGoalValue("s", 735)).toBe("12:15");
    expect(formatGoalValue("s", 9000)).toBe("2:30:00");
    expect(formatGoalValue("s", 40.9)).toBe("0:40.9");
    expect(formatGoalValue("s/mi", 480)).toBe("8:00/mi");
    expect(formatGoalValue("lb", 247.5)).toBe("247.5 lb");
    expect(formatGoalValue("%", 10)).toBe("10%");
    expect(formatGoalValue("m", 100)).toBe("100 m");
  });

  it("tags a goal taken from the Ideal column", () => {
    expect(
      goalTag(row({ row: 20, label: "Farmer carry", goalColumn: "D" })),
    ).toBe("ideal");
  });
});

describe("columns", () => {
  it("splits the sections in order at about half the rows", () => {
    const named = (name: string, n: number): GoalSection => ({
      name,
      test: null,
      pointsNow: null,
      pointsGoal: null,
      pointsMax: null,
      scoringNote: null,
      rows: Array.from({ length: n }, (_, i) =>
        row({ row: i, label: `${name} ${i}` }),
      ),
    });
    const columns = splitByRows([
      named("Run", 4),
      named("AFT", 5),
      named("FBI", 4),
      named("Body", 2),
      named("Strength", 4),
    ]);
    expect(columns.map((c) => c.map((s) => s.name))).toEqual([
      ["Run", "AFT"],
      ["FBI", "Body", "Strength"],
    ]);
  });

  it("dates the latest result, short beside it and in full on hover", () => {
    const today = new Date(2026, 10, 3);
    expect(daysSince("2026-04-03", today)).toBe(214);
    expect(ageLabel(0)).toBe("0d");
    expect(ageLabel(13)).toBe("13d");
    expect(ageLabel(35)).toBe("5wk");
    expect(ageLabel(214)).toBe("7mo");
    expect(ageLabel(800)).toBe("2y");
    expect(
      latestDetail(
        {
          metric: "hand-release-push-ups",
          value: 44,
          basis: "Measured",
          evidence: "AFT of 2026-04-03",
          on: "2026-04-03",
        },
        today,
      ),
    ).toBe("Measured Apr 3, 2026 (214 days ago): AFT of 2026-04-03.");
  });

  it("says in words that a modeled value was worked out, not done", () => {
    expect(
      latestDetail(
        {
          metric: "run-10mi",
          value: 5400,
          basis: "Modeled",
          evidence: "modeled from your VDOT 37.5 anchor of 2026-04-03",
          on: "2026-04-03",
        },
        new Date(2026, 10, 3),
      ),
    ).toBe(
      "Modeled, not done as this event, from data through Apr 3, 2026 (214 days ago): modeled from your VDOT 37.5 anchor of 2026-04-03.",
    );
  });

  it("labels each outside standard by its source and level", () => {
    const pullUps = {
      ...row({ row: 30, label: "Pull-ups", goal: 15 }),
      unit: "reps",
    } as GoalRow;
    const fbi = { source: "FBI", level: "max (10 pts)", value: 20, note: "" };
    expect(standardLabel(pullUps, fbi)).toBe("FBI max 20");
    expect(standardLabel({ ...pullUps, goal: 20 }, fbi)).toBe("FBI max");
  });

  it("tags a goal set at the FBI 10-point mark as FBI max, though its note names Evoke too", () => {
    expect(
      goalTag(
        row({
          row: 12,
          label: "Pull-ups",
          note: "2027 goal 20 is the FBI PFT 10-point mark, the harder standard. Evoke's standard is 15 strict pull-ups (Kuenzle and Johnston).",
        }),
      ),
    ).toBe("FBI max");
  });

  it("tags Evoke from its standards, with the set count where the row is a one-rep max", () => {
    const squat = {
      ...row({
        row: 21,
        label: "Back Squat",
        goal: 315,
        note: "beats Evoke's 250 lb x5",
      }),
      unit: "lb",
    } as GoalRow;
    expect(goalTag(squat)).toBeNull();
    expect(
      standardLabel(squat, {
        source: "Evoke",
        level: "SFAS-ready",
        value: 250,
        note: "",
        display: "250 lb ×5",
      }),
    ).toBe("Evoke SFAS-ready 250 lb ×5");

    const aet = {
      ...row({ row: 6, label: "Aerobic threshold pace", goal: 480 }),
      unit: "s/mi",
    } as GoalRow;
    expect(
      standardLabel(aet, {
        source: "Evoke",
        level: "SFAS-ready",
        value: 480,
        note: "",
      }),
    ).toBe("Evoke SFAS-ready");
  });
});

describe("forecastDataLine", () => {
  it("names the data date alone when nothing more is needed", () => {
    expect(
      forecastDataLine({
        probability: 0.5,
        projected: null,
        method: "trend",
        evidence: "",
        hoursToReach: null,
        dataThrough: "2026-10-30",
        needsData: null,
      }),
    ).toBe("Data through Oct 30, 2026.");
  });

  it("says when a forecast has no dated data", () => {
    expect(
      forecastDataLine({
        probability: null,
        projected: null,
        method: "none",
        evidence: "",
        hoursToReach: null,
        needsData:
          "3 dated readings over 42 days to make a trend; the log has 0",
      }),
    ).toBe(
      "No dated data. Needs more data: 3 dated readings over 42 days to make a trend; the log has 0.",
    );
  });
});

describe("where each number comes from", () => {
  it("gives a modeled value's data date, source, and model", () => {
    expect(
      latestDetail(
        {
          metric: "run-2mi",
          value: 1009,
          basis: "Modeled",
          evidence: "modeled from your VDOT 37.5 anchor of 2026-04-03",
          on: "2026-04-03",
          source: "the time trial in your profile",
          model:
            "Daniels and Gilbert VDOT race equivalence (1979): the time trial's VDOT gives the equal time at this distance",
        },
        new Date(2026, 10, 3),
      ),
    ).toBe(
      "Modeled, not done as this event, from data through Apr 3, 2026 (214 days ago): modeled from your VDOT 37.5 anchor of 2026-04-03. Source: the time trial in your profile. Model: Daniels and Gilbert VDOT race equivalence (1979): the time trial's VDOT gives the equal time at this distance.",
    );
  });

  it("gives a measured value's source and no model", () => {
    expect(
      latestDetail(
        {
          metric: "hand-release-push-ups",
          value: 44,
          basis: "Measured",
          evidence: "AFT of 2026-04-03",
          on: "2026-04-03",
          source: "an AFT entered on the Log tab",
          model: null,
        },
        new Date(2026, 10, 3),
      ),
    ).toBe(
      "Measured Apr 3, 2026 (214 days ago): AFT of 2026-04-03. Source: an AFT entered on the Log tab.",
    );
  });

  it("gives a forecast's data range, sources, and model", () => {
    const forecast = {
      probability: 0.4,
      projected: 2000,
      method: "trajectory" as const,
      evidence: "",
      hoursToReach: null,
      dataFrom: "2026-05-01",
      dataThrough: "2026-11-05",
      sources:
        "runs from Garmin, and the time trial of 2026-04-03 in your profile",
      model:
        "Banister impulse-response training model (Banister 1975, Busso 2003)",
      needsData: null,
    };
    expect(forecastDataLine(forecast)).toBe(
      "Data: May 1, 2026 to Nov 5, 2026. Sources: runs from Garmin, and the time trial of 2026-04-03 in your profile.",
    );
    expect(forecastModelLine(forecast)).toBe(
      "Model: Banister impulse-response training model (Banister 1975, Busso 2003).",
    );
    expect(
      forecastModelLine({ ...forecast, model: null, method: "held" }),
    ).toBe("No model: the goal is met today and is held, not forecast.");
  });

  it("says what inferred means, with the note's basis", () => {
    const r = row({
      row: 6,
      label: "10 mile run",
      note: "2027 goal inferred from the 5-mile",
    });
    expect(tagDetail("inferred", r)).toBe(
      "Inferred: your sheet had no usable number here, so this goal was worked out from your own records. Basis: 2027 goal inferred from the 5-mile",
    );
    expect(tagDetail("FBI max", r)).toBeNull();
  });

  it("defines measured, modeled, training, inferred, forecast, and the warning mark on the tab", async () => {
    serve(connected);
    render(<GoalsPanel />);
    const terms = await screen.findByTestId("terms");
    expect(TERMS.map((t) => t.term)).toEqual([
      "Measured",
      "Modeled",
      "Training",
      "Inferred",
      "Forecast",
      "Warning mark",
    ]);
    expect(screen.getByTestId("terms-link").getAttribute("href")).toBe(
      "#goal-terms",
    );
    for (const { term } of TERMS)
      expect(within(terms).getByText(term).tagName).toBe("DT");
  });
});
