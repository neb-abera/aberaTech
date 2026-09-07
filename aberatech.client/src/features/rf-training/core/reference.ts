/**
 * The reference cards and the plan template, as data.
 *
 * Cards are what to carry until the drill makes them unnecessary: the
 * formulas, the decibel table, the antenna lengths for the plan's bands,
 * the phonetic alphabet, the prowords, and the Zulu offsets. The template
 * is the communications plan the field block requires, as headings and
 * the questions each one must answer.
 */

import { zones } from "./drills";

export interface Formula {
  name: string;
  formula: string;
  note: string;
}

export const formulas: Formula[] = [
  {
    name: "Wavelength",
    formula: "λ (m) = 300 / f (MHz)",
    note: "Feet: 984 / f.",
  },
  {
    name: "Half-wave dipole",
    formula: "L (ft) = 468 / f (MHz)",
    note: "Each leg 234 / f. Cut long, trim to the analyzer.",
  },
  {
    name: "Off-centre feed point",
    formula: "0.14 × (468 / f)",
    note: "From the centre. Tenths of a foot × 12 = inches.",
  },
  {
    name: "Quarter-wave vertical",
    formula: "L (ft) = 234 / f (MHz)",
    note: "Needs radials or a counterpoise.",
  },
  {
    name: "Decibels",
    formula: "dB = 10 log10 (P2 / P1)",
    note: "3 dB doubles, 10 dB is ten times, 20 dB is a hundred.",
  },
  {
    name: "dBm",
    formula: "mW = 10^(dBm / 10)",
    note: "0 dBm is 1 mW, 30 dBm is 1 W, 37 dBm is 5 W.",
  },
  {
    name: "Ohm's law",
    formula: "V = I R, P = V I = V² / R",
    note: "",
  },
  {
    name: "Battery",
    formula: "Ah = A × h; h = Ah / A",
    note: "Keep a third in reserve for cold and cloud.",
  },
  {
    name: "Back azimuth",
    formula: "az ± 180",
    note: "Add if under 180, subtract otherwise.",
  },
  {
    name: "Broadside wire",
    formula: "az ± 90",
    note: "The wire runs at right angles to the bearing.",
  },
];

export const decibels: { db: number; ratio: string }[] = [
  { db: 1, ratio: "1.26" },
  { db: 3, ratio: "2" },
  { db: 6, ratio: "4" },
  { db: 10, ratio: "10" },
  { db: 13, ratio: "20" },
  { db: 20, ratio: "100" },
  { db: 30, ratio: "1 000" },
];

/** Half-wave lengths for the bands the plan puts the reader on. */
export const bandLengths = [1.9, 3.6, 7.1, 10.1, 14.2, 18.1, 21.3, 28.5].map(
  (mhz) => ({
    mhz,
    metres: 300 / mhz,
    halfWaveFeet: 468 / mhz,
    legFeet: 234 / mhz,
    ocfFeet: 0.14 * (468 / mhz),
  }),
);

export const phonetic: [string, string][] = [
  ["A", "Alfa"],
  ["B", "Bravo"],
  ["C", "Charlie"],
  ["D", "Delta"],
  ["E", "Echo"],
  ["F", "Foxtrot"],
  ["G", "Golf"],
  ["H", "Hotel"],
  ["I", "India"],
  ["J", "Juliett"],
  ["K", "Kilo"],
  ["L", "Lima"],
  ["M", "Mike"],
  ["N", "November"],
  ["O", "Oscar"],
  ["P", "Papa"],
  ["Q", "Quebec"],
  ["R", "Romeo"],
  ["S", "Sierra"],
  ["T", "Tango"],
  ["U", "Uniform"],
  ["V", "Victor"],
  ["W", "Whiskey"],
  ["X", "X-ray"],
  ["Y", "Yankee"],
  ["Z", "Zulu"],
];

export const prowords: [string, string][] = [
  ["THIS IS", "The transmission is from the station whose call follows."],
  ["OVER", "My transmission is ended and I expect a reply."],
  ["OUT", "My transmission is ended and no reply is expected."],
  ["ROGER", "I have received your last transmission satisfactorily."],
  ["WILCO", "Received, understood, and will comply. Never with ROGER."],
  [
    "SAY AGAIN",
    "Repeat all, or the part after ALL AFTER or before ALL BEFORE.",
  ],
  ["I SAY AGAIN", "I am repeating the transmission or the part indicated."],
  ["I SPELL", "I shall spell the next word phonetically."],
  ["FIGURES", "Numerals follow."],
  ["WORDS TWICE", "Communication is difficult; send every phrase twice."],
  ["CORRECTION", "An error has been made; the correct version follows."],
  ["WAIT", "I must pause for a few seconds."],
  ["WAIT OUT", "I must pause longer than a few seconds; I will call you."],
  ["READ BACK", "Repeat this entire transmission back to me exactly."],
  ["I READ BACK", "The following is my reply to your read back request."],
  ["RADIO CHECK", "What is my signal strength and readability?"],
  ["UNKNOWN STATION", "The identity of the station I am calling is unknown."],
  ["BREAK", "I separate the text from the rest of the message."],
];

export const zuluOffsets = zones;

export interface TemplateSection {
  title: string;
  /** The questions the section must answer. */
  prompts: string[];
}

/** The communications plan, as the field block requires it. */
export const planTemplate: TemplateSection[] = [
  {
    title: "Situation and task",
    prompts: [
      "Who talks to whom, from where, for how long?",
      "What must get through even if everything else fails?",
      "Grid and azimuth to every distant station.",
    ],
  },
  {
    title: "Means, for every link",
    prompts: [
      "Primary: the means, frequency or channel, antenna, and the hours it is expected to work.",
      "Alternate: the second means, ready without repacking.",
      "Contingency: what you fall back to when both are out, and who decides.",
      "Emergency: the last resort, and the signal that says you are on it.",
    ],
  },
  {
    title: "Net diagram",
    prompts: [
      "Every station, every link between them, and the means on each link.",
      "Which station controls each net.",
    ],
  },
  {
    title: "Schedule",
    prompts: [
      "Contact windows in Zulu, with the local time beside each.",
      "Frequencies by time of day, from the propagation prediction, with the fallback if the band is dead.",
      "What happens after a missed window: how long to wait, what to try next.",
    ],
  },
  {
    title: "Procedures",
    prompts: [
      "Call signs, authentication method, and how a message is formatted.",
      "What is sent in the clear and what is not.",
      "Radio checks: when, and what counts as good enough to proceed.",
    ],
  },
  {
    title: "Site",
    prompts: [
      "Ground wave or sky wave, and the site chosen for it from the map.",
      "Antenna, orientation and height at each site, with the space it needs.",
      "What is left behind: nothing.",
    ],
  },
  {
    title: "Power",
    prompts: [
      "Every load in amps, the hours it runs, and the amp-hours that adds up to.",
      "Batteries carried, with the reserve, and how they are recharged.",
      "The point at which the plan degrades to save power, and what is cut first.",
    ],
  },
  {
    title: "After",
    prompts: [
      "What failed, why, and the one thing that changes before the next exercise.",
    ],
  },
];

export const templateStandard =
  "From a written scenario to a finished plan on this template inside four hours. The plan is finished when a second person could run the exercise from it without asking you anything.";
