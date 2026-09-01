// @vitest-environment jsdom
/**
 * Bringing data in, from the user's chair.
 *
 * The page used to make you say which of three buttons your download was, and
 * the file Garmin actually emails — the "Export Your Data" archive — matched
 * none of them. There is one door now, so what matters from out here is that a
 * file goes to it untouched and the page reports back what the server made of
 * it.
 */

import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import type { SettingsDto } from "../../core/api";
import DataPanel from "../DataPanel";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

const settings: SettingsDto = {
  referenceHr: 152,
  ltSecondsPerKm: 340,
  planMinutesPerWeek: 160,
  startVdot: 37,
  vdotMeasuredOn: null,
  currentWeightKg: 78.9,
  birthYear: 1993,
  pastPeakDistanceMeters: 3218.688,
  pastPeakSeconds: 765,
  pastPeakYear: 2019,
  homeAltitudeMeters: 1190,
  pastPeakWeightKg: 80.7,
  goalWeightKg: 74.8,
  maxWeightAdjustmentFraction: 0.1,
  female: null,
  availableHoursPerWeek: 7,
};

function json(body: unknown, ok = true): Response {
  return new Response(JSON.stringify(body), {
    status: ok ? 200 : 400,
    headers: { "Content-Type": "application/json" },
  });
}

/** Answers /activities, and lets each test say what the import returns. */
function stubFetch(
  reply: (request: Request) => Response,
  page: { activities: unknown[]; total: number; limit: number } = {
    activities: [],
    total: 0,
    limit: 50,
  },
) {
  const calls: Request[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn((input: RequestInfo, init?: RequestInit) => {
      const request = new Request(
        typeof input === "string" ? `http://localhost${input}` : input,
        init,
      );
      calls.push(request);
      if (request.url.endsWith("/api/fitness/activities")) {
        return Promise.resolve(json(page));
      }
      return Promise.resolve(reply(request));
    }),
  );
  return calls;
}

function mount(onDataChanged: () => void = () => {}) {
  return render(
    <DataPanel
      hevyApi={false}
      settings={settings}
      onDataChanged={onDataChanged}
    />,
  );
}

function pick(container: HTMLElement, file: File) {
  const input = container.querySelector<HTMLInputElement>('input[type="file"]');
  if (!input) throw new Error("no file input on the panel");
  fireEvent.change(input, { target: { files: [file] } });
}

// The worker's first MUI mount pays a one-time emotion cost that can blow the
// first timed test's budget under CI load. Same rehearsal as AppAppBar.
beforeAll(() => {
  stubFetch(() => json({}));
  mount();
  cleanup();
}, 30_000);

describe("bringing data in", () => {
  it("posts whatever file it is given to the one import route", async () => {
    const calls = stubFetch(() =>
      json({
        kind: "Garmin export archive",
        parsed: 28,
        added: 28,
        skipped: 0,
        superseded: 0,
      }),
    );
    const { container } = mount();

    pick(container, new File(["PK"], "garmin-export.zip"));

    await waitFor(() => {
      expect(
        calls.some((call) => call.url.endsWith("/api/fitness/import")),
      ).toBe(true);
    });

    // No kind in the path and none in the body: the bytes decide, not the page.
    const upload = calls.find((call) =>
      call.url.endsWith("/api/fitness/import"),
    );
    expect(upload?.method).toBe("POST");
  });

  it("reports what the server made of the file", async () => {
    stubFetch(() =>
      json({
        kind: "Garmin export archive",
        parsed: 28,
        added: 28,
        skipped: 0,
        superseded: 0,
      }),
    );
    const { container } = mount();

    pick(container, new File(["PK"], "garmin-export.zip"));

    await waitFor(() => {
      expect(
        screen.getByText(/Garmin export archive: 28 activities, 28 new/),
      ).toBeTruthy();
    });
  });

  it("tells the page to rebuild the dashboard, not just the list below", async () => {
    // The bug this guards: an import refreshed the activity table on this tab
    // and nothing else, so the Dashboard — built once when the page loaded —
    // went on showing the state of things before the upload. The rows were
    // visibly there and the charts said there was nothing.
    const onDataChanged = vi.fn();
    stubFetch(() =>
      json({
        kind: "Garmin export archive",
        parsed: 28,
        added: 28,
        skipped: 0,
        superseded: 0,
      }),
    );
    const { container } = mount(onDataChanged);

    pick(container, new File(["PK"], "garmin-export.zip"));

    await waitFor(() => expect(onDataChanged).toHaveBeenCalled());
  });

  it("rebuilds it even when the file added nothing new", async () => {
    // Re-uploading the archive adds nothing, but a supersede still changes
    // what the charts should say.
    const onDataChanged = vi.fn();
    stubFetch(() =>
      json({
        kind: "Garmin export archive",
        parsed: 28,
        added: 0,
        skipped: 0,
        superseded: 28,
      }),
    );
    const { container } = mount(onDataChanged);

    pick(container, new File(["PK"], "garmin-export.zip"));

    await waitFor(() => expect(onDataChanged).toHaveBeenCalled());
  });

  it("shows the server's own words when a file is not one it reads", async () => {
    stubFetch(
      () =>
        new Response("Nothing importable in that file.", {
          status: 400,
        }),
    );
    const { container } = mount();

    pick(container, new File(["nope"], "holiday-photo.jpg"));

    await waitFor(() => {
      expect(screen.getByText(/Nothing importable in that file/)).toBeTruthy();
    });
  });
  it("says how much of the history the table is showing", async () => {
    // It always showed the newest fifty and never said so, which made a
    // truncated list read as the whole log.
    stubFetch(() => json({}), {
      activities: [],
      total: 214,
      limit: 50,
    });
    mount();

    await waitFor(() =>
      expect(screen.getByText(/The newest 0 of 214/)).toBeTruthy(),
    );
  });

  it("can take a wrong row back out", async () => {
    const row = {
      id: "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
      source: "garmin-export",
      startedAt: "2026-08-27T03:57:36Z",
      sport: "run",
      name: "Treadmill Running",
      distanceMeters: 3020,
      durationSeconds: 1200,
      averageHr: 153,
    };
    const calls = stubFetch(() => json({}), {
      activities: [row],
      total: 1,
      limit: 50,
    });
    vi.spyOn(window, "confirm").mockReturnValue(true);
    mount();

    const remove = await screen.findByRole("button", { name: /^Remove / });
    fireEvent.click(remove);

    await waitFor(() => {
      const del = calls.find((call) => call.method === "DELETE");
      expect(del?.url).toContain(`/api/fitness/activities/${row.id}`);
    });
  });
});
