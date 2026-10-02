import { describe, expect, it } from "vitest";
import { height, layout, type Node, width } from "../layout";
import {
  emptyNetwork,
  example,
  type NetworkDocument,
  type Person,
} from "../model";

function crowd(people: number, organizations: number): NetworkDocument {
  const document: NetworkDocument = {
    version: 1,
    updated: "2026-10-02",
    sectors: [
      { id: "s1", label: "One" },
      { id: "s2", label: "Two" },
      { id: "s3", label: "Three" },
    ],
    organizations: [],
    people: [],
  };
  for (let i = 0; i < organizations; i++)
    document.organizations.push({
      id: `o${i}`,
      name: `Org ${i}`,
      sector: `s${(i % 3) + 1}`,
    });
  for (let i = 0; i < people; i++) {
    const person: Person = {
      id: `p${i}`,
      name: `Person ${i}`,
      role: "Role",
      sector: `s${(i % 3) + 1}`,
      orgs: i % 5 === 0 ? [] : [`o${i % organizations}`],
      tier: ((i % 5) + 1) as Person["tier"],
      status: "pending",
    };
    document.people.push(person);
  }
  return document;
}

const overlapping = (nodes: Node[]) => {
  const pairs: string[] = [];
  for (let i = 0; i < nodes.length; i++)
    for (let j = i + 1; j < nodes.length; j++) {
      const a = nodes[i];
      const b = nodes[j];
      if (Math.hypot(a.x - b.x, a.y - b.y) < a.r + b.r)
        pairs.push(`${a.id}/${b.id}`);
    }
  return pairs;
};

const centroid = (nodes: Node[]) => ({
  x: nodes.reduce((sum, n) => sum + n.x, 0) / nodes.length,
  y: nodes.reduce((sum, n) => sum + n.y, 0) / nodes.length,
});

describe("layout", () => {
  it("draws nothing for an empty document", () => {
    expect(layout(emptyNetwork())).toEqual({
      nodes: [],
      edges: [],
      hulls: [],
      labels: [],
    });
  });

  it("is the same picture every time", () => {
    expect(layout(crowd(40, 9))).toEqual(layout(crowd(40, 9)));
  });

  it("keeps every node inside the box and no two nodes on top of each other", () => {
    const { nodes } = layout(crowd(60, 12));
    expect(nodes).toHaveLength(72);
    for (const node of nodes) {
      expect(node.x - node.r, node.id).toBeGreaterThanOrEqual(0);
      expect(node.y - node.r, node.id).toBeGreaterThanOrEqual(0);
      expect(node.x + node.r, node.id).toBeLessThanOrEqual(width);
      expect(node.y + node.r, node.id).toBeLessThanOrEqual(height);
    }
    expect(overlapping(nodes)).toEqual([]);
  });

  it("gathers each sector into its own region", () => {
    const { nodes } = layout(crowd(60, 12));
    const centres = ["s1", "s2", "s3"].map((sector) =>
      centroid(nodes.filter((node) => node.sector === sector)),
    );
    // The three centroids are well apart, and every node is nearer its own
    // sector's centroid than either of the others.
    for (let i = 0; i < 3; i++)
      for (let j = i + 1; j < 3; j++)
        expect(
          Math.hypot(centres[i].x - centres[j].x, centres[i].y - centres[j].y),
        ).toBeGreaterThan(150);
    const strays = nodes.filter((node) => {
      const own = ["s1", "s2", "s3"].indexOf(node.sector);
      const distance = (c: { x: number; y: number }) =>
        Math.hypot(node.x - c.x, node.y - c.y);
      return centres.some(
        (c, k) => k !== own && distance(c) < distance(centres[own]),
      );
    });
    // A person who belongs to another sector's organization sits between
    // the two; with everyone in their own sector's organizations here,
    // almost nobody strays.
    expect(strays.length).toBeLessThanOrEqual(3);
  });

  it("puts a person beside their first organization and draws the edge", () => {
    const { nodes, edges } = layout(example());
    const byId = Object.fromEntries(nodes.map((node) => [node.id, node]));
    expect(
      Math.hypot(byId.ada.x - byId.acme.x, byId.ada.y - byId.acme.y),
    ).toBeLessThan(90);
    expect(edges).toEqual([
      { from: "ada", to: "acme" },
      { from: "bo", to: "north-fund" },
      { from: "bo", to: "acme" },
    ]);
    expect(byId.ada.r).toBe(15);
    expect(byId.bo.r).toBe(11);
    expect(byId.acme.kind).toBe("organization");
    expect(byId["radio-day"].event).toBe(true);
    expect(byId.acme.event).toBeUndefined();
  });

  it("draws a hull round each sector with its caption at the edge, clear of every node", () => {
    for (const document of [example(), crowd(60, 12)]) {
      const { hulls, nodes } = layout(document);
      expect(hulls.length).toBe(document.sectors.length);
      for (const hull of hulls) {
        expect(hull.path).toMatch(
          /^M[\d.]+ [\d.]+ (Q[\d.]+ [\d.]+ [\d.]+ [\d.]+ ?)+Z$/,
        );
        const w = hull.label.length * 13 * 0.72;
        const x1 =
          hull.anchor === "middle"
            ? hull.x - w / 2
            : hull.anchor === "start"
              ? hull.x
              : hull.x - w;
        const box = { x1, y1: hull.y - 13, x2: x1 + w, y2: hull.y + 3 };
        expect(box.x1).toBeGreaterThanOrEqual(0);
        expect(box.x2).toBeLessThanOrEqual(width);
        for (const node of nodes) {
          const hit =
            box.x1 < node.x + node.r &&
            box.x2 > node.x - node.r &&
            box.y1 < node.y + node.r &&
            box.y2 > node.y - node.r;
          expect(hit, `${hull.label} covers ${node.id}`).toBe(false);
        }
      }
    }
  });

  it("captions every organization and every tier 4 or 5 person without covering a node, and leaves the rest to hover", () => {
    const { nodes, labels } = layout(crowd(60, 12));
    const byId = Object.fromEntries(nodes.map((node) => [node.id, node]));
    const captioned = new Set(labels.map((label) => label.id));
    for (const node of nodes) {
      if (node.kind === "person" && (node.tier ?? 0) < 4)
        expect(captioned.has(node.id), node.id).toBe(false);
    }
    expect(labels.length).toBeGreaterThan(12);
    for (const label of labels) {
      const size = label.size;
      const w = label.text.length * size * 0.56;
      const x1 =
        label.anchor === "middle"
          ? label.x - w / 2
          : label.anchor === "start"
            ? label.x
            : label.x - w;
      const box = { x1, y1: label.y - size, x2: x1 + w, y2: label.y + 2 };
      for (const node of nodes) {
        const hit =
          box.x1 < node.x + node.r &&
          box.x2 > node.x - node.r &&
          box.y1 < node.y + node.r &&
          box.y2 > node.y - node.r;
        expect(hit, `${label.text} covers ${node.id}`).toBe(false);
      }
      expect(byId[label.id].label).toBe(label.text);
    }
  });
});
