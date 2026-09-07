/**
 * The plan as a plan: addressable, linkable, gated, and on its own subject.
 *
 * The last of those is the reason this file exists. The page is public and
 * describes training that has a private purpose; these tests hold the copy
 * to the subject it names on the tin, so a later edit cannot drift into
 * naming what it is for. A word on the list fails the build, not a review.
 */
import { describe, expect, it } from "vitest";
import { allTasks, cadence, copy, gear, plan, rules } from "../plan";

const everything = JSON.stringify({ plan, cadence, gear, rules, copy });

describe("the plan", () => {
  it("has seven blocks, each with tasks, a gate and at least one resource", () => {
    expect(plan).toHaveLength(7);
    for (const block of plan) {
      expect(block.tasks.length, block.id).toBeGreaterThan(2);
      expect(block.gate.length, block.id).toBeGreaterThan(30);
      expect(block.resources.length, block.id).toBeGreaterThan(0);
    }
  });

  it("gives every block and every task an id that is unique across the plan", () => {
    const blockIds = plan.map((block) => block.id);
    expect(new Set(blockIds).size).toBe(blockIds.length);

    const taskIds = allTasks.map((task) => task.id);
    expect(new Set(taskIds).size).toBe(taskIds.length);
  });

  it("prefixes each task id with its block, so progress keys read as addresses", () => {
    for (const block of plan) {
      for (const task of block.tasks) {
        expect(task.id.startsWith(`${block.id}-`), task.id).toBe(true);
      }
    }
  });

  it("links reading over https, and practice over https with one named exception", () => {
    // kiwisdr.com's public receiver list is served over plain http and has
    // no https listener; it is linked anyway because there is no substitute.
    const allowedHttp = new Set(["http://kiwisdr.com/public/"]);
    for (const block of plan) {
      for (const resource of block.resources) {
        expect(resource.url.startsWith("https://"), resource.title).toBe(true);
      }
      for (const resource of block.practice) {
        expect(
          resource.url.startsWith("https://") || allowedHttp.has(resource.url),
          resource.title,
        ).toBe(true);
      }
    }
  });

  it("gives every block something that scores you, and says what to log", () => {
    for (const block of plan) {
      expect(block.practice.length, block.id).toBeGreaterThan(0);
      for (const resource of block.practice) {
        expect(resource.note?.length ?? 0, resource.title).toBeGreaterThan(20);
      }
    }
  });

  it("puts a clock or a count in every gate", () => {
    // A gate without a number is an opinion. Each one names a time limit, a
    // count, or a distance the reader can be held to.
    const measurable =
      /\b(minutes?|hours?|hour|seconds?|weekend|year|fifty|twenty|twelve|hundred|kilometers?|inside)\b/i;
    for (const block of plan) {
      expect(block.gate, block.id).toMatch(measurable);
    }
  });

  it("holds the antenna block to a measured standard", () => {
    const wire = plan.find((block) => block.id === "wire");
    expect(wire?.gate).toContain("three inches");
    expect(wire?.gate).toContain("thirty minutes");
    expect(wire?.gate).toContain("azimuth");
    const ids = wire?.tasks.map((task) => task.id) ?? [];
    for (const id of [
      "wire-ocf",
      "wire-longwire",
      "wire-terminated",
      "wire-multimeter",
    ]) {
      expect(ids).toContain(id);
    }
  });

  it("has a weekly cadence and a gear list with a cost on every line", () => {
    expect(cadence.length).toBeGreaterThan(3);
    expect(gear.length).toBeGreaterThan(5);
    for (const line of gear) {
      expect(line.cost, line.item).toMatch(/^\d+( to \d+)?$/);
    }
    expect(rules.length).toBeGreaterThan(3);
  });
});

describe("the subject", () => {
  // The words that would say what the training is for. The page describes
  // field communications; this keeps it describing field communications.
  const offSubject = [
    "18E",
    "18X",
    "Special Forces",
    "Green Beret",
    "Q Course",
    "Q-Course",
    "SFAS",
    "SFQC",
    "ODA",
    "MOS",
    "Robin Sage",
    "Communications Sergeant",
    "Fort Bragg",
    "Fort Liberty",
    "Camp Mackall",
    "selection",
    "STP",
    "Soldier's Manual",
    "Trainer's Guide",
    "OPORD",
    "operations order",
    "SOI",
    "signal operating instructions",
    "signal annex",
    "SFOD",
    "ANCOC",
    "clandestine",
    "sterilize",
    "331-201",
    "113-596",
  ];

  it.each(offSubject)("never says %s", (word) => {
    // Whole words: "MOS" must not be found inside "most", nor "ODA" in "today".
    const escaped = word.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    expect(everything).not.toMatch(new RegExp(`\\b${escaped}\\b`, "i"));
  });

  it("names its subject in the intro", () => {
    expect(copy.intro).toContain("Field communications");
  });
});
