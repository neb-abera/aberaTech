// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { rollingSums } from "../charts";
import TrainingDoseCard from "../TrainingDoseCard";

afterEach(cleanup);

/** 28 days: 85 min in the last seven, 210 in the seven before, 400 in all. */
const minutes: Record<number, number> = {
  0: 45,
  3: 40,
  8: 70,
  10: 70,
  12: 70,
  20: 105,
};
const days = Array.from({ length: 28 }, (_, i) => {
  const back = 27 - i;
  const date = new Date(Date.UTC(2026, 8, 9 - back));
  return {
    date: date.toISOString().slice(0, 10),
    minutes: minutes[back] ?? 0,
  };
});

describe("TrainingDoseCard", () => {
  it("leads with rolling windows and offers calendar weeks", () => {
    render(
      <TrainingDoseCard
        days={days}
        weeks={[
          { weekStart: "2026-08-31", minutes: 210 },
          { weekStart: "2026-09-07", minutes: 85 },
        ]}
        planMinutes={160}
      />,
    );

    expect(
      screen
        .getByRole("button", { name: "Rolling" })
        .getAttribute("aria-pressed"),
    ).toBe("true");
    const headline = screen.getByText(/Last 7 days/).textContent;
    expect(headline).toContain("Last 7 days 85 min");
    expect(headline).toContain("7 before 210 min");
    expect(headline).toContain("28-day average 100 min/wk");
    expect(screen.getByRole("img", { name: /Rolling seven-day/ })).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "By week" }));
    expect(screen.getByRole("img", { name: /Weekly endurance/ })).toBeTruthy();
    expect(screen.queryByText(/Last 7 days/)).toBeNull();
  });

  it("sums only full windows", () => {
    const sums = rollingSums(
      [1, 2, 3, 4].map((m, i) => ({ date: `d${i}`, minutes: m })),
      3,
    );
    expect(sums).toEqual([null, null, 6, 9]);
  });
});
