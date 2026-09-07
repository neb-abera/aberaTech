/**
 * The daily drill: fresh arithmetic across the plan, generated from a seed.
 *
 * Ten kinds, one per thing the plan says must become a reflex: decibels,
 * wavelength, dipole length, the off-centre feed point, Ohm's law, power
 * budgets, azimuths, Zulu time, one-time pads, and subnetting. Every
 * generator is a pure function of a random source, so a session is
 * reproducible from its seed and every answer can be checked against the
 * formula in a test.
 *
 * Answers are strings. Numeric ones are checked within a tolerance, because
 * "20" is the right answer to "13 dB as a ratio" even though the exact value
 * is 19.95; addresses and masks are checked exactly, because a subnet that
 * is nearly right is wrong.
 */

export type DrillKind =
  | "db"
  | "wavelength"
  | "dipole"
  | "ocf"
  | "ohm"
  | "power"
  | "azimuth"
  | "zulu"
  | "pad"
  | "subnet";

export const kinds: DrillKind[] = [
  "db",
  "wavelength",
  "dipole",
  "ocf",
  "ohm",
  "power",
  "azimuth",
  "zulu",
  "pad",
  "subnet",
];

export const kindLabel: Record<DrillKind, string> = {
  db: "Decibels",
  wavelength: "Wavelength",
  dipole: "Dipole length",
  ocf: "Off-centre feed",
  ohm: "Ohm's law",
  power: "Power budget",
  azimuth: "Azimuth",
  zulu: "Zulu time",
  pad: "One-time pad",
  subnet: "Subnetting",
};

export interface Problem {
  id: string;
  kind: DrillKind;
  prompt: string;
  /** The exact answer, as the checker compares it. */
  answer: string;
  unit?: string;
  /** Fraction of the answer allowed either side; 0 means exact. */
  tolerance: number;
  /** The working, shown after an answer. */
  explanation: string;
}

export interface Session {
  seed: number;
  problems: Problem[];
}

export interface Result {
  seed: number;
  /** ISO date-time when the session finished. */
  at: string;
  total: number;
  correct: number;
  seconds: number;
  byKind: Record<DrillKind, { total: number; correct: number }>;
}

export type Random = () => number;

/** mulberry32: small, fast, and good enough to shuffle drill problems. */
export function seededRandom(seed: number): Random {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const pick = <T>(random: Random, items: readonly T[]): T =>
  items[Math.floor(random() * items.length)];

const between = (random: Random, low: number, high: number): number =>
  low + Math.floor(random() * (high - low + 1));

/** Round to a sensible number of significant figures for display. */
const round = (value: number, places = 2): string =>
  String(Number(value.toFixed(places)));

// A gain in decibels is 10 log10 of the power ratio. The drill uses the
// values an operator meets: 3 dB is double, 10 dB is ten times, 20 is a
// hundred, and dBm is decibels relative to a milliwatt.
const dbValues = [3, 6, 10, 13, 20, 23, 30] as const;

export function dbProblem(random: Random, id: string): Problem {
  const form = between(random, 0, 2);
  if (form === 0) {
    const db = pick(random, dbValues);
    const ratio = 10 ** (db / 10);
    return {
      id,
      kind: "db",
      prompt: `A power gain of ${db} dB is a ratio of how much?`,
      answer: round(ratio, 1),
      tolerance: 0.05,
      explanation: `Ratio = 10^(${db} / 10) = ${round(ratio, 1)}.`,
    };
  }
  if (form === 1) {
    const ratio = pick(random, [2, 4, 10, 20, 100, 200, 1000] as const);
    const db = 10 * Math.log10(ratio);
    return {
      id,
      kind: "db",
      prompt: `A power ratio of ${ratio} is how many decibels?`,
      answer: round(db, 1),
      unit: "dB",
      tolerance: 0.05,
      explanation: `dB = 10 log10(${ratio}) = ${round(db, 1)}.`,
    };
  }
  const dbm = pick(random, [0, 10, 20, 27, 30, 33, 37, 40] as const);
  const mw = 10 ** (dbm / 10);
  return {
    id,
    kind: "db",
    prompt: `${dbm} dBm is how many milliwatts?`,
    answer: round(mw, 0),
    unit: "mW",
    tolerance: 0.05,
    explanation: `mW = 10^(${dbm} / 10) = ${round(mw, 0)}. Every 3 dB doubles it; every 10 dB is ten times.`,
  };
}

// Wavelength in metres is 300 divided by the frequency in megahertz. The
// frequencies are the middles of the bands the plan has the reader on.
const bandsMHz = [
  1.9, 3.6, 7.1, 10.1, 14.2, 18.1, 21.3, 28.5, 50.1, 146, 446,
] as const;

export function wavelengthProblem(random: Random, id: string): Problem {
  const f = pick(random, bandsMHz);
  const metres = 300 / f;
  if (random() < 0.5) {
    return {
      id,
      kind: "wavelength",
      prompt: `What is the wavelength of ${f} MHz, in metres?`,
      answer: round(metres, 2),
      unit: "m",
      tolerance: 0.02,
      explanation: `λ = 300 / ${f} = ${round(metres, 2)} m.`,
    };
  }
  return {
    id,
    kind: "wavelength",
    prompt: `A wavelength of ${round(metres, 2)} m is what frequency, in megahertz?`,
    answer: round(f, 2),
    unit: "MHz",
    tolerance: 0.02,
    explanation: `f = 300 / ${round(metres, 2)} = ${round(f, 2)} MHz.`,
  };
}

// A half-wave dipole is 468 / f(MHz) feet end to end, 234 / f per leg.
export function dipoleProblem(random: Random, id: string): Problem {
  const f = pick(
    random,
    bandsMHz.filter((band) => band < 60),
  );
  const perLeg = random() < 0.5;
  const feet = (perLeg ? 234 : 468) / f;
  return {
    id,
    kind: "dipole",
    prompt: perLeg
      ? `Each leg of a half-wave dipole for ${f} MHz is how long, in feet?`
      : `A half-wave dipole for ${f} MHz is how long end to end, in feet?`,
    answer: round(feet, 1),
    unit: "ft",
    tolerance: 0.02,
    explanation: `${perLeg ? 234 : 468} / ${f} = ${round(feet, 1)} ft. Cut long and trim to the analyzer.`,
  };
}

// V = IR and P = VI, with the values found on a field power budget.
export function ohmProblem(random: Random, id: string): Problem {
  const volts = pick(random, [6, 12, 13.8, 24, 48] as const);
  const ohms = pick(random, [2, 4, 8, 12, 24, 50, 100] as const);
  const amps = volts / ohms;
  const watts = volts * amps;
  const form = between(random, 0, 2);
  if (form === 0) {
    return {
      id,
      kind: "ohm",
      prompt: `${volts} V across ${ohms} Ω draws how many amps?`,
      answer: round(amps, 3),
      unit: "A",
      tolerance: 0.02,
      explanation: `I = V / R = ${volts} / ${ohms} = ${round(amps, 3)} A.`,
    };
  }
  if (form === 1) {
    return {
      id,
      kind: "ohm",
      prompt: `${volts} V across ${ohms} Ω dissipates how many watts?`,
      answer: round(watts, 2),
      unit: "W",
      tolerance: 0.02,
      explanation: `P = V² / R = ${volts}² / ${ohms} = ${round(watts, 2)} W.`,
    };
  }
  return {
    id,
    kind: "ohm",
    prompt: `A load drawing ${round(amps, 3)} A at ${volts} V has what resistance, in ohms?`,
    answer: round(ohms, 2),
    unit: "Ω",
    tolerance: 0.02,
    explanation: `R = V / I = ${volts} / ${round(amps, 3)} = ${ohms} Ω.`,
  };
}

/** Feet with a decimal to feet and inches, the way a tape measure reads. */
export function feetAndInches(feet: number): string {
  const whole = Math.floor(feet);
  const inches = Math.round((feet - whole) * 12);
  if (inches === 12) return `${whole + 1} ft 0 in`;
  return `${whole} ft ${inches} in`;
}

// The off-centre-fed dipole: a half-wave of wire fed 14 percent from the
// middle. The drill asks for the feed point in feet and inches, because the
// arithmetic that fails in the field is the tenths-of-a-foot conversion.
export function ocfProblem(random: Random, id: string): Problem {
  const f = pick(
    random,
    bandsMHz.filter((band) => band < 30),
  );
  const length = 468 / f;
  const offset = 0.14 * length;
  if (random() < 0.5) {
    return {
      id,
      kind: "ocf",
      prompt: `A half-wave for ${f} MHz is fed 14 percent from its centre. How far from the centre is the feed point, in feet?`,
      answer: round(offset, 1),
      unit: "ft",
      tolerance: 0.02,
      explanation: `468 / ${f} = ${round(length, 1)} ft; 0.14 × ${round(length, 1)} = ${round(offset, 1)} ft, which is ${feetAndInches(offset)}.`,
    };
  }
  const tenths = Math.round((offset - Math.floor(offset)) * 10) / 10;
  return {
    id,
    kind: "ocf",
    prompt: `${tenths} of a foot is how many inches?`,
    answer: round(tenths * 12, 1),
    unit: "in",
    tolerance: 0.05,
    explanation: `Tenths of a foot × 12: ${tenths} × 12 = ${round(tenths * 12, 1)} in.`,
  };
}

// A power budget: loads to watts, watts and hours to amp-hours at 12 V,
// or a battery's amp-hours to hours of runtime.
const loads = [
  ["radio on receive", 0.6],
  ["radio on transmit, one third duty", 4],
  ["mesh node", 0.15],
  ["phone charging", 1],
  ["laptop", 3.5],
  ["router and modem", 1.2],
  ["drone battery charger", 5],
] as const;

export function powerProblem(random: Random, id: string): Problem {
  const form = between(random, 0, 2);
  const volts = 12;
  if (form === 0) {
    const count = between(random, 2, 4);
    const chosen: (typeof loads)[number][] = [];
    while (chosen.length < count) {
      const load = pick(random, loads);
      if (!chosen.includes(load)) chosen.push(load);
    }
    const amps = chosen.reduce((sum, [, a]) => sum + a, 0);
    const watts = amps * volts;
    const list = chosen.map(([name, a]) => `${name} at ${a} A`).join(", ");
    return {
      id,
      kind: "power",
      prompt: `At ${volts} V, these loads run together: ${list}. What is the total draw, in watts?`,
      answer: round(watts, 1),
      unit: "W",
      tolerance: 0.02,
      explanation: `${round(amps, 2)} A × ${volts} V = ${round(watts, 1)} W.`,
    };
  }
  if (form === 1) {
    const amps = pick(random, [0.5, 1, 1.5, 2, 2.5, 3] as const);
    const hours = pick(random, [12, 24, 48, 72] as const);
    return {
      id,
      kind: "power",
      prompt: `A kit averages ${amps} A for ${hours} hours. How many amp-hours does it need, before any reserve?`,
      answer: round(amps * hours, 1),
      unit: "Ah",
      tolerance: 0.02,
      explanation: `${amps} A × ${hours} h = ${round(amps * hours, 1)} Ah. Add reserve for cold and for the day the sun does not come out.`,
    };
  }
  const capacity = pick(random, [7, 12, 20, 50, 100] as const);
  const amps = pick(random, [0.5, 1, 2, 2.5] as const);
  return {
    id,
    kind: "power",
    prompt: `A ${capacity} Ah battery feeds a steady ${amps} A load. How many hours does it last, using all of it?`,
    answer: round(capacity / amps, 1),
    unit: "h",
    tolerance: 0.02,
    explanation: `${capacity} Ah / ${amps} A = ${round(capacity / amps, 1)} h.`,
  };
}

// Azimuths: the back azimuth for a path, and the line a broadside wire
// runs along to face a station. Degrees, 0 to 359, exact.
export function azimuthProblem(random: Random, id: string): Problem {
  const azimuth = between(random, 0, 359);
  if (random() < 0.5) {
    const back = (azimuth + 180) % 360;
    return {
      id,
      kind: "azimuth",
      prompt: `The distant station bears ${azimuth}°. What is the back azimuth, in degrees?`,
      answer: String(back),
      unit: "°",
      tolerance: 0,
      explanation: `Add 180 if under 180, subtract 180 otherwise: ${azimuth} → ${back}.`,
    };
  }
  const low = (azimuth + 90) % 360;
  const high = (azimuth + 270) % 360;
  const answer = String(Math.min(low, high));
  return {
    id,
    kind: "azimuth",
    prompt: `A wire antenna must sit broadside to a station at ${azimuth}°. Along which azimuth does the wire run? Give the smaller of the two, in degrees.`,
    answer,
    unit: "°",
    tolerance: 0,
    explanation: `Broadside means the wire runs at right angles to the bearing: ${azimuth} ± 90 gives ${Math.min(low, high)} and ${Math.max(low, high)}.`,
  };
}

// Zulu time. Zones the reader lives and drives in, standard and daylight.
export const zones = [
  ["Eastern Standard", -5],
  ["Eastern Daylight", -4],
  ["Central Standard", -6],
  ["Central Daylight", -5],
  ["Mountain Standard", -7],
  ["Mountain Daylight", -6],
  ["Pacific Standard", -8],
  ["Pacific Daylight", -7],
] as const;

const hhmm = (minutes: number): string => {
  const m = ((minutes % 1440) + 1440) % 1440;
  return `${String(Math.floor(m / 60)).padStart(2, "0")}${String(m % 60).padStart(2, "0")}`;
};

export function zuluProblem(random: Random, id: string): Problem {
  const [zone, offset] = pick(random, zones);
  const local =
    between(random, 0, 23) * 60 + pick(random, [0, 15, 30, 45] as const);
  if (random() < 0.5) {
    const zulu = local - offset * 60;
    return {
      id,
      kind: "zulu",
      prompt: `It is ${hhmm(local)} ${zone} time (UTC${offset}). What is that in Zulu, as four digits?`,
      answer: hhmm(zulu),
      unit: "Z",
      tolerance: 0,
      explanation: `Zulu is local minus the offset: ${hhmm(local)} − (${offset}) h = ${hhmm(zulu)}Z${zulu >= 1440 ? ", the next day" : zulu < 0 ? ", the day before" : ""}.`,
    };
  }
  const back = local + offset * 60;
  return {
    id,
    kind: "zulu",
    prompt: `A contact window is ${hhmm(local)}Z. What is that in ${zone} time (UTC${offset}), as four digits?`,
    answer: hhmm(back),
    tolerance: 0,
    explanation: `Local is Zulu plus the offset: ${hhmm(local)} + (${offset}) h = ${hhmm(back)}${back < 0 ? ", the day before" : back >= 1440 ? ", the next day" : ""}.`,
  };
}

// A one-time pad on letters: cipher = plain + key, plain = cipher − key,
// both mod 26 with A as 0. Groups of five, exact.
const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

export function padApply(text: string, key: string, direction: 1 | -1): string {
  return [...text.toUpperCase()]
    .map((letter, i) => {
      const p = alphabet.indexOf(letter);
      const k = alphabet.indexOf(key[i % key.length].toUpperCase());
      if (p < 0 || k < 0) return letter;
      return alphabet[(((p + direction * k) % 26) + 26) % 26];
    })
    .join("");
}

const padWords = [
  "RIVER",
  "NORTH",
  "READY",
  "MOVED",
  "CLEAR",
  "WATER",
  "RELAY",
  "TOWER",
] as const;

export function padProblem(random: Random, id: string): Problem {
  const plain = pick(random, padWords);
  const key = Array.from(
    { length: 5 },
    () => alphabet[between(random, 0, 25)],
  ).join("");
  const cipher = padApply(plain, key, 1);
  if (random() < 0.5) {
    return {
      id,
      kind: "pad",
      prompt: `Encode the group ${plain} with the pad group ${key}, adding letter values with A as 0 and wrapping at Z.`,
      answer: cipher,
      tolerance: 0,
      explanation: `Letter by letter, plain + key mod 26: ${plain} + ${key} = ${cipher}.`,
    };
  }
  return {
    id,
    kind: "pad",
    prompt: `Decode the group ${cipher} with the pad group ${key}, subtracting letter values with A as 0 and wrapping below A.`,
    answer: plain,
    tolerance: 0,
    explanation: `Letter by letter, cipher − key mod 26: ${cipher} − ${key} = ${plain}.`,
  };
}

// Subnetting, both directions, checked exactly.
const toInt = (octets: number[]): number =>
  ((octets[0] << 24) | (octets[1] << 16) | (octets[2] << 8) | octets[3]) >>> 0;

const toDotted = (value: number): string =>
  [24, 16, 8, 0].map((shift) => (value >>> shift) & 255).join(".");

export function maskFor(prefix: number): number {
  return prefix === 0 ? 0 : (0xffffffff << (32 - prefix)) >>> 0;
}

export function subnetOf(ip: string, prefix: number) {
  const value = toInt(ip.split(".").map(Number));
  const mask = maskFor(prefix);
  const network = (value & mask) >>> 0;
  const broadcast = (network | (~mask >>> 0)) >>> 0;
  const usable = prefix >= 31 ? 0 : 2 ** (32 - prefix) - 2;
  return {
    network: toDotted(network),
    broadcast: toDotted(broadcast),
    mask: toDotted(mask),
    usable,
  };
}

/** The longest prefix whose block still leaves `hosts` usable addresses. */
export function prefixForHosts(hosts: number): number {
  let prefix = 30;
  while (2 ** (32 - prefix) - 2 < hosts) prefix -= 1;
  return prefix;
}

export function subnetProblem(random: Random, id: string): Problem {
  const form = between(random, 0, 4);
  if (form === 4) {
    const hosts = pick(random, [
      2, 6, 12, 25, 50, 100, 120, 200, 250, 500, 1000,
    ] as const);
    const prefix = prefixForHosts(hosts);
    return {
      id,
      kind: "subnet",
      prompt: `What is the longest prefix that still fits ${hosts} usable hosts? Answer as /N.`,
      answer: `/${prefix}`,
      tolerance: 0,
      explanation: `/${prefix} gives 2^${32 - prefix} − 2 = ${2 ** (32 - prefix) - 2} usable addresses; /${prefix + 1} gives ${2 ** (31 - prefix) - 2}, too few.`,
    };
  }
  const first = pick(random, [10, 172, 192] as const);
  const second =
    first === 10
      ? between(random, 0, 255)
      : first === 172
        ? between(random, 16, 31)
        : 168;
  const ip = [
    first,
    second,
    between(random, 0, 255),
    between(random, 1, 254),
  ].join(".");
  const prefix = between(random, 20, 30);
  const subnet = subnetOf(ip, prefix);
  const forms = [
    {
      prompt: `What is the network address of ${ip}/${prefix}?`,
      answer: subnet.network,
      explanation: `Mask ${subnet.mask}; ${ip} AND mask = ${subnet.network}.`,
    },
    {
      prompt: `What is the broadcast address of ${ip}/${prefix}?`,
      answer: subnet.broadcast,
      explanation: `Network ${subnet.network}, block of ${2 ** (32 - prefix)}; the last address is ${subnet.broadcast}.`,
    },
    {
      prompt: `How many usable host addresses are in ${ip}/${prefix}?`,
      answer: String(subnet.usable),
      explanation: `2^(32 − ${prefix}) − 2 = ${subnet.usable}.`,
    },
    {
      prompt: `What is the subnet mask for /${prefix}, in dotted decimal?`,
      answer: subnet.mask,
      explanation: `${prefix} ones then ${32 - prefix} zeros: ${subnet.mask}.`,
    },
  ];
  const chosen = forms[form];
  return { id, kind: "subnet", tolerance: 0, ...chosen };
}

const generators: Record<DrillKind, (random: Random, id: string) => Problem> = {
  db: dbProblem,
  wavelength: wavelengthProblem,
  dipole: dipoleProblem,
  ocf: ocfProblem,
  ohm: ohmProblem,
  power: powerProblem,
  azimuth: azimuthProblem,
  zulu: zuluProblem,
  pad: padProblem,
  subnet: subnetProblem,
};

/**
 * A session of `count` problems, kinds dealt round-robin then shuffled, so
 * twenty problems are two of each and never three subnets in a row.
 */
export function makeSession(seed: number, count = 20): Session {
  const random = seededRandom(seed);
  const order: DrillKind[] = Array.from(
    { length: count },
    (_, i) => kinds[i % kinds.length],
  );
  for (let i = order.length - 1; i > 0; i--) {
    const j = Math.floor(random() * (i + 1));
    [order[i], order[j]] = [order[j], order[i]];
  }
  const problems = order.map((kind, i) =>
    generators[kind](random, `${seed}-${i}`),
  );
  return { seed, problems };
}

/** A seed for today's session: the same all day, different tomorrow. */
export function seedForDate(date: Date): number {
  return (
    date.getFullYear() * 10000 + (date.getMonth() + 1) * 100 + date.getDate()
  );
}

/**
 * Whether an answer is right. Numbers are parsed leniently ("19.9", "20",
 * "20 mW") and compared within the problem's tolerance; exact answers are
 * compared after trimming and lower-casing, and a bare "25" matches "/25".
 */
export function isCorrect(problem: Problem, input: string): boolean {
  const given = input.trim().toLowerCase();
  if (!given) return false;
  if (problem.tolerance === 0) {
    const want = problem.answer.toLowerCase();
    const bare = given.replace(/[z°\s]+$/g, "");
    return (
      given === want ||
      bare === want ||
      (want.startsWith("/") && `/${given}` === want)
    );
  }
  const number = Number.parseFloat(given.replace(/[^0-9.+-]/g, ""));
  if (!Number.isFinite(number)) return false;
  const want = Number(problem.answer);
  return Math.abs(number - want) <= Math.abs(want) * problem.tolerance;
}

export function score(
  session: Session,
  answers: string[],
  seconds: number,
  at: Date,
): Result {
  const byKind = Object.fromEntries(
    kinds.map((kind) => [kind, { total: 0, correct: 0 }]),
  ) as Result["byKind"];
  let correct = 0;
  session.problems.forEach((problem, i) => {
    const right = isCorrect(problem, answers[i] ?? "");
    byKind[problem.kind].total += 1;
    if (right) {
      byKind[problem.kind].correct += 1;
      correct += 1;
    }
  });
  return {
    seed: session.seed,
    at: at.toISOString(),
    total: session.problems.length,
    correct,
    seconds,
    byKind,
  };
}
