// @vitest-environment jsdom
/**
 * The ring's browser parts: the tone is made with Web Audio, starts only
 * after the switch's click, beeps every second and stops, and the
 * notification goes only when the owner allowed it.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { askToNotify, notify, webAudioTone } from "../ring";

let oscillators: { frequency: number; started: number; stopped: number }[];
let resumed: number;

class FakeAudioContext {
  currentTime = 10;
  destination = {};
  resume = vi.fn(async () => {
    resumed++;
  });
  createOscillator() {
    const record = { frequency: 0, started: 0, stopped: 0 };
    oscillators.push(record);
    return {
      type: "",
      frequency: {
        set value(v: number) {
          record.frequency = v;
        },
      },
      connect: (node: unknown) => node,
      start: (at: number) => {
        record.started = at;
      },
      stop: (at: number) => {
        record.stopped = at;
      },
    };
  }
  createGain() {
    return { gain: { value: 0 }, connect: (node: unknown) => node };
  }
}

beforeEach(() => {
  vi.useFakeTimers();
  oscillators = [];
  resumed = 0;
  vi.stubGlobal("AudioContext", FakeAudioContext);
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("the tone", () => {
  it("beeps 880 Hz then 660 Hz every second once unlocked, and stops", () => {
    const tone = webAudioTone();
    tone.unlock();
    tone.start();

    expect(oscillators.map((o) => [o.frequency, o.started, o.stopped])).toEqual(
      [
        [880, 10, 10.25],
        [660, 10.25, 10.5],
      ],
    );
    vi.advanceTimersByTime(3000);
    expect(oscillators).toHaveLength(8);

    tone.stop();
    vi.advanceTimersByTime(3000);
    expect(oscillators).toHaveLength(8);
    expect(resumed).toBeGreaterThan(0);
  });

  it("makes no sound before the click unlocks it, and never throws", () => {
    const tone = webAudioTone();
    tone.start();
    vi.advanceTimersByTime(2000);
    tone.stop();

    expect(oscillators).toHaveLength(0);
  });

  it("does nothing where the browser has no Web Audio", () => {
    vi.stubGlobal("AudioContext", undefined);
    const tone = webAudioTone();

    expect(() => {
      tone.unlock();
      tone.start();
      tone.stop();
    }).not.toThrow();
  });
});

describe("the notification", () => {
  it("goes only with permission, and asking happens only while undecided", () => {
    const shown: string[] = [];
    const requestPermission = vi.fn(async () => "granted");
    const Fake = Object.assign(
      function (this: unknown, title: string) {
        shown.push(title);
      },
      { permission: "default", requestPermission },
    );
    vi.stubGlobal("Notification", Fake);

    notify("Ringing: Standup", "Starts 9:00", "k");
    askToNotify();
    Fake.permission = "granted";
    askToNotify();
    notify("Ringing: Standup", "Starts 9:00", "k");

    expect(shown).toEqual(["Ringing: Standup"]);
    expect(requestPermission).toHaveBeenCalledTimes(1);
  });

  it("is skipped where the browser has none", () => {
    vi.stubGlobal("Notification", undefined);
    // jsdom's window still has the property: remove it.
    Reflect.deleteProperty(window, "Notification");

    expect(() => {
      notify("a", "b", "c");
      askToNotify();
    }).not.toThrow();
  });
});
