/**
 * The plan as a plan: staged, addressable, linkable, gated, honest about
 * what is free, and dated where it makes claims about standards.
 *
 * The page renders the data in plan.ts and nothing else, so these rules are
 * the whole contract: a task that cannot be ticked, a gate that cannot be
 * failed, a link that opens over plain http, or a standards table with no
 * date on it fails the build rather than a reader.
 */
import { describe, expect, it } from "vitest";
import {
  allTasks,
  bookshelf,
  cadence,
  copy,
  documentKey,
  ideas,
  levels,
  plan,
  rules,
  stages,
  standards,
} from "../plan";

describe("the stages", () => {
  it("run classical, quantum, post-quantum, each with an exit standard", () => {
    expect(stages.map((stage) => stage.id)).toEqual([
      "classical",
      "quantum",
      "postquantum",
    ]);
    for (const stage of stages) {
      expect(stage.blocks.length, stage.id).toBeGreaterThan(1);
      expect(stage.why.length, stage.id).toBeGreaterThan(40);
      expect(stage.exit.length, stage.id).toBeGreaterThan(40);
    }
  });

  it("flatten to the plan in page order", () => {
    expect(plan.map((block) => block.id)).toEqual(
      stages.flatMap((stage) => stage.blocks.map((block) => block.id)),
    );
  });

  it("start with the classical foundations and end with the research edge", () => {
    // The order is the argument: post-quantum cryptography is classical
    // cryptography with new assumptions, so the classical stage comes first
    // however experienced the reader is.
    expect(plan[0].id).toBe("maths");
    expect(plan[plan.length - 1].id).toBe("research");
  });
});

describe("the plan", () => {
  it("has twelve blocks each with tasks, a gate and reading", () => {
    expect(plan).toHaveLength(12);
    for (const block of plan) {
      expect(block.tasks.length, block.id).toBeGreaterThan(2);
      expect(block.gate.length, block.id).toBeGreaterThan(30);
      expect(block.resources.length, block.id).toBeGreaterThan(0);
      expect(block.why.length, block.id).toBeGreaterThan(30);
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

  it("links everything over https", () => {
    for (const block of plan) {
      for (const resource of [...block.resources, ...block.practice]) {
        expect(resource.url.startsWith("https://"), resource.title).toBe(true);
      }
    }
    for (const book of bookshelf) {
      expect(book.url.startsWith("https://"), book.title).toBe(true);
    }
    for (const standard of standards) {
      expect(standard.url.startsWith("https://"), standard.name).toBe(true);
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
    // count, a tolerance or a coverage the reader can be held to.
    const measurable =
      /\b(minutes?|hours?|weekend|months?|one|two|three|five|six|ten|thirty|forty|hundred|percent|within|every|under)\b/i;
    for (const block of plan) {
      expect(block.gate, block.id).toMatch(measurable);
    }
  });

  it("implements the standards from their specifications, and says so", () => {
    // Two gates are implementations written from a FIPS document alone and
    // scored against NIST's vectors. Those are the plan's claim to expert
    // level, so they are held to by name.
    const lattices = plan.find((block) => block.id === "lattices");
    expect(lattices?.gate).toContain("FIPS 203");
    expect(lattices?.gate).toContain("known-answer vector");
    const families = plan.find((block) => block.id === "families");
    expect(families?.gate).toContain("FIPS 205");
    expect(families?.gate).toContain("known-answer vector");
  });

  it("has a weekly cadence and rules", () => {
    expect(cadence.length).toBeGreaterThan(3);
    expect(rules.length).toBeGreaterThan(3);
  });
});

describe("the ideas", () => {
  it("each say what the idea is and how you know you own it", () => {
    expect(ideas.length).toBeGreaterThan(6);
    const names = ideas.map((idea) => idea.name);
    expect(new Set(names).size).toBe(names.length);
    for (const idea of ideas) {
      expect(idea.idea.length, idea.name).toBeGreaterThan(40);
      expect(idea.test.length, idea.name).toBeGreaterThan(40);
    }
  });
});

describe("the standards table", () => {
  it("names the three final FIPS documents and the transition timeline", () => {
    const names = standards.map((standard) => standard.name);
    expect(names.some((name) => name.includes("FIPS 203"))).toBe(true);
    expect(names.some((name) => name.includes("FIPS 204"))).toBe(true);
    expect(names.some((name) => name.includes("FIPS 205"))).toBe(true);
    expect(names.some((name) => name.includes("8547"))).toBe(true);
    expect(names.some((name) => name.includes("CNSA"))).toBe(true);
  });

  it("gives every standard a status and says what it is for", () => {
    const names = standards.map((standard) => standard.name);
    expect(new Set(names).size).toBe(names.length);
    for (const standard of standards) {
      expect(standard.status.length, standard.name).toBeGreaterThan(5);
      expect(standard.what.length, standard.name).toBeGreaterThan(10);
    }
  });

  it("is dated, because it is a snapshot", () => {
    // A table of standards without a date becomes wrong silently. The copy
    // above it names the month it was true in.
    expect(copy.standards).toMatch(
      /\b(January|February|March|April|May|June|July|August|September|October|November|December) 20\d\d\b/,
    );
  });
});

describe("the bookshelf", () => {
  it("names a stage, an author and a reason for every book", () => {
    expect(bookshelf.length).toBeGreaterThan(8);
    const titles = bookshelf.map((book) => book.title);
    expect(new Set(titles).size).toBe(titles.length);
    for (const book of bookshelf) {
      expect(Object.keys(levels), book.title).toContain(book.level);
      expect(book.authors.length, book.title).toBeGreaterThan(3);
      expect(book.why.length, book.title).toBeGreaterThan(30);
    }
  });

  it("has a free book at every stage, so cost is never the reason to stop", () => {
    for (const level of Object.keys(levels)) {
      expect(
        bookshelf.some((book) => book.level === level && book.free),
        level,
      ).toBe(true);
    }
  });

  it("is what the blocks read from: every book is a resource somewhere", () => {
    const linked = new Set(
      plan.flatMap((block) =>
        [...block.resources, ...block.practice].map((resource) => resource.url),
      ),
    );
    for (const book of bookshelf) {
      expect(linked.has(book.url), book.title).toBe(true);
    }
  });
});

describe("the page", () => {
  it("names its subject in the intro, and says whose progress it is", () => {
    expect(copy.intro).toContain("post-quantum");
    expect(copy.note).toContain("read-only");
    expect(documentKey).toBe("quantum-cryptography");
  });
});
