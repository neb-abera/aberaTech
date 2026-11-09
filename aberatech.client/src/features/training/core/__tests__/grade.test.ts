import fc from "fast-check";
import { describe, expect, it } from "vitest";
import { ParseError, parse, readError, sameValue } from "../grade";

describe("short answers graded by value", () => {
  it.each([
    ["1/2", "0.5"],
    ["2/4", "1/2"],
    ["1024", "2^10"],
    ["2**10", "2^10"],
    ["sqrt(2)/2", "1/sqrt(2)"],
    ["3pi", "3*pi"],
    ["3π", "3pi"],
    ["x = 7", "7"],
    ["-2^2", "-4"],
    ["2(3+4)", "14"],
    ["n(n+1)/2", "(n^2+n)/2"],
    ["(a+b)^2", "a^2+2ab+b^2"],
    ["2sqrt(3)", "sqrt(12)"],
    ["1.5e", "1.5e"],
  ])("takes %s for %s", (given, key) => {
    expect(sameValue(given, key)).toBe(true);
  });

  it.each([
    ["0.333", "1/3"],
    ["n(n-1)/2", "n(n+1)/2"],
    ["a^2+b^2", "(a+b)^2"],
    ["5", "6"],
    ["", "1"],
    ["2 +", "2"],
    ["hello!", "1"],
  ])("refuses %s for %s", (given, key) => {
    expect(sameValue(given, key)).toBe(false);
  });

  it("says why an answer cannot be read", () => {
    expect(readError("2 + (3")).toBe("A parenthesis is not closed.");
    expect(readError("3 $")).toBe('Unexpected "$".');
    expect(readError("2^10")).toBeNull();
  });

  it("never throws on any input but a ParseError", () => {
    fc.assert(
      fc.property(fc.string({ maxLength: 40 }), (text) => {
        try {
          parse(text);
        } catch (error) {
          expect(error).toBeInstanceOf(ParseError);
        }
        expect(typeof sameValue(text, "1")).toBe("boolean");
      }),
    );
  });

  it("grades integer sums and products right", () => {
    fc.assert(
      fc.property(
        fc.integer({ min: -1e6, max: 1e6 }),
        fc.integer({ min: -1e6, max: 1e6 }),
        (a, b) =>
          sameValue(`${a}+(${b})`, String(a + b)) &&
          sameValue(`(${a})(${b})`, String(a * b)) &&
          !sameValue(`${a}+(${b})`, String(a + b + 1)),
      ),
    );
  });
});
