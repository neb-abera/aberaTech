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
  it("saves the biggest week alone, and the anchor race is not sent again", async () => {
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
    expect(body.sustainedWeeklyHours).toBe(8);
    expect(body.availableHoursPerWeek).toBe(7);
    expect(body.anchorSeconds).toBeNull();
    expect(body.vdotMeasuredOn).toBe("2026-04-03");
    expect(body.pastPeakSeconds).toBe(765);
    expect(await within(hours).findByText(/Nothing else changed/)).toBeTruthy();
  });

  it("shows the current anchor and takes a newer race in empty fields", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetch);
    render(<ProfileCard settings={settings} onSaved={() => undefined} />);

    const anchor = screen.getByRole("region", { name: "Anchor race" });
    expect(
      within(anchor).getByText(/VDOT 35.4, from a race on 2026-04-03/),
    ).toBeTruthy();
    const time = within(anchor).getByLabelText(
      "Time (m:ss)",
    ) as HTMLInputElement;
    expect(time.value).toBe("");

    fireEvent.click(
      within(anchor).getByRole("button", { name: "Record race" }),
    );
    expect(fetch).not.toHaveBeenCalled();

    fireEvent.change(time, { target: { value: "15:30" } });
    fireEvent.change(within(anchor).getByLabelText("Race date"), {
      target: { value: "2026-11-20" },
    });
    fireEvent.click(
      within(anchor).getByRole("button", { name: "Record race" }),
    );

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));
    const body = sent(fetch);
    expect(body.anchorSeconds).toBe(930);
    expect(body.vdotMeasuredOn).toBe("2026-11-20");
    expect(body.availableHoursPerWeek).toBe(7);
  });
});
