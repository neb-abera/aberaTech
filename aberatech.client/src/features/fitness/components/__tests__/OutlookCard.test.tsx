// @vitest-environment jsdom
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import OutlookCard from "../OutlookCard";

afterEach(cleanup);

const base = {
  weeksNeeded: 6,
  runsPerWeek: 3,
  referenceHr: 152,
  weeks: [],
};

describe("OutlookCard", () => {
  it("says what it still needs instead of a number", () => {
    render(
      <OutlookCard
        outlook={{
          ...base,
          ready: false,
          qualifyingWeeks: 2,
          slopeSecPerKmPerWeek: null,
          ahead: [],
        }}
      />,
    );
    expect(screen.getByText(/Needs more data/).textContent).toContain(
      "2 of 6 so far",
    );
    expect(screen.queryByText(/In 4 weeks/)).toBeNull();
  });

  it("gives the pace 4 and 8 weeks ahead with its range, per mile", () => {
    render(
      <OutlookCard
        outlook={{
          ...base,
          ready: true,
          qualifyingWeeks: 6,
          slopeSecPerKmPerWeek: -5,
          ahead: [
            {
              weeksAhead: 4,
              secPerKm: 375,
              lowSecPerKm: 360,
              highSecPerKm: 390,
            },
            {
              weeksAhead: 8,
              secPerKm: 355,
              lowSecPerKm: 335,
              highSecPerKm: 375,
            },
          ],
        }}
      />,
    );
    expect(screen.getByText(/In 4 weeks/).textContent).toContain("10:04/mi");
    expect(screen.getByText(/Faster by 8.0 s\/mi a week/)).toBeTruthy();
  });

  it("says 1 week, not 1 weeks", () => {
    render(
      <OutlookCard
        outlook={{
          ...base,
          ready: false,
          qualifyingWeeks: 5,
          slopeSecPerKmPerWeek: null,
          ahead: [],
        }}
      />,
    );
    expect(screen.getByText(/Needs more data/).textContent).toContain(
      "get there in 1 week.",
    );
  });
});
