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
};

function json(body: unknown, ok = true): Response {
  return new Response(JSON.stringify(body), {
    status: ok ? 200 : 400,
    headers: { "Content-Type": "application/json" },
  });
}

/** Answers /activities, and lets each test say what the import returns. */
function stubFetch(reply: (request: Request) => Response) {
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
        return Promise.resolve(json([]));
      }
      return Promise.resolve(reply(request));
    }),
  );
  return calls;
}

function mount() {
  return render(
    <DataPanel hevyApi={false} settings={settings} onProfileSaved={() => {}} />,
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
});
