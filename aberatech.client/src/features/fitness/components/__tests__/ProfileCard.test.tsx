// @vitest-environment jsdom
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { SettingsDto } from "../../core/api";
import ProfileCard from "../ProfileCard";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

const settings: SettingsDto = {
  referenceHr: 152,
  ltSecondsPerKm: null,
  planMinutesPerWeek: 160,
  startVdot: 35.4,
  vdotMeasuredOn: "2026-04-03",
  currentWeightKg: 79.4,
  birthYear: 1991,
  pastPeakDistanceMeters: 3218.688,
  pastPeakSeconds: 765,
  pastPeakYear: 2018,
  homeAltitudeMeters: 15,
  pastPeakWeightKg: 80.7,
  goalWeightKg: 74.8,
  maxWeightAdjustmentFraction: 0.1,
  female: null,
  availableHoursPerWeek: 7,
  sustainedWeeklyHours: null,
  ltHr: null,
};

function sent(spy: ReturnType<typeof vi.fn>) {
  return JSON.parse(spy.mock.calls[0][1].body as string);
}

describe("ProfileCard", () => {
  it("saves the biggest week alone, and nothing else", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetch);
    render(<ProfileCard settings={settings} onSaved={() => undefined} />);

    const hours = screen.getByRole("region", { name: "Training history" });
    fireEvent.change(
      within(hours).getByLabelText("Biggest week you have held (h)"),
      {
        target: { value: "8" },
      },
    );
    fireEvent.click(within(hours).getByRole("button", { name: "Save" }));

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));
    const body = sent(fetch);
    // Only this part's own fields: nothing else can be written back stale.
    expect(Object.keys(body)).toEqual(["sustainedWeeklyHours"]);
    expect(body.sustainedWeeklyHours).toBe(8);
    expect(await within(hours).findByText(/Nothing else changed/)).toBeTruthy();
  });
});
