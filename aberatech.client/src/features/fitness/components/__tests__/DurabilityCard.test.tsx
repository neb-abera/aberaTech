// @vitest-environment jsdom
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import type { Durability } from "../../core/api";
import DurabilityCard from "../DurabilityCard";

afterEach(cleanup);

const days = Array.from({ length: 28 }, (_, i) => ({
  date: `2026-08-${String(11 + (i % 20)).padStart(2, "0")}`,
  load: i % 7 === 6 ? 0 : 1 + (i % 3) * 0.5,
  impact: i % 7 !== 6,
}));

const durability: Durability = {
  acuteLoad: 10.5,
  chronicLoad: 7.9,
  acwr: 1.33,
  monotony: 1.4,
  weeklyStrain: 14.7,
  impactStreakDays: 4,
  restDaysLast7: 1,
  daysOfLog: 60,
  days,
  steps: [
    {
      label: "Acute:chronic ratio",
      expression: "10.5 ÷ 7.9",
      value: "1.33 — safe band 0.8–1.3",
      citationId: "gabbett-workload",
    },
  ],
};

describe("DurabilityCard", () => {
  it("shows the ratio, monotony, and streak with their verdicts", () => {
    render(<DurabilityCard durability={durability} />);

    expect(screen.getByText("1.33")).toBeTruthy();
    expect(screen.getByText("ramping")).toBeTruthy();
    expect(screen.getByText("1.4")).toBeTruthy();
    expect(screen.getByText("4 d")).toBeTruthy();
    expect(screen.getByText(/1 rest days in the last 7/)).toBeTruthy();
    expect(
      screen.getByRole("img", { name: /daily training load/i }),
    ).toBeTruthy();
  });

  it("says when the ratio has no four weeks to stand on", () => {
    render(
      <DurabilityCard
        durability={{ ...durability, acwr: null, daysOfLog: 10 }}
      />,
    );

    expect(screen.getByText("needs 4 weeks of log")).toBeTruthy();
  });
});
