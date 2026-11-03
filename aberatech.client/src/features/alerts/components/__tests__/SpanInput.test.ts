import { describe, expect, it } from "vitest";
import { joinMinutes, splitMinutes } from "../SpanInput";

describe("SpanInput minutes", () => {
  it("shows minutes in the largest whole unit", () => {
    expect(splitMinutes("90")).toEqual({ amount: "90", unit: "minutes" });
    expect(splitMinutes("120")).toEqual({ amount: "2", unit: "hours" });
    expect(splitMinutes("1440")).toEqual({ amount: "1", unit: "days" });
    expect(splitMinutes("0")).toEqual({ amount: "0", unit: "minutes" });
    expect(splitMinutes("")).toEqual({ amount: "", unit: "minutes" });
  });

  it("turns an amount and a unit back into minutes, and empty into empty", () => {
    expect(joinMinutes("3", "hours")).toBe("180");
    expect(joinMinutes("1", "days")).toBe("1440");
    expect(joinMinutes("45", "minutes")).toBe("45");
    expect(joinMinutes(" ", "hours")).toBe("");
  });
});
