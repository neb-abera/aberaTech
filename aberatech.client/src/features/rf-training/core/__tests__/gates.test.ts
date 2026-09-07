import { describe, expect, it } from "vitest";
import {
  type Attempt,
  asAttemptLog,
  isoDate,
  newAttemptId,
  summarize,
} from "../gates";

describe("summarize", () => {
  it("is empty for no attempts", () => {
    expect(summarize([])).toEqual({
      attempts: 0,
      passes: 0,
      latest: null,
      best: null,
    });
  });

  it("counts passes, keeps the latest, and finds the fastest pass", () => {
    const attempts: Attempt[] = [
      { id: "a", on: "2027-01-05", passed: false, minutes: 41 },
      {
        id: "b",
        on: "2027-01-19",
        passed: true,
        minutes: 33,
        note: "headlamp",
      },
      { id: "c", on: "2027-02-02", passed: true, minutes: 27 },
      { id: "d", on: "2027-02-16", passed: false },
    ];
    const summary = summarize(attempts);
    expect(summary.attempts).toBe(4);
    expect(summary.passes).toBe(2);
    expect(summary.latest).toEqual(attempts[3]);
    expect(summary.best).toEqual(attempts[2]);
  });

  it("does not let a fast fail count as the best", () => {
    const summary = summarize([
      { id: "a", on: "2027-01-05", passed: false, minutes: 5 },
      { id: "b", on: "2027-01-06", passed: true },
    ]);
    expect(summary.best).toBeNull();
    expect(summary.passes).toBe(1);
  });
});

describe("isoDate", () => {
  it("writes the local calendar date with zero padding", () => {
    expect(isoDate(new Date(2027, 0, 5, 23, 30))).toBe("2027-01-05");
  });
});

describe("asAttemptLog", () => {
  it("keeps only well-formed attempts and drops everything else", () => {
    expect(asAttemptLog("nope")).toBeNull();
    expect(asAttemptLog([1, 2])).toBeNull();
    expect(
      asAttemptLog({
        wire: [
          { id: "x", on: "2027-01-05", passed: true, minutes: 30 },
          { on: 5, passed: true },
          "junk",
        ],
        networks: "not a list",
      }),
    ).toEqual({
      wire: [{ id: "x", on: "2027-01-05", passed: true, minutes: 30 }],
    });
  });

  it("gives an entry saved without an id one from its position", () => {
    expect(
      asAttemptLog({ wire: [{ on: "2027-01-05", passed: true }] }),
    ).toEqual({
      wire: [{ id: "wire-legacy-0", on: "2027-01-05", passed: true }],
    });
  });

  it("makes ids that differ", () => {
    expect(newAttemptId()).not.toBe(newAttemptId());
  });
});
