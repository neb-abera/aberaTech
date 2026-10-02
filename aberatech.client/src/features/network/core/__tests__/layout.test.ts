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

describe("layout", () => {
  it("draws nothing for an empty document", () => {
    expect(layout(emptyNetwork())).toEqual({
      nodes: [],
      edges: [],
      sectors: [],
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
  });

  it("labels each sector outside the ring, anchored away from it", () => {
    const { sectors } = layout(example());
    expect(sectors.map((s) => s.id)).toEqual(["engineering", "capital"]);
    for (const label of sectors) {
      expect(label.x).toBeGreaterThan(0);
      expect(label.x).toBeLessThan(width);
      expect(["start", "middle", "end"]).toContain(label.anchor);
    }
  });

  it("places a person with no organization in their sector, inside the ring", () => {
    const document = crowd(5, 2);
    const { nodes } = layout(document);
    const loose = nodes.find((node) => node.id === "p0");
    expect(loose).toBeDefined();
    const fromCentre = Math.hypot(
      (loose?.x ?? 0) - width / 2,
      (loose?.y ?? 0) - height / 2,
    );
    expect(fromCentre).toBeLessThan(Math.min(width, height) * 0.3);
  });
});
