/**
 * The words for each repeat against the golden vectors the server and the
 * iPhone app test too. routine-ring-vectors.json beside this file is the
 * same file, byte for byte, as the server's copy in
 * aberaTech.Server.Tests/Alerts/Golden and the app's copy in abera-alarms.
 * The server's test checks the rings. The page works out none, so this
 * checks the text.
 */

import { describe, expect, it } from "vitest";
import { type Routine, repeatSummary } from "../routines";
import text from "./routine-ring-vectors.json?raw";

/**
 * The SHA-256 of the file. The server's test and the app's test hold the
 * same value. Change the file in all three places and this with it.
 */
const sha256 =
  "2e8564b13518f7d131cc051fdc0e097fc180a08a398985e08c17fe3dd8d48c88";

interface Vector {
  name: string;
  summary: string;
  routine: Pick<Routine, "days" | "repeat" | "every" | "unit" | "startsOn">;
}

const vectors: Vector[] = JSON.parse(text).vectors;

describe("the routine ring vectors", () => {
  it("are the file the server and the app test", async () => {
    const digest = await crypto.subtle.digest(
      "SHA-256",
      new TextEncoder().encode(text),
    );
    const hex = [...new Uint8Array(digest)]
      .map((byte) => byte.toString(16).padStart(2, "0"))
      .join("");

    expect(hex).toBe(sha256);
    expect(vectors.length).toBeGreaterThanOrEqual(50);
  });

  it.each(vectors.map((vector) => [vector.name, vector] as const))(
    "%s: the summary",
    (_name, vector) => {
      expect(repeatSummary(vector.routine)).toBe(vector.summary);
    },
  );
});
