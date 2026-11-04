// @vitest-environment jsdom
import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import PushoverWarning from "../PushoverWarning";

afterEach(() => cleanup());

const show = async (
  answer: { known: boolean; devices: string[]; licensed: boolean } | null,
) => {
  await act(async () => {
    render(<PushoverWarning read={async () => answer} />);
  });
};

describe("Pushover warning", () => {
  it("says so when Pushover has no device", async () => {
    await show({ known: true, devices: [], licensed: false });
    expect(screen.getByText("Pushover has no device")).toBeTruthy();
  });

  it("warns while the device is on the trial", async () => {
    await show({ known: true, devices: ["iphone"], licensed: false });
    expect(screen.getByText("Pushover is on its 30-day trial")).toBeTruthy();
  });

  it("says nothing with a licensed device, or when Pushover could not be asked", async () => {
    await show({ known: true, devices: ["Neb_iphone"], licensed: true });
    expect(screen.queryByText("Pushover has no device")).toBeNull();
    expect(
      screen.getByText("Pushover delivers to Neb_iphone, licensed."),
    ).toBeTruthy();
    cleanup();
    await show({ known: false, devices: [], licensed: false });
    expect(screen.queryByText("Pushover has no device")).toBeNull();
    cleanup();
    await show(null);
    expect(screen.queryByText("Pushover has no device")).toBeNull();
  });
});
