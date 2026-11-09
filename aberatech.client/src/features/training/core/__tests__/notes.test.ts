import { describe, expect, it } from "vitest";
import { noteLimit, setNote } from "../notes";
import { emptyTraining } from "../progress";

describe("a note", () => {
  const now = new Date("2026-11-10T10:00:00Z");

  it("is kept with its time", () => {
    const document = setNote(emptyTraining(), "cp-3", "Symmetry.", now);
    expect(document.notes["cp-3"]).toEqual({
      text: "Symmetry.",
      at: now.toISOString(),
    });
  });

  it("is removed when emptied", () => {
    const document = setNote(
      setNote(emptyTraining(), "cp-3", "x", now),
      "cp-3",
      "  ",
      now,
    );
    expect(document.notes).toEqual({});
  });

  it("is cut at the limit", () => {
    const document = setNote(
      emptyTraining(),
      "cp-3",
      "a".repeat(noteLimit + 10),
      now,
    );
    expect(document.notes["cp-3"].text).toHaveLength(noteLimit);
  });
});
