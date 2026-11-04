// @vitest-environment jsdom
import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import PushoverWarning from "../PushoverWarning";

afterEach(() => cleanup());

const show = async (answer: { known: boolean; devices: string[] } | null) => {
  await act(async () => {
    render(<PushoverWarning read={async () => answer} />);
  });
};

describe("Pushover warning", () => {
  it("says so when Pushover has no device", async () => {
    await show({ known: true, devices: [] });
    expect(screen.getByText("Pushover has no device")).toBeTruthy();
  });

  it("says nothing with a device, or when Pushover could not be asked", async () => {
    await show({ known: true, devices: ["iphone"] });
    expect(screen.queryByText("Pushover has no device")).toBeNull();
    cleanup();
    await show({ known: false, devices: [] });
    expect(screen.queryByText("Pushover has no device")).toBeNull();
    cleanup();
    await show(null);
    expect(screen.queryByText("Pushover has no device")).toBeNull();
  });
});
