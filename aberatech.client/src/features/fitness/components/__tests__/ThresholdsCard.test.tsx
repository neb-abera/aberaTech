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
import ThresholdsCard from "../ThresholdsCard";

const settings: SettingsDto = {
  referenceHr: 152,
  ltSecondsPerKm: null,
  ltHr: null,
  planMinutesPerWeek: 160,
  startVdot: 37,
  vdotMeasuredOn: null,
  currentWeightKg: 79,
  birthYear: 1993,
  female: null,
  availableHoursPerWeek: 7,
  sustainedWeeklyHours: null,
  pastPeakDistanceMeters: null,
  pastPeakSeconds: null,
  pastPeakYear: null,
  pastPeakWeightKg: null,
  goalWeightKg: null,
  maxWeightAdjustmentFraction: 0.1,
  homeAltitudeMeters: 15,
  selectionDate: null,
};

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

function stubPut() {
  const fetchMock = vi
    .spyOn(globalThis, "fetch")
    .mockResolvedValue(new Response(null, { status: 204 }));
  return fetchMock;
}

describe("ThresholdsCard", () => {
  it("lists the tests the log contained and applies the suggestion in one click", async () => {
    const fetchMock = stubPut();
    const onSaved = vi.fn();

    render(
      <ThresholdsCard
        settings={settings}
        tests={[
          {
            kind: "aet",
            activityId: "a1",
            date: "2026-08-30",
            secPerKm: 403,
            averageHr: 158,
            driftPercent: 0.031,
            indoor: true,
            evidence: "Held within 5 bpm of your AeT; drift 3.1%.",
          },
        ]}
        suggestion={{
          aetHr: 158,
          ltHr: null,
          ltSecPerKm: null,
          reason: "The 2026-08-30 test held 158 bpm with 3.1% drift.",
          basis: "uphill-athlete-hr-drift",
        }}
        onSaved={onSaved}
      />,
    );

    expect(screen.getByText(/6:43\/km at 158 bpm, drift 3.1%/)).toBeTruthy();
    expect(screen.getByText(/\(treadmill\)/)).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Apply" }));

    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("/api/fitness/settings");
    const body = JSON.parse(String(init.body)) as Record<string, unknown>;
    expect(body.referenceHr).toBe(158);
    expect(body.ltHr).toBeNull();
    expect(body.planMinutesPerWeek).toBe(160);
  });

  it("saves typed thresholds with the pace read as m:ss per km", async () => {
    const fetchMock = stubPut();

    render(
      <ThresholdsCard
        settings={settings}
        tests={[]}
        suggestion={null}
        onSaved={() => {}}
      />,
    );

    fireEvent.change(screen.getByLabelText("LT heart rate (bpm)"), {
      target: { value: "168" },
    });
    fireEvent.change(screen.getByLabelText("LT pace (m:ss per km)"), {
      target: { value: "5:40" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save thresholds" }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    const body = JSON.parse(
      String((fetchMock.mock.calls[0] as [string, RequestInit])[1].body),
    ) as Record<string, unknown>;
    expect(body.ltHr).toBe(168);
    expect(body.ltSecondsPerKm).toBe(340);
    expect(body.referenceHr).toBe(152);
  });

  it("refuses an LT heart rate below the AeT", async () => {
    const fetchMock = stubPut();

    render(
      <ThresholdsCard
        settings={settings}
        tests={[]}
        suggestion={null}
        onSaved={() => {}}
      />,
    );

    fireEvent.change(screen.getByLabelText("LT heart rate (bpm)"), {
      target: { value: "140" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save thresholds" }));

    expect(await screen.findByText(/must sit above the AeT/)).toBeTruthy();
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
