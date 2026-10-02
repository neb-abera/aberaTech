import { describe, expect, it } from "vitest";
import {
  coerce,
  counts,
  emptyNetwork,
  example,
  parse,
  stringify,
} from "../model";

describe("coerce", () => {
  it("drops what it cannot draw and keeps the rest", () => {
    const document = coerce({
      version: 1,
      updated: "2026-10-02",
      sectors: [{ id: "a", label: "A" }, { id: "", label: "nameless" }, "junk"],
      organizations: [
        { id: "x", name: "X", sector: "a", url: "https://x.example" },
        { name: "no id" },
      ],
      people: [
        {
          id: "p",
          name: "P",
          role: "Lead",
          sector: "a",
          orgs: ["x", 3],
          tier: 9,
          status: "odd",
          mutuals: 2,
        },
        { id: "", name: "nobody" },
      ],
    });
    expect(document.sectors).toEqual([{ id: "a", label: "A" }]);
    expect(document.organizations).toEqual([
      { id: "x", name: "X", sector: "a", url: "https://x.example" },
    ]);
    expect(document.people).toEqual([
      {
        id: "p",
        name: "P",
        role: "Lead",
        sector: "a",
        orgs: ["x"],
        tier: 1,
        status: "none",
        mutuals: 2,
      },
    ]);
    expect(document.updated).toBe("2026-10-02");
  });

  it("answers an empty document for anything that is not an object", () => {
    expect(coerce(null)).toEqual(emptyNetwork());
    expect(coerce([1])).toEqual(emptyNetwork());
    expect(coerce("text")).toEqual(emptyNetwork());
  });
});

describe("parse", () => {
  it("accepts the example and round-trips it", () => {
    const parsed = parse(stringify(example()));
    expect(parsed).toEqual({ ok: true, document: example() });
  });

  it("names every problem at once", () => {
    const parsed = parse(
      JSON.stringify({
        sectors: [
          { id: "a", label: "A" },
          { id: "a", label: "Again" },
        ],
        organizations: [
          { id: "x", name: "X", sector: "zz", url: "javascript:alert(1)" },
        ],
        people: [
          {
            id: "p",
            name: "P",
            sector: "a",
            orgs: ["nope"],
            tier: 7,
            status: "maybe",
          },
          { id: "p", name: "Twice", sector: "a", tier: 1, status: "none" },
        ],
      }),
    );
    expect(parsed.ok).toBe(false);
    if (parsed.ok) return;
    expect(parsed.problems).toEqual([
      'Sector "a" appears twice.',
      'Organization "x" names a sector that is not listed: "zz".',
      'Organization "x" has a url that is not http or https.',
      'Person "p" has a tier that is not 1 to 5.',
      'Person "p" has a status that is not one of connected, pending, followed, withdrawn, blocked, none.',
      'Person "p" names an organization that is not listed: "nope".',
      'Person "p" appears twice.',
    ]);
  });

  it("refuses a way in that names nobody", () => {
    const document = example();
    document.people[0].via = ["acme", "nobody"];
    const parsed = parse(JSON.stringify(document));
    expect(parsed).toEqual({
      ok: false,
      problems: ['Person "ada" names a way in that is not listed: "nobody".'],
    });
  });

  it("refuses text that is not JSON, and JSON that is not an object with the three lists", () => {
    expect(parse("{")).toMatchObject({ ok: false });
    expect(parse("[]")).toEqual({
      ok: false,
      problems: ["The document is a JSON object."],
    });
    expect(parse("{}")).toEqual({
      ok: false,
      problems: [
        '"sectors" is a list.',
        '"organizations" is a list.',
        '"people" is a list.',
      ],
    });
  });
});

describe("counts", () => {
  it("counts people by status", () => {
    const summary = counts(example());
    expect(summary.people).toBe(2);
    expect(summary.organizations).toBe(3);
    expect(summary.byStatus.pending).toBe(1);
    expect(summary.byStatus.followed).toBe(1);
    expect(summary.byStatus.connected).toBe(0);
  });
});
