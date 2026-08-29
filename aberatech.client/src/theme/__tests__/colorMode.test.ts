import { describe, expect, it } from "vitest";
import { availableModes, correctedMode } from "../colorMode";

describe("availableModes", () => {
  it("offers System only to account holders", () => {
    expect(availableModes(true)).toContain("system");
    expect(availableModes(false)).not.toContain("system");
  });

  it("always offers both real schemes", () => {
    for (const signedIn of [true, false]) {
      expect(availableModes(signedIn)).toEqual(
        expect.arrayContaining(["light", "dark"]),
      );
    }
  });
});

describe("correctedMode", () => {
  it("leaves a valid choice alone", () => {
    expect(correctedMode("dark", false)).toBeNull();
    expect(correctedMode("light", false)).toBeNull();
    expect(correctedMode("system", true)).toBeNull();
  });

  it("moves a signed-out visitor off System, to dark rather than light", () => {
    // The case worth handling: chose System while signed in, then signed out.
    // Without this they sit on a setting they can neither see nor change, in a
    // menu whose every entry looks unselected. Dark, because that is the
    // default a fresh visitor gets.
    expect(correctedMode("system", false)).toBe("dark");
  });

  it("does nothing before the scheme has resolved", () => {
    expect(correctedMode(undefined, false)).toBeNull();
    expect(correctedMode(undefined, true)).toBeNull();
  });
});
