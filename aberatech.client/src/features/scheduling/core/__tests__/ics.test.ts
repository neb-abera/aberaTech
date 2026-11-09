import { describe, expect, it } from "vitest";
import { buildIcs, eventId } from "../ics";

const booking = {
  id: "0F1D2C3B-4A59-4687-9A0B-1C2D3E4F5A6B",
  startsAt: "2026-11-16T15:00:00Z",
  endsAt: "2026-11-16T15:30:00Z",
  hostName: "Neb Abera",
};

describe("buildIcs", () => {
  const ics = buildIcs(booking, new Date("2026-11-09T12:00:00Z"));
  const lines = ics.split("\r\n");

  it("is one event with the booking's exact times in UTC", () => {
    expect(lines[0]).toBe("BEGIN:VCALENDAR");
    expect(lines).toContain("DTSTART:20261116T150000Z");
    expect(lines).toContain("DTEND:20261116T153000Z");
    expect(lines).toContain("DTSTAMP:20261109T120000Z");
    expect(lines).toContain("SUMMARY:Meeting with Neb Abera");
    expect(lines.filter((line) => line === "BEGIN:VEVENT")).toHaveLength(1);
  });

  it("shares its UID with the Google invite, so a calendar holds one event", () => {
    expect(eventId(booking.id)).toBe("0f1d2c3b4a5946879a0b1c2d3e4f5a6b");
    expect(lines).toContain("UID:0f1d2c3b4a5946879a0b1c2d3e4f5a6b@google.com");
  });

  it("uses CRLF line ends and no line past 75 characters", () => {
    expect(ics.endsWith("END:VCALENDAR\r\n")).toBe(true);
    expect(ics.replace(/\r\n/g, "")).not.toMatch(/[\r\n]/);
    for (const line of lines) expect(line.length).toBeLessThanOrEqual(75);
  });

  it("escapes a host name with a comma", () => {
    const named = buildIcs({ ...booking, hostName: "Abera, Neb" });
    expect(named).toContain("SUMMARY:Meeting with Abera\\, Neb");
  });
});
