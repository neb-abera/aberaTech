// @vitest-environment jsdom
/**
 * The cards from the reader's chair: every table present, the band lengths
 * printed in feet and inches, and one button that hands the page to the
 * browser's print dialog.
 */

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { bandLengths, phonetic, prowords } from "../../core/reference";
import PlanTemplate from "../PlanTemplate";
import ReferenceCards from "../ReferenceCards";

afterEach(cleanup);

describe("the reference cards", () => {
  it("show every card", () => {
    render(<ReferenceCards />);
    for (const name of [
      "Formulas",
      "Decibel table",
      "Antenna lengths",
      "Prowords",
      "Zulu offsets",
    ]) {
      expect(screen.getByRole("table", { name })).toBeTruthy();
    }
    expect(
      screen.getByRole("region", { name: "Phonetic alphabet" }),
    ).toBeTruthy();
  });

  it("print the antenna lengths in feet and inches for every band", () => {
    render(<ReferenceCards />);
    const table = screen.getByRole("table", { name: "Antenna lengths" });
    for (const band of bandLengths) {
      expect(table.textContent).toContain(String(band.mhz));
    }
    // 7.1 MHz: 468 / 7.1 = 65.9 ft, which is 65 feet 11 inches.
    expect(table.textContent).toContain("65′ 11″");
  });

  it("list all twenty six letters and every proword", () => {
    render(<ReferenceCards />);
    for (const [, word] of phonetic)
      expect(screen.getByText(word)).toBeTruthy();
    for (const [word] of prowords) expect(screen.getByText(word)).toBeTruthy();
  });

  it("hand the page to the print dialog", () => {
    const print = vi.fn();
    vi.stubGlobal("print", print);
    render(<ReferenceCards />);

    fireEvent.click(
      screen.getByRole("button", {
        name: "Print the cards and the plan template",
      }),
    );

    expect(print).toHaveBeenCalledTimes(1);
    vi.unstubAllGlobals();
  });
});

describe("the plan template", () => {
  it("shows the standard and every section with its questions", () => {
    render(<PlanTemplate />);
    expect(screen.getByText(/inside four hours/)).toBeTruthy();
    expect(screen.getByRole("region", { name: "Net diagram" })).toBeTruthy();
    expect(screen.getByRole("region", { name: "Power" }).textContent).toContain(
      "amp-hours",
    );
  });
});
