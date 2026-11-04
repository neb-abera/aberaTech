// @vitest-environment jsdom
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import FbiEntry from "../FbiEntry";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("FbiEntry", () => {
  it("sends only the events run, blanks as null", async () => {
    const fetch = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ total: 9, eventsRun: 2 }), {
        headers: { "Content-Type": "application/json" },
      }),
    );
    vi.stubGlobal("fetch", fetch);
    render(<FbiEntry onSaved={() => undefined} />);

    fireEvent.change(screen.getByLabelText("Push-ups"), {
      target: { value: "52" },
    });
    fireEvent.change(screen.getByLabelText("300 m sprint (s)"), {
      target: { value: "44.1" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save test" }));

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));
    const body = JSON.parse(fetch.mock.calls[0][1].body as string);
    expect(body.pushUps).toBe(52);
    expect(body.sprint300mSeconds).toBe(44.1);
    expect(body.pullUps).toBeNull();
    expect(body.run1_5MileSeconds).toBeNull();
    expect(await screen.findByText(/9 points over 2 of 4 events/)).toBeTruthy();
  });

  it("refuses an empty test", () => {
    const fetch = vi.fn();
    vi.stubGlobal("fetch", fetch);
    render(<FbiEntry onSaved={() => undefined} />);
    fireEvent.click(screen.getByRole("button", { name: "Save test" }));
    expect(screen.getByText("Enter at least one event.")).toBeTruthy();
    expect(fetch).not.toHaveBeenCalled();
  });
});
