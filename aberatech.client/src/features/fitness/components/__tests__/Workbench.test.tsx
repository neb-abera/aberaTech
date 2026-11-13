// @vitest-environment jsdom
import {
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { Summary } from "../../core/api";
import { NumberInput } from "../NumberInput";
import Workbench, { plotAxes } from "../Workbench";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("NumberInput", () => {
  it("can be cleared and retyped, and only a value in range is kept", () => {
    const commits: number[] = [];
    render(
      <NumberInput
        label="Hours"
        value={0}
        min={0}
        max={40}
        step={0.5}
        onCommit={(v) => commits.push(v)}
      />,
    );
    const box = screen.getByLabelText("Hours") as HTMLInputElement;
    fireEvent.focus(box);
    fireEvent.change(box, { target: { value: "" } });
    expect(box.value).toBe("");
    fireEvent.change(box, { target: { value: "9" } });
    expect(box.value).toBe("9");
    fireEvent.change(box, { target: { value: "99" } });
    expect(commits).toEqual([9]);
  });
});

describe("Workbench", () => {
  it("starts from 8 h of running and 3 h of strength, with no compliance", () => {
    // Nothing answers: the defaults are what is under test.
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(() => new Promise(() => undefined)),
    );
    const summary = {
      settings: { currentWeightKg: 79, goalWeightKg: 74.8 },
      measuredDose: { runningHours: 1.5, strengthHours: 1.2 },
    } as unknown as Summary;

    render(<Workbench summary={summary} />);

    expect(screen.queryByText(/Compliance/)).toBeNull();
    // Every box names its unit; the date is months from now.
    expect(screen.getByLabelText("months from now")).toBeTruthy();
    const athletic = screen.getByRole("button", {
      name: "Athletic: 8 h run, 3 h strength",
    });
    expect(athletic.className).toContain("MuiChip-filled");

    const strength = screen
      .getByLabelText("Solve for Strength hours")
      .closest(".MuiCard-root") as HTMLElement;
    expect(
      (within(strength).getByRole("spinbutton") as HTMLInputElement).value,
    ).toBe("3");

    fireEvent.click(
      screen.getByRole("button", {
        name: "Evoke plan: 4.2 h run, 2.5 h strength",
      }),
    );
    expect(
      (within(strength).getByRole("spinbutton") as HTMLInputElement).value,
    ).toBe("2.5");
  });
});

describe("plotAxes", () => {
  const keys = (options: { key: string }[]) => options.map((o) => o.key);

  it("shows in each box the axis the chart draws", () => {
    // Solving for hours with the defaults: hours cannot be an axis, so
    // months move across and race weight down.
    const axes = plotAxes("WeeklyHours", "WeeklyHours", "Months", true);
    expect(axes.across).toBe("Months");
    expect(axes.down).toBe("RaceMassKg");
    expect(keys(axes.acrossOptions)).toContain(axes.across);
    expect(keys(axes.downOptions)).toContain(axes.down);
  });

  it("never offers the solved factor or the other axis", () => {
    const axes = plotAxes("WeeklyHours", "Months", "RaceMassKg", true);
    expect(keys(axes.acrossOptions)).toEqual(["Months", "StrengthHours"]);
    expect(keys(axes.downOptions)).toEqual(["RaceMassKg", "StrengthHours"]);
  });

  it("leaves race weight out when no weight is recorded", () => {
    const axes = plotAxes("RaceTime", "WeeklyHours", "RaceMassKg", false);
    expect(axes.down).toBe("Months");
    expect(keys(axes.downOptions)).not.toContain("RaceMassKg");
  });
});
