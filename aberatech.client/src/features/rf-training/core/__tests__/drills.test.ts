/**
 * The drill against its own formulas: every generated answer must be what
 * the textbook says, a seed must give the same session twice, and the
 * checker must accept a rounded right answer and reject a nearly right
 * subnet.
 */
import { describe, expect, it } from "vitest";
import {
  azimuthProblem,
  dbProblem,
  dipoleProblem,
  feetAndInches,
  isCorrect,
  kinds,
  makeSession,
  ocfProblem,
  ohmProblem,
  padApply,
  padProblem,
  powerProblem,
  prefixForHosts,
  score,
  seededRandom,
  seedForDate,
  subnetOf,
  subnetProblem,
  wavelengthProblem,
  zuluProblem,
} from "../drills";

/** Within one percent: prompts show rounded values, answers are exact. */
const within1pct = (value: number, expected: number) =>
  expect(Math.abs(value - expected) / Math.abs(expected)).toBeLessThan(0.01);

describe("seededRandom", () => {
  it("is deterministic and stays in [0, 1)", () => {
    const a = seededRandom(42);
    const b = seededRandom(42);
    for (let i = 0; i < 100; i++) {
      const value = a();
      expect(value).toBe(b());
      expect(value).toBeGreaterThanOrEqual(0);
      expect(value).toBeLessThan(1);
    }
  });
});

describe("subnet arithmetic", () => {
  it("finds the network, broadcast, mask and host count", () => {
    expect(subnetOf("192.168.1.130", 26)).toEqual({
      network: "192.168.1.128",
      broadcast: "192.168.1.191",
      mask: "255.255.255.192",
      usable: 62,
    });
    expect(subnetOf("10.20.30.40", 20)).toEqual({
      network: "10.20.16.0",
      broadcast: "10.20.31.255",
      mask: "255.255.240.0",
      usable: 4094,
    });
    expect(subnetOf("172.16.5.9", 30).usable).toBe(2);
  });

  it("picks the longest prefix that fits the hosts", () => {
    expect(prefixForHosts(2)).toBe(30);
    expect(prefixForHosts(6)).toBe(29);
    expect(prefixForHosts(100)).toBe(25);
    expect(prefixForHosts(126)).toBe(25);
    expect(prefixForHosts(127)).toBe(24);
    expect(prefixForHosts(1000)).toBe(22);
  });
});

describe("generators", () => {
  // Many seeds, so every branch of every generator is exercised.
  const seeds = Array.from({ length: 60 }, (_, i) => i + 1);

  it.each(seeds)("db problems match 10 log10 (seed %i)", (seed) => {
    const problem = dbProblem(seededRandom(seed), "p");
    const value = Number(problem.answer);
    expect(Number.isFinite(value)).toBe(true);
    if (problem.prompt.includes("dBm")) {
      const dbm = Number(problem.prompt.match(/^(\d+) dBm/)?.[1]);
      expect(value).toBeCloseTo(10 ** (dbm / 10), -1);
    } else if (problem.prompt.startsWith("A power gain")) {
      const db = Number(problem.prompt.match(/of (\d+) dB/)?.[1]);
      expect(value).toBeCloseTo(10 ** (db / 10), 0);
    } else {
      const ratio = Number(problem.prompt.match(/ratio of (\d+)/)?.[1]);
      expect(value).toBeCloseTo(10 * Math.log10(ratio), 1);
    }
  });

  it.each(seeds)("wavelength problems use 300 / f (seed %i)", (seed) => {
    const problem = wavelengthProblem(seededRandom(seed), "p");
    const value = Number(problem.answer);
    if (problem.unit === "m") {
      const f = Number(problem.prompt.match(/of ([\d.]+) MHz/)?.[1]);
      within1pct(value, 300 / f);
    } else {
      const metres = Number(problem.prompt.match(/of ([\d.]+) m/)?.[1]);
      within1pct(value, 300 / metres);
    }
  });

  it.each(seeds)("dipole problems use 468 or 234 over f (seed %i)", (seed) => {
    const problem = dipoleProblem(seededRandom(seed), "p");
    const f = Number(problem.prompt.match(/for ([\d.]+) MHz/)?.[1]);
    const constant = problem.prompt.startsWith("Each leg") ? 234 : 468;
    expect(Number(problem.answer)).toBeCloseTo(constant / f, 0);
    expect(f).toBeLessThan(60);
  });

  it.each(seeds)("ohm problems obey V = IR and P = VI (seed %i)", (seed) => {
    const problem = ohmProblem(seededRandom(seed), "p");
    const value = Number(problem.answer);
    const volts = Number(problem.prompt.match(/([\d.]+) V/)?.[1]);
    if (problem.unit === "A") {
      const ohms = Number(problem.prompt.match(/across (\d+) Ω/)?.[1]);
      within1pct(value, volts / ohms);
    } else if (problem.unit === "W") {
      const ohms = Number(problem.prompt.match(/across (\d+) Ω/)?.[1]);
      within1pct(value, (volts * volts) / ohms);
    } else {
      const amps = Number(problem.prompt.match(/drawing ([\d.]+) A/)?.[1]);
      within1pct(value, volts / amps);
    }
  });

  it.each(seeds)("subnet problems agree with subnetOf (seed %i)", (seed) => {
    const problem = subnetProblem(seededRandom(seed), "p");
    if (problem.prompt.startsWith("What is the longest prefix")) {
      const hosts = Number(problem.prompt.match(/fits (\d+) usable/)?.[1]);
      expect(problem.answer).toBe(`/${prefixForHosts(hosts)}`);
      return;
    }
    if (problem.prompt.startsWith("What is the subnet mask")) {
      const prefix = Number(problem.prompt.match(/\/(\d+)/)?.[1]);
      expect(problem.answer).toBe(subnetOf("10.0.0.1", prefix).mask);
      return;
    }
    const [, ip, prefix] = problem.prompt.match(/([\d.]+)\/(\d+)/) ?? [];
    const subnet = subnetOf(ip, Number(prefix));
    if (problem.prompt.includes("network address"))
      expect(problem.answer).toBe(subnet.network);
    else if (problem.prompt.includes("broadcast address"))
      expect(problem.answer).toBe(subnet.broadcast);
    else expect(problem.answer).toBe(String(subnet.usable));
  });
});

describe("the new generators", () => {
  const seeds = Array.from({ length: 60 }, (_, i) => i + 1);

  it("writes feet and inches the way a tape reads", () => {
    expect(feetAndInches(16.38)).toBe("16 ft 5 in");
    expect(feetAndInches(58.5)).toBe("58 ft 6 in");
    expect(feetAndInches(9.99)).toBe("10 ft 0 in");
  });

  it.each(seeds)(
    "off-centre feed problems use 0.14 × 468 / f (seed %i)",
    (seed) => {
      const problem = ocfProblem(seededRandom(seed), "p");
      const value = Number(problem.answer);
      if (problem.unit === "ft") {
        const f = Number(problem.prompt.match(/for ([\d.]+) MHz/)?.[1]);
        within1pct(value, (0.14 * 468) / f);
      } else {
        const tenths = Number(problem.prompt.match(/^([\d.]+) of a foot/)?.[1]);
        within1pct(value, tenths * 12);
      }
    },
  );

  it.each(seeds)("power problems add up (seed %i)", (seed) => {
    const problem = powerProblem(seededRandom(seed), "p");
    const value = Number(problem.answer);
    if (problem.unit === "W") {
      const amps = [...problem.prompt.matchAll(/at ([\d.]+) A/g)].reduce(
        (sum, m) => sum + Number(m[1]),
        0,
      );
      within1pct(value, amps * 12);
    } else if (problem.unit === "Ah") {
      const [, amps, hours] =
        problem.prompt.match(/([\d.]+) A for (\d+) hours/) ?? [];
      within1pct(value, Number(amps) * Number(hours));
    } else {
      const [, capacity, amps] =
        problem.prompt.match(/A (\d+) Ah battery feeds a steady ([\d.]+) A/) ??
        [];
      within1pct(value, Number(capacity) / Number(amps));
    }
  });

  it.each(seeds)("azimuth problems stay on the compass (seed %i)", (seed) => {
    const problem = azimuthProblem(seededRandom(seed), "p");
    const azimuth = Number(problem.prompt.match(/(\d+)°/)?.[1]);
    const value = Number(problem.answer);
    expect(value).toBeGreaterThanOrEqual(0);
    expect(value).toBeLessThan(360);
    if (problem.prompt.includes("back azimuth")) {
      expect(value).toBe((azimuth + 180) % 360);
    } else {
      expect([(azimuth + 90) % 360, (azimuth + 270) % 360]).toContain(value);
      expect(value).toBe(Math.min((azimuth + 90) % 360, (azimuth + 270) % 360));
    }
  });

  it.each(seeds)("zulu problems convert both ways (seed %i)", (seed) => {
    const problem = zuluProblem(seededRandom(seed), "p");
    expect(problem.answer).toMatch(/^([01]\d|2[0-3])[0-5]\d$/);
    const offset = Number(problem.prompt.match(/UTC(-\d+)/)?.[1]);
    const shown = problem.prompt.match(/(\d{4})(Z| )/)?.[1] ?? "";
    const minutes = (t: string) =>
      Number(t.slice(0, 2)) * 60 + Number(t.slice(2));
    const wrap = (m: number) => ((m % 1440) + 1440) % 1440;
    const expected = problem.prompt.includes("What is that in Zulu")
      ? wrap(minutes(shown) - offset * 60)
      : wrap(minutes(shown) + offset * 60);
    expect(minutes(problem.answer)).toBe(expected);
  });

  it("applies a pad and takes it off again", () => {
    expect(padApply("HELLO", "XMCKL", 1)).toBe("EQNVZ");
    expect(padApply("EQNVZ", "XMCKL", -1)).toBe("HELLO");
  });

  it.each(seeds)("pad problems round-trip (seed %i)", (seed) => {
    const problem = padProblem(seededRandom(seed), "p");
    const [, group, key] =
      problem.prompt.match(/group ([A-Z]{5}) with the pad group ([A-Z]{5})/) ??
      [];
    if (problem.prompt.startsWith("Encode")) {
      expect(problem.answer).toBe(padApply(group, key, 1));
    } else {
      expect(padApply(problem.answer, key, 1)).toBe(group);
    }
  });

  it("accepts a Zulu answer with or without the Z, and degrees with the sign", () => {
    const zulu = {
      id: "p",
      kind: "zulu" as const,
      prompt: "",
      answer: "1930",
      tolerance: 0,
      explanation: "",
    };
    expect(isCorrect(zulu, "1930")).toBe(true);
    expect(isCorrect(zulu, "1930Z")).toBe(true);
    expect(isCorrect(zulu, "1930 z")).toBe(true);
    expect(isCorrect(zulu, "0730")).toBe(false);
    const az = { ...zulu, kind: "azimuth" as const, answer: "270" };
    expect(isCorrect(az, "270°")).toBe(true);
    expect(isCorrect(az, "90")).toBe(false);
    const pad = { ...zulu, kind: "pad" as const, answer: "RIVER" };
    expect(isCorrect(pad, "river")).toBe(true);
    expect(isCorrect(pad, "rivet")).toBe(false);
  });
});

describe("makeSession", () => {
  it("deals the kinds evenly and reproduces from its seed", () => {
    const a = makeSession(7, 20);
    const b = makeSession(7, 20);
    expect(a.problems.map((p) => p.prompt)).toEqual(
      b.problems.map((p) => p.prompt),
    );
    for (const kind of kinds) {
      expect(a.problems.filter((p) => p.kind === kind)).toHaveLength(2);
    }
    expect(new Set(a.problems.map((p) => p.id)).size).toBe(20);
  });

  it("differs between seeds", () => {
    expect(makeSession(1).problems.map((p) => p.prompt)).not.toEqual(
      makeSession(2).problems.map((p) => p.prompt),
    );
  });

  it("seeds from the calendar date, not the time", () => {
    const morning = new Date(2026, 8, 7, 6, 0);
    const night = new Date(2026, 8, 7, 23, 59);
    const tomorrow = new Date(2026, 8, 8, 0, 1);
    expect(seedForDate(morning)).toBe(seedForDate(night));
    expect(seedForDate(morning)).not.toBe(seedForDate(tomorrow));
    expect(seedForDate(morning)).toBe(20260907);
  });
});

describe("isCorrect", () => {
  const ratio = {
    id: "p",
    kind: "db" as const,
    prompt: "",
    answer: "20",
    tolerance: 0.05,
    explanation: "",
  };
  const prefix = {
    ...ratio,
    kind: "subnet" as const,
    answer: "/25",
    tolerance: 0,
  };
  const network = { ...prefix, answer: "192.168.1.128" };

  it("accepts a rounded number inside the tolerance, with or without a unit", () => {
    expect(isCorrect(ratio, "20")).toBe(true);
    expect(isCorrect(ratio, "19.95")).toBe(true);
    expect(isCorrect(ratio, " 20 mW ")).toBe(true);
    expect(isCorrect(ratio, "21.5")).toBe(false);
    expect(isCorrect(ratio, "")).toBe(false);
    expect(isCorrect(ratio, "twenty")).toBe(false);
  });

  it("checks addresses exactly and prefixes with or without the slash", () => {
    expect(isCorrect(network, "192.168.1.128")).toBe(true);
    expect(isCorrect(network, "192.168.1.129")).toBe(false);
    expect(isCorrect(prefix, "/25")).toBe(true);
    expect(isCorrect(prefix, "25")).toBe(true);
    expect(isCorrect(prefix, "/24")).toBe(false);
  });
});

describe("score", () => {
  it("counts the right answers overall and by kind", () => {
    const session = makeSession(3, 10);
    const answers = session.problems.map((p, i) =>
      i % 2 === 0 ? p.answer : "wrong",
    );
    const result = score(
      session,
      answers,
      125,
      new Date("2026-09-07T12:00:00Z"),
    );
    expect(result.total).toBe(10);
    expect(result.correct).toBe(5);
    expect(result.seconds).toBe(125);
    expect(result.at).toBe("2026-09-07T12:00:00.000Z");
    const totals = Object.values(result.byKind).reduce(
      (sum, k) => sum + k.total,
      0,
    );
    expect(totals).toBe(10);
  });
});
