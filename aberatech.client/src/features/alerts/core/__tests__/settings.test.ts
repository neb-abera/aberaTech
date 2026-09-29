/**
 * The words and arithmetic behind the settings: when an alarm stops, what
 * it does, and the page's summary.
 */

import { expect, it, describe as suite } from "vitest";
import { settings } from "../../../../test/alertsFixtures";
import {
  alarmLine,
  count,
  describe,
  isNonstop,
  longSounds,
  nonstop,
  stopWork,
} from "../settings";

suite("alarmLine", () => {
  it("says how often an alarm rings and when it stops, with the saved numbers", () => {
    expect(alarmLine(settings, 50)).toBe(
      "rings every 60 s until you acknowledge it on the phone or here, and stops after 50 min.",
    );
    expect(alarmLine({ repeatSeconds: 120, stopAfterMinutes: 30 }, 50)).toBe(
      "rings every 120 s until you acknowledge it on the phone or here, and stops after 30 min.",
    );
    expect(alarmLine({ repeatSeconds: 45, stopAfterMinutes: 180 }, 50)).toBe(
      "rings every 45 s until you acknowledge it on the phone or here, and stops after 37.5 min.",
    );
  });
});

suite("stopWork", () => {
  it("stops at the fiftieth sound when that comes first", () => {
    expect(stopWork(60, 180, 50)).toEqual({
      minutes: 50,
      text: "Stops after 50 min: 50 sounds × 60 s = 50 min, before the 180 min limit.",
    });
  });

  it("stops at the limit when that comes first, or ties with it", () => {
    expect(stopWork(120, 30, 50).minutes).toBe(30);
    expect(stopWork(60, 50, 50)).toEqual({
      minutes: 50,
      text: "Stops after 50 min: the limit comes before 50 sounds × 60 s = 50 min.",
    });
  });
});

suite("describe", () => {
  it("says an alarm always repeats until acknowledged, with the saved numbers", () => {
    expect(describe(settings, 50, "UTC")).toContain(
      "An alarm is one Pushover message. It rings every 60 s until you acknowledge it on the phone or here, and stops after 50 min.",
    );
    expect(
      describe(
        { ...settings, repeatSeconds: 30, stopAfterMinutes: 10 },
        50,
        "UTC",
      ),
    ).toContain(
      "It rings every 30 s until you acknowledge it on the phone or here, and stops after 10 min.",
    );
    expect(describe(settings, 50, "UTC")).not.toMatch(/emergency|priority/i);
    expect(describe(settings, 50, "Asia/Amman")).toContain(
      "Times are in Asia/Amman, the calendar's own zone.",
    );
  });

  it("says what a notification and an unmarked event do", () => {
    expect(describe(settings, 50, "UTC")).toContain(
      "A notification is one message with one sound, and follows the phone's settings. An event with no mark and no type set here sends nothing.",
    );
    expect(
      describe(
        { ...settings, notificationPriority: 1, defaultType: "notification" },
        50,
        "UTC",
      ),
    ).toContain(
      "A notification is one message with one sound, even during Pushover's quiet hours. An event with no mark and no type set here sends a notification.",
    );
  });
});

suite("count", () => {
  it("says one unit in the singular", () => {
    expect(count(1, "minute")).toBe("1 minute");
    expect(count(10, "minute")).toBe("10 minutes");
    expect(count(0, "minute")).toBe("0 minutes");
    expect(count(48, "hour")).toBe("48 hours");
  });
});

suite("nonstop", () => {
  it("is a 30 s repeat with one of Pushover's long sounds, and nothing else", () => {
    expect(nonstop).toEqual({
      repeatSeconds: 30,
      sound: "persistent",
    });
    expect(longSounds).toEqual([
      "alien",
      "climb",
      "persistent",
      "echo",
      "updown",
    ]);
    expect(longSounds).toContain(nonstop.sound);
  });

  it("is pressed only at 30 s with a long sound", () => {
    expect(isNonstop(30, "persistent")).toBe(true);
    expect(isNonstop(30, "echo")).toBe(true);
    expect(isNonstop(30, "siren")).toBe(false);
    expect(isNonstop(60, "persistent")).toBe(false);
  });
});
