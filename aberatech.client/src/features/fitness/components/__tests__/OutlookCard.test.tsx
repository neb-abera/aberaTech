// @vitest-environment jsdom
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import OutlookCard from "../OutlookCard";

afterEach(cleanup);

const anchor = {
  date: "2026-11-23",
  // 4.5 mph at 2% is 4.905 mph flat: 12:14 a mile.
  secPerKm: 456.1,
  averageHr: 159,
  gradePercent: 2,
  indoor: true,
};

describe("OutlookCard", () => {
  it("asks for a drift test and shows no number without one", () => {
    render(
      <OutlookCard
        outlook={{
          ready: false,
          referenceHr: 158,
          anchor: null,
          needs: "A heart-rate drift test.",
          weeks: [],
          ahead: [],
        }}
      />,
    );
    expect(screen.getByText("Needs: A heart-rate drift test.")).toBeTruthy();
    expect(screen.queryByText(/In 4 weeks/)).toBeNull();
  });

  it("starts from the drift test's flat pace and gets faster, never slower", () => {
    render(
      <OutlookCard
        outlook={{
          ready: true,
          referenceHr: 158,
          anchor,
          needs: null,
          weeks: [],
          ahead: [
            {
              weeksAhead: 4,
              secPerKm: 448.0,
              lowSecPerKm: 447.2,
              highSecPerKm: 448.9,
            },
            {
              weeksAhead: 8,
              secPerKm: 440.3,
              lowSecPerKm: 438.6,
              highSecPerKm: 442.0,
            },
          ],
        }}
      />,
    );
    expect(screen.getByTestId("outlook-anchor").textContent).toBe(
      "Your drift test of Mon, Nov 23, 2026: 12:14/mi on the flat at 159 bpm, run at 2% and read as the flat pace of the same cost.",
    );
    expect(screen.getByText(/In 8 weeks:/).textContent).toContain("11:49/mi");
    expect(screen.getByText(/In 8 weeks:/).textContent).toContain(
      "11:51/mi to 11:46/mi",
    );
    expect(screen.queryByText(/Slower/)).toBeNull();
    expect(screen.queryByText(/straight line/)).toBeNull();
  });

  it("keeps the test on show when it is too old to carry forward", () => {
    render(
      <OutlookCard
        outlook={{
          ready: false,
          referenceHr: 158,
          anchor,
          needs: "A new drift test: the last was 9 weeks ago.",
          weeks: [],
          ahead: [],
        }}
      />,
    );
    expect(screen.getByTestId("outlook-anchor")).toBeTruthy();
    expect(screen.getByText(/A new drift test/)).toBeTruthy();
  });
});
