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
import WeightPlanCard from "../WeightPlanCard";

const settings: SettingsDto = {
  referenceHr: 158,
  ltSecondsPerKm: null,
  ltHr: null,
  planMinutesPerWeek: 160,
  startVdot: 37,
  vdotMeasuredOn: null,
  currentWeightKg: 77.1,
  birthYear: 1991,
  female: null,
  availableHoursPerWeek: 7,
  sustainedWeeklyHours: null,
  pastPeakDistanceMeters: null,
  pastPeakSeconds: null,
  pastPeakYear: null,
  pastPeakWeightKg: null,
  goalWeightKg: 74.84,
  maxWeightAdjustmentFraction: 0.1,
  homeAltitudeMeters: 0,
};

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

function stubPatch() {
  return vi
    .spyOn(globalThis, "fetch")
    .mockResolvedValue(new Response(null, { status: 204 }));
}

function sentBody(fetchMock: ReturnType<typeof stubPatch>) {
  const [, init] = fetchMock.mock.calls[0];
  return JSON.parse(String((init as RequestInit).body));
}

describe("WeightPlanCard", () => {
  it("names the goal weight the plan stops at", () => {
    render(<WeightPlanCard settings={settings} onSaved={() => {}} />);
    expect(
      screen.getByText(/stop at 165 lb, the goal weight on the profile/),
    ).toBeTruthy();
  });

  it("saves the rate in kilograms and the start date, and sends nothing else", async () => {
    const fetchMock = stubPatch();
    const onSaved = vi.fn();
    render(<WeightPlanCard settings={settings} onSaved={onSaved} />);

    fireEvent.change(screen.getByLabelText("Loss (lb a week)"), {
      target: { value: "1.5" },
    });
    fireEvent.change(screen.getByLabelText("Began"), {
      target: { value: "2026-10-30" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save weight plan" }));

    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    const body = sentBody(fetchMock);
    expect(body.weightPlanKgPerWeek).toBeCloseTo(0.6804, 4);
    expect(body.weightPlanStartedOn).toBe("2026-10-30");
    expect(Object.keys(body).sort()).toEqual([
      "weightPlanKgPerWeek",
      "weightPlanStartedOn",
    ]);
    expect(
      screen.getByText("Saved. 1.5 lb a week from 2026-10-30 to 165 lb."),
    ).toBeTruthy();
  });

  it("refuses a rate without a date before it reaches the server", async () => {
    const fetchMock = stubPatch();
    render(<WeightPlanCard settings={settings} onSaved={() => {}} />);

    fireEvent.change(screen.getByLabelText("Loss (lb a week)"), {
      target: { value: "1.5" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save weight plan" }));

    expect(
      await screen.findByText("Pick the day the plan began."),
    ).toBeTruthy();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("shows a saved plan in pounds and clears it", async () => {
    const fetchMock = stubPatch();
    render(
      <WeightPlanCard
        settings={{
          ...settings,
          weightPlanKgPerWeek: 0.680389,
          weightPlanStartedOn: "2026-10-30",
        }}
        onSaved={() => {}}
      />,
    );
    expect(
      (screen.getByLabelText("Loss (lb a week)") as HTMLInputElement).value,
    ).toBe("1.5");

    fireEvent.click(screen.getByRole("button", { name: "Clear" }));
    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    const body = sentBody(fetchMock);
    expect(body.weightPlanKgPerWeek).toBeNull();
    expect(body.weightPlanStartedOn).toBeNull();
  });
});
