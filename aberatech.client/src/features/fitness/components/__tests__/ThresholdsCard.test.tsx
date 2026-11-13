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
import type { SettingsDto, ThresholdEntry } from "../../core/api";
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
};

const carried: ThresholdEntry = {
  id: "t1",
  kind: "aet-hr",
  value: 152,
  measuredOn: null,
  source: "in use before the log was kept",
  activityId: null,
  current: true,
};

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

/** Answers the list with the given entries and records every write. */
function stubApi(entries: ThresholdEntry[]) {
  return vi
    .spyOn(globalThis, "fetch")
    .mockImplementation(async (_input, init) => {
      const method = init?.method ?? "GET";
      if (method === "GET") {
        return new Response(JSON.stringify(entries), { status: 200 });
      }
      if (method === "POST") {
        return new Response(JSON.stringify({ current: true }), {
          status: 200,
        });
      }
      return new Response(null, { status: 204 });
    });
}

function writes(fetchMock: ReturnType<typeof stubApi>) {
  return fetchMock.mock.calls
    .filter(([, init]) => (init as RequestInit | undefined)?.method)
    .map(([url, init]) => ({
      url: String(url),
      method: (init as RequestInit).method,
      body: (init as RequestInit).body
        ? (JSON.parse(String((init as RequestInit).body)) as Record<
            string,
            unknown
          >)
        : null,
    }));
}

describe("ThresholdsCard", () => {
  it("tells the reader how to log a threshold", async () => {
    stubApi([carried]);
    render(
      <ThresholdsCard
        settings={settings}
        tests={[]}
        suggestion={null}
        onSaved={() => {}}
      />,
    );

    expect(screen.getByText("How to log a threshold")).toBeTruthy();
    await screen.findByText(/carried over from before the log was kept/);
    expect(screen.getByText(/Assumed 167 bpm/)).toBeTruthy();
  });

  it("logs one threshold alone, with its test date, and touches nothing else", async () => {
    const fetchMock = stubApi([carried]);
    const onSaved = vi.fn();
    render(
      <ThresholdsCard
        settings={settings}
        tests={[]}
        suggestion={null}
        onSaved={onSaved}
      />,
    );

    fireEvent.change(screen.getByLabelText("LT pace (m:ss per km)"), {
      target: { value: "5:40" },
    });
    fireEvent.change(screen.getByLabelText("LT pace test date"), {
      target: { value: "2026-11-10" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Log LT pace" }));

    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    const sent = writes(fetchMock);
    expect(sent).toEqual([
      {
        url: "/api/fitness/thresholds",
        method: "POST",
        body: { kind: "lt-pace", value: 340, measuredOn: "2026-11-10" },
      },
    ]);
    expect(sent.some((w) => w.url.includes("/settings"))).toBe(false);
  });

  it("logs a test's value in one click, dated by the test", async () => {
    const fetchMock = stubApi([carried]);
    render(
      <ThresholdsCard
        settings={settings}
        tests={[
          {
            kind: "aet",
            activityId: "a1",
            date: "2026-11-16",
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
          reason: "The 2026-11-16 test held 158 bpm with 3.1% drift.",
          basis: "uphill-athlete-hr-drift",
        }}
        onSaved={() => {}}
      />,
    );

    const offer = await screen.findByText(/HR drift test on 2026-11-16 gives/);
    fireEvent.click(
      within(offer.closest(".MuiAlert-root") as HTMLElement).getByRole(
        "button",
        { name: "Log it" },
      ),
    );

    await waitFor(() => expect(writes(fetchMock)).toHaveLength(1));
    expect(writes(fetchMock)[0].body).toEqual({
      kind: "aet-hr",
      value: 158,
      measuredOn: "2026-11-16",
      source: "HR drift test",
      activityId: "a1",
    });
  });

  it("refuses a heart rate nobody has without sending it", async () => {
    const fetchMock = stubApi([carried]);
    render(
      <ThresholdsCard
        settings={settings}
        tests={[]}
        suggestion={null}
        onSaved={() => {}}
      />,
    );

    fireEvent.change(screen.getByLabelText("AeT heart rate (bpm)"), {
      target: { value: "30" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Log AeT heart rate" }));

    expect(
      await screen.findByText(/whole number from 80 to 220 bpm/),
    ).toBeTruthy();
    expect(writes(fetchMock)).toHaveLength(0);
  });

  it("removes one wrong entry by its own button", async () => {
    const wrong: ThresholdEntry = {
      id: "t2",
      kind: "aet-hr",
      value: 165,
      measuredOn: "2026-11-12",
      source: "entered by hand",
      activityId: null,
      current: true,
    };
    const fetchMock = stubApi([wrong, { ...carried, current: false }]);
    render(
      <ThresholdsCard
        settings={settings}
        tests={[]}
        suggestion={null}
        onSaved={() => {}}
      />,
    );

    fireEvent.click(
      await screen.findByRole("button", {
        name: "Remove AeT heart rate 165 bpm from 2026-11-12",
      }),
    );

    await waitFor(() => expect(writes(fetchMock)).toHaveLength(1));
    expect(writes(fetchMock)[0]).toMatchObject({
      url: "/api/fitness/thresholds/t2",
      method: "DELETE",
    });
  });
});
