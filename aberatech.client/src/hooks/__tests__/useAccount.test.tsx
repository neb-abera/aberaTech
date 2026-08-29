// @vitest-environment jsdom
/**
 * One sign-in probe per page load, no matter how many components ask.
 *
 * The app bar mounts its account-aware control twice (desktop and phone
 * variants), and before this test existed every mount fired its own request to
 * /api/scheduling/admin/me — two identical auth checks from every anonymous
 * visitor, visible in any network tab.
 */

import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { resetAccountProbeForTests, useAccount } from "../useAccount";

afterEach(cleanup);

function Probe({ id }: { id: string }) {
  const { signedIn, resolved } = useAccount();
  return (
    <span data-testid={id}>
      {resolved ? (signedIn ? "in" : "out") : "pending"}
    </span>
  );
}

describe("useAccount", () => {
  beforeEach(() => {
    resetAccountProbeForTests();
    vi.restoreAllMocks();
  });

  it("reports unresolved until the server answers", async () => {
    // Callers deciding something destructive on "not signed in" — like
    // demoting a stored System preference — must be able to tell "no" from
    // "no answer yet".
    let answer!: (response: Response) => void;
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockReturnValue(
          new Promise<Response>((resolve) => (answer = resolve)),
        ),
    );

    render(<Probe id="only" />);

    expect(screen.getByTestId("only").textContent).toBe("pending");

    answer(new Response(JSON.stringify({ signedIn: true })));

    await waitFor(() => {
      expect(screen.getByTestId("only").textContent).toBe("in");
    });
  });

  it("asks the server once for any number of mounts", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify({ signedIn: true })));
    vi.stubGlobal("fetch", fetchMock);

    render(
      <>
        <Probe id="desktop" />
        <Probe id="phone" />
      </>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("desktop").textContent).toBe("in");
      expect(screen.getByTestId("phone").textContent).toBe("in");
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("answers not signed in when the endpoint is missing", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response("gone", { status: 404 })),
    );

    render(<Probe id="only" />);

    await waitFor(() => {
      expect(screen.getByTestId("only").textContent).toBe("out");
    });
  });

  it("answers not signed in when the network fails", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("offline")));

    render(<Probe id="only" />);

    await waitFor(() => {
      expect(screen.getByTestId("only").textContent).toBe("out");
    });
  });
});
