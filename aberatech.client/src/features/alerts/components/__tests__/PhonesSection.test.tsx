// @vitest-environment jsdom
/**
 * The Phones section from its buttons: pairing shows the link as a QR code,
 * a link and the token once, the list shows each phone without its token,
 * and Revoke asks first, then removes it.
 */

import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from "@testing-library/react";
import QRCode from "qrcode";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import type { Device, PairedDevice } from "../../core/api";
import PhonesSection, { PairingQr, type PhonesApi } from "../PhonesSection";

const token = `aat_${"A".repeat(43)}`;
const pairUrl = `aberaalarms://pair#token=${token}`;

const paired: PairedDevice = {
  id: "0b9c6f1e-3f6e-4a53-9d53-8f1b2a7c4d10",
  name: "Neb's iPhone",
  createdAt: "2026-10-28T12:00:00+00:00",
  token,
  pairUrl,
};

const listed: Device = {
  id: paired.id,
  name: paired.name,
  createdAt: paired.createdAt,
  lastSeenAt: null,
};

const when = (iso: string) => `at ${iso}`;

function fakeApi(devices: Device[][], over: Partial<PhonesApi> = {}) {
  const lists = [...devices];
  return {
    list: vi.fn(async () => ({
      ok: true as const,
      devices: lists.length > 1 ? (lists.shift() as Device[]) : lists[0],
    })),
    pair: vi.fn(async () => ({ ok: true as const, device: paired })),
    revoke: vi.fn(async () => ({ ok: true })),
    ...over,
  };
}

const settle = async () => {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
};

beforeAll(() => {
  render(<PhonesSection api={fakeApi([[]])} when={when} />);
  cleanup();
});

afterEach(() => cleanup());

describe("pairing", () => {
  it("shows the link as a QR code, as a link, and the token once with the note", async () => {
    const api = fakeApi([[], [listed]]);
    render(<PhonesSection api={api} when={when} />);
    await settle();
    expect(screen.getByText("No phones paired.")).toBeTruthy();
    const button = screen.getByRole("button", { name: "Pair a phone" });
    expect((button as HTMLButtonElement).disabled).toBe(true);

    fireEvent.change(screen.getByLabelText("Phone name"), {
      target: { value: "  Neb's iPhone  " },
    });
    await act(async () => {
      fireEvent.click(button);
    });
    await settle();

    expect(api.pair).toHaveBeenCalledExactlyOnceWith("Neb's iPhone");
    const region = screen.getByRole("region", { name: "Pairing Neb's iPhone" });
    expect(
      within(region).getByRole("img", { name: "QR code that pairs a phone" }),
    ).toBeTruthy();
    expect(
      within(region)
        .getByRole("link", { name: "Open on this phone" })
        .getAttribute("href"),
    ).toBe(pairUrl);
    expect(within(region).getByLabelText("Token").textContent).toBe(token);
    expect(within(region).getByText(/cannot be shown again/)).toBeTruthy();

    const list = screen.getByRole("list", { name: "Paired phones" });
    expect(within(list).getByText("Neb's iPhone")).toBeTruthy();
    expect(within(list).getByText(/Not seen yet/)).toBeTruthy();
    expect(list.textContent).not.toContain(token);

    fireEvent.click(within(region).getByRole("button", { name: "Done" }));
    expect(screen.queryByRole("region", { name: /Pairing/ })).toBeNull();
    expect(document.body.textContent).not.toContain(token);
  });

  it.each([
    [
      {
        ok: false as const,
        reason: "full" as const,
        detail: "At most 5 phones. Revoke one first.",
      },
      "At most 5 phones. Revoke one first.",
    ],
    [
      { ok: false as const, reason: "invalid" as const },
      "A name of 1 to 60 characters.",
    ],
    [
      { ok: false as const, reason: "throttled" as const },
      "Too many presses. Wait a minute.",
    ],
    [
      { ok: false as const, reason: "visitor" as const },
      "The session expired. Reload and sign in again.",
    ],
    [
      { ok: false as const, reason: "network" as const },
      "The server did not take it. Try again.",
    ],
  ])(
    "a refused pairing says why and shows no token (%#)",
    async (answer, text) => {
      const api = fakeApi([[]], { pair: vi.fn(async () => answer) });
      render(<PhonesSection api={api} when={when} />);
      await settle();

      fireEvent.change(screen.getByLabelText("Phone name"), {
        target: { value: "Sixth" },
      });
      await act(async () => {
        fireEvent.click(screen.getByRole("button", { name: "Pair a phone" }));
      });

      expect(screen.getByText(text)).toBeTruthy();
      expect(screen.queryByRole("region", { name: /Pairing/ })).toBeNull();
    },
  );
});

describe("the list", () => {
  it("names each phone with when it was paired and last seen", async () => {
    render(
      <PhonesSection
        api={fakeApi([
          [{ ...listed, lastSeenAt: "2026-10-28T12:30:00+00:00" }],
        ])}
        when={when}
      />,
    );
    await settle();

    expect(
      screen.getByText(
        "Paired at 2026-10-28T12:00:00+00:00. Last seen at 2026-10-28T12:30:00+00:00.",
      ),
    ).toBeTruthy();
  });

  it("Revoke asks first, Cancel keeps the phone, Yes removes it", async () => {
    const api = fakeApi([[listed], []]);
    render(<PhonesSection api={api} when={when} />);
    await settle();

    fireEvent.click(
      screen.getByRole("button", { name: "Revoke Neb's iPhone" }),
    );
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    expect(api.revoke).not.toHaveBeenCalled();

    fireEvent.click(
      screen.getByRole("button", { name: "Revoke Neb's iPhone" }),
    );
    await act(async () => {
      fireEvent.click(
        screen.getByRole("button", { name: "Yes, revoke Neb's iPhone" }),
      );
    });
    await settle();

    expect(api.revoke).toHaveBeenCalledExactlyOnceWith(listed.id);
    expect(screen.getByText("No phones paired.")).toBeTruthy();
  });

  it("a revoke the server refuses says so", async () => {
    const api = fakeApi([[listed]], {
      revoke: vi.fn(async () => ({ ok: false, reason: "network" as const })),
    });
    render(<PhonesSection api={api} when={when} />);
    await settle();

    fireEvent.click(
      screen.getByRole("button", { name: "Revoke Neb's iPhone" }),
    );
    await act(async () => {
      fireEvent.click(
        screen.getByRole("button", { name: "Yes, revoke Neb's iPhone" }),
      );
    });

    expect(
      screen.getByText("The server did not revoke it. Try again."),
    ).toBeTruthy();
  });

  it("a list that does not load says so", async () => {
    render(
      <PhonesSection
        api={fakeApi([[]], {
          list: vi.fn(async () => ({
            ok: false as const,
            reason: "network" as const,
          })),
        })}
        when={when}
      />,
    );
    await settle();

    expect(screen.getByText(/did not load/)).toBeTruthy();
  });
});

describe("the QR code", () => {
  it("draws one square per dark module of the code, inside a four-module margin", () => {
    render(<PairingQr value={pairUrl} />);

    const svg = screen.getByRole("img", { name: "QR code that pairs a phone" });
    const { modules } = QRCode.create(pairUrl, { errorCorrectionLevel: "M" });
    let dark = 0;
    for (let row = 0; row < modules.size; row++)
      for (let column = 0; column < modules.size; column++)
        if (modules.get(row, column)) dark++;

    expect(svg.getAttribute("viewBox")).toBe(
      `0 0 ${modules.size + 8} ${modules.size + 8}`,
    );
    const d = svg.querySelector("path")?.getAttribute("d") ?? "";
    expect(d.match(/M/g)).toHaveLength(dark);
    // The top-left finder pattern starts at the margin.
    expect(d.startsWith("M4 4h1v1h-1z")).toBe(true);
  });
});
