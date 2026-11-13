// @vitest-environment jsdom
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { Summary } from "../../core/api";
import EffortsCard from "../EffortsCard";
import PlacesCard from "../PlacesCard";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

const summary = {
  efforts: [
    {
      id: "aft-1",
      kind: "Aft2Mile",
      date: "2026-04-03",
      name: "AFT 2-mile",
      distanceMeters: 3218.688,
      seconds: 1009,
      altitudeMeters: 1186,
      vdot: 35.8,
      isAnchor: true,
      removable: false,
      note: "run after four other events, so it sets a floor",
    },
    {
      id: "race-1",
      kind: "Entered",
      date: "2025-10-12",
      name: "Army Ten-Miler",
      distanceMeters: 16093.44,
      seconds: 7255,
      altitudeMeters: 15,
      vdot: 31.9,
      isAnchor: false,
      removable: true,
      note: null,
    },
  ],
  skippedTests: [
    {
      activityId: "a9",
      date: "2026-11-20",
      name: "5 mile time trial",
      reason:
        "the recording covers 9.10 km, not the 8.05 km its name says; save the warm-up as its own run",
    },
  ],
} as unknown as Summary;

function stubWrites() {
  return vi
    .spyOn(globalThis, "fetch")
    .mockResolvedValue(new Response(null, { status: 204 }));
}

describe("EffortsCard", () => {
  it("lists every test with its altitude, marks the anchor, and reads an AFT as a floor", () => {
    render(<EffortsCard summary={summary} onChanged={() => {}} />);

    expect(screen.getByText("How to add one")).toBeTruthy();
    expect(screen.getByText("anchor")).toBeTruthy();
    expect(screen.getByText(/at least 35.8/)).toBeTruthy();
    expect(screen.getByText("1,186 m (3,891 ft)")).toBeTruthy();
    expect(screen.getByText(/save the warm-up as its own run/)).toBeTruthy();
    // An AFT comes from the AFT log, so only the entered race can be removed here.
    expect(screen.queryByRole("button", { name: /Remove AFT/ })).toBeNull();
    expect(
      screen.getByRole("button", {
        name: "Remove Army Ten-Miler of 2025-10-12",
      }),
    ).toBeTruthy();
  });

  it("enters a race at any distance, with an hours time and its altitude", async () => {
    const fetchMock = stubWrites();
    const onChanged = vi.fn();
    render(<EffortsCard summary={summary} onChanged={onChanged} />);

    fireEvent.change(screen.getByLabelText("Time (m:ss or h:mm:ss)"), {
      target: { value: "1:45:30" },
    });
    fireEvent.change(screen.getByLabelText("Date run"), {
      target: { value: "2026-10-04" },
    });
    fireEvent.change(screen.getByLabelText("Altitude (ft, optional)"), {
      target: { value: "1000" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Enter race" }));

    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("/api/fitness/races");
    const body = JSON.parse(String(init.body));
    expect(body.seconds).toBe(6330);
    expect(body.distanceMeters).toBeCloseTo(8046.72, 2);
    expect(body.altitudeMeters).toBeCloseTo(304.8, 1);
    expect(body.name).toBe("5 mile race");
  });

  it("refuses a time it cannot read without sending it", async () => {
    const fetchMock = stubWrites();
    render(<EffortsCard summary={summary} onChanged={() => {}} />);

    fireEvent.change(screen.getByLabelText("Time (m:ss or h:mm:ss)"), {
      target: { value: "34 minutes" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Enter race" }));

    expect(await screen.findByText(/Time looks like 34:10/)).toBeTruthy();
    expect(fetchMock).not.toHaveBeenCalled();
  });
});

describe("PlacesCard", () => {
  it("adds a place in feet, with the day you arrived", async () => {
    const fetchMock = stubWrites();
    render(
      <PlacesCard
        places={[
          {
            id: "p1",
            since: null,
            place: "Home altitude from the profile",
            altitudeMeters: 15,
            current: true,
          },
        ]}
        onChanged={() => {}}
      />,
    );

    expect(screen.getByText("now")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Place"), {
      target: { value: "Fort Bliss" },
    });
    fireEvent.change(screen.getByLabelText("Altitude (ft)"), {
      target: { value: "3891" },
    });
    fireEvent.change(
      screen.getByLabelText("Arrived (blank for before everything)"),
      {
        target: { value: "2026-02-13" },
      },
    );
    fireEvent.click(screen.getByRole("button", { name: "Add place" }));

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    const body = JSON.parse(
      String((fetchMock.mock.calls[0] as [string, RequestInit])[1].body),
    );
    expect(body).toMatchObject({ since: "2026-02-13", place: "Fort Bliss" });
    expect(body.altitudeMeters).toBeCloseTo(1186, 0);
  });
});
