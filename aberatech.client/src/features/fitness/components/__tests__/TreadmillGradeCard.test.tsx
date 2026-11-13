// @vitest-environment jsdom
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { SettingsDto } from "../../core/api";
import TreadmillGradeCard, { flatPace } from "../TreadmillGradeCard";

const settings: SettingsDto = {
  referenceHr: 152,
  ltSecondsPerKm: null,
  ltHr: 168,
  planMinutesPerWeek: 160,
  startVdot: 37,
  vdotMeasuredOn: null,
  currentWeightKg: 79,
  birthYear: 1991,
  female: null,
  availableHoursPerWeek: 7,
  sustainedWeeklyHours: null,
  pastPeakDistanceMeters: null,
  pastPeakSeconds: null,
  pastPeakYear: null,
  pastPeakWeightKg: null,
  goalWeightKg: null,
  maxWeightAdjustmentFraction: 0.1,
  homeAltitudeMeters: 0,
};

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

function stubPut() {
  return vi
    .spyOn(globalThis, "fetch")
    .mockResolvedValue(new Response(null, { status: 204 }));
}

function sentBody(fetchMock: ReturnType<typeof stubPut>) {
  const [, init] = fetchMock.mock.calls[0];
  return JSON.parse(String((init as RequestInit).body));
}

describe("TreadmillGradeCard", () => {
  it("converts a pace the way the server does", () => {
    expect(flatPace(376.2, 1)).toBeCloseTo(360, 6);
    expect(flatPace(360, 0)).toBe(360);
  });

  it("saves the incline and its start date and keeps every other setting", async () => {
    const fetchMock = stubPut();
    const onSaved = vi.fn();
    render(
      <TreadmillGradeCard
        settings={settings}
        examplePace={390}
        onSaved={onSaved}
      />,
    );

    fireEvent.change(screen.getByLabelText("Incline (%)"), {
      target: { value: "1" },
    });
    fireEvent.change(screen.getByLabelText("Since"), {
      target: { value: "2026-10-20" },
    });
    expect(
      screen.getByText(/6:30\/km on the belt reads as 6:13\/km flat/),
    ).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Save incline" }));

    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    const body = sentBody(fetchMock);
    expect(body.treadmillGradePercent).toBe(1);
    expect(body.treadmillGradeSince).toBe("2026-10-20");
    expect(body.ltHr).toBe(168);
    expect(body.birthYear).toBe(1991);
  });

  it("refuses an incline without a date before it reaches the server", async () => {
    const fetchMock = stubPut();
    render(
      <TreadmillGradeCard
        settings={settings}
        examplePace={null}
        onSaved={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByLabelText("Incline (%)"), {
      target: { value: "1" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save incline" }));

    expect(await screen.findByText(/Pick the date/)).toBeTruthy();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("clears a saved incline in one click", async () => {
    const fetchMock = stubPut();
    render(
      <TreadmillGradeCard
        settings={{
          ...settings,
          treadmillGradePercent: 1,
          treadmillGradeSince: "2026-10-20",
        }}
        examplePace={390}
        onSaved={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Clear" }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    const body = sentBody(fetchMock);
    expect(body.treadmillGradePercent).toBeNull();
    expect(body.treadmillGradeSince).toBeNull();
  });
});
