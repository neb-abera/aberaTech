// @vitest-environment jsdom
/**
 * The document hook, through a tiny component: a visitor never writes, the
 * owner's ticks are saved a beat later, closing the page flushes the last
 * one, and showing the page again takes the server's copy unless a tick of
 * our own is waiting.
 */

import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useOwnerDocument } from "../useOwnerDocument";

interface Doc {
  count: number;
}

function Harness() {
  const { status, value, set, saving } = useOwnerDocument<Doc>(
    "rf-training",
    100,
  );
  return (
    <div>
      <output data-testid="status">{status}</output>
      <output data-testid="count">{value?.count ?? "none"}</output>
      <output data-testid="saving">{saving ? "saving" : "idle"}</output>
      <button
        type="button"
        onClick={() => set((current) => ({ count: (current?.count ?? 0) + 1 }))}
      >
        tick
      </button>
    </div>
  );
}

const respond = (status: number, body: unknown = null) => ({
  status,
  ok: status >= 200 && status < 300,
  headers: {
    get: (name: string) =>
      name === "content-type" ? "application/json" : null,
  },
  json: async () => body,
});

let fetchMock: ReturnType<typeof vi.fn>;

beforeEach(() => {
  vi.useFakeTimers();
  fetchMock = vi.fn();
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

const settle = async () => {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
};

describe("useOwnerDocument", () => {
  it("starts loading and read-only, and stays read-only for a visitor", async () => {
    fetchMock.mockResolvedValueOnce(respond(401));
    render(<Harness />);
    expect(screen.getByTestId("status").textContent).toBe("loading");

    await settle();
    expect(screen.getByTestId("status").textContent).toBe("visitor");

    screen.getByRole("button", { name: "tick" }).click();
    await settle();
    await act(async () => {
      vi.advanceTimersByTime(500);
    });

    // One GET, no PUT: a visitor's browser never writes.
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(screen.getByTestId("count").textContent).toBe("none");
  });

  it("loads the owner's document and saves a change once the debounce passes", async () => {
    fetchMock
      .mockResolvedValueOnce(respond(200, { count: 4 }))
      .mockResolvedValue(respond(204));
    render(<Harness />);
    await settle();
    expect(screen.getByTestId("status").textContent).toBe("owner");
    expect(screen.getByTestId("count").textContent).toBe("4");

    await act(async () => {
      screen.getByRole("button", { name: "tick" }).click();
    });
    await act(async () => {
      screen.getByRole("button", { name: "tick" }).click();
    });
    expect(screen.getByTestId("count").textContent).toBe("6");
    expect(screen.getByTestId("saving").textContent).toBe("saving");
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await act(async () => {
      vi.advanceTimersByTime(150);
    });
    await settle();

    // Two ticks, one PUT, carrying the latest value.
    expect(fetchMock).toHaveBeenCalledTimes(2);
    const [, options] = fetchMock.mock.calls[1];
    expect(options.method).toBe("PUT");
    expect(JSON.parse(options.body)).toEqual({ count: 6 });
    expect(options.keepalive).toBe(false);
    expect(screen.getByTestId("saving").textContent).toBe("idle");
  });

  it("flushes a pending save with keepalive when the page hides", async () => {
    fetchMock
      .mockResolvedValueOnce(respond(404))
      .mockResolvedValue(respond(204));
    render(<Harness />);
    await settle();

    await act(async () => {
      screen.getByRole("button", { name: "tick" }).click();
    });
    await act(async () => {
      window.dispatchEvent(new Event("pagehide"));
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);
    const [, options] = fetchMock.mock.calls[1];
    expect(options.method).toBe("PUT");
    expect(options.keepalive).toBe(true);
    expect(JSON.parse(options.body)).toEqual({ count: 1 });
  });

  it("takes the server's copy again when the page is shown, without writing", async () => {
    // The phone ticked a task while this tab was in the background. On
    // return the tab must show that tick, and must not send its stale copy.
    fetchMock
      .mockResolvedValueOnce(respond(200, { count: 4 }))
      .mockResolvedValueOnce(respond(200, { count: 9 }));
    render(<Harness />);
    await settle();
    expect(screen.getByTestId("count").textContent).toBe("4");

    await act(async () => {
      document.dispatchEvent(new Event("visibilitychange"));
    });
    await settle();

    expect(screen.getByTestId("count").textContent).toBe("9");
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[1][1].method).toBeUndefined();
  });

  it("leaves a tick waiting to be saved alone when the page is shown", async () => {
    fetchMock
      .mockResolvedValueOnce(respond(200, { count: 4 }))
      .mockResolvedValue(respond(204));
    render(<Harness />);
    await settle();

    await act(async () => {
      screen.getByRole("button", { name: "tick" }).click();
    });
    await act(async () => {
      document.dispatchEvent(new Event("visibilitychange"));
    });
    await settle();

    // No reload while our own change is pending: the tick is what is true.
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(screen.getByTestId("count").textContent).toBe("5");

    await act(async () => {
      vi.advanceTimersByTime(150);
    });
    await settle();
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(JSON.parse(fetchMock.mock.calls[1][1].body)).toEqual({ count: 5 });
  });

  it("lets a tick made during a reload win over what the reload brings", async () => {
    let answer: (value: unknown) => void = () => {};
    fetchMock
      .mockResolvedValueOnce(respond(200, { count: 4 }))
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            answer = resolve;
          }),
      )
      .mockResolvedValue(respond(204));
    render(<Harness />);
    await settle();

    await act(async () => {
      document.dispatchEvent(new Event("visibilitychange"));
    });
    await act(async () => {
      screen.getByRole("button", { name: "tick" }).click();
    });
    await act(async () => {
      answer(respond(200, { count: 9 }));
    });
    await settle();

    // The reload arrived after the tick, so it stands down; the tick is
    // saved on top of whatever the server had.
    expect(screen.getByTestId("count").textContent).toBe("5");
    await act(async () => {
      vi.advanceTimersByTime(150);
    });
    await settle();
    expect(JSON.parse(fetchMock.mock.calls[2][1].body)).toEqual({ count: 5 });
  });
});
