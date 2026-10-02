import type { NetworkDocument, Status } from "./model";

/**
 * Where everything is drawn. Deterministic: the same document always gives
 * the same picture, so a node stays where the eye left it after an edit,
 * and a test can say where a node is.
 *
 * Organizations sit on a ring, grouped by sector, each sector taking a
 * share of the circle in proportion to what it holds. People sit on a
 * smaller ring around their first organization, and people with no
 * organization sit in their sector's wedge on an inner ring. Then a few
 * passes push overlapping nodes apart and keep every node inside the box.
 */

export const width = 1000;
export const height = 700;

export interface Node {
  id: string;
  kind: "person" | "organization";
  label: string;
  sector: string;
  x: number;
  y: number;
  r: number;
  status?: Status;
  tier?: number;
}

export interface Edge {
  from: string;
  to: string;
}

export interface SectorLabel {
  id: string;
  label: string;
  x: number;
  y: number;
  /** Where the text is anchored, so it reads away from the ring. */
  anchor: "start" | "middle" | "end";
}

export interface Layout {
  nodes: Node[];
  edges: Edge[];
  sectors: SectorLabel[];
}

const organizationRadius = 15;
/** A person's node grows with the tier: 7 px at tier 1, 15 px at tier 5. */
export const personRadius = (tier: number): number => 5 + 2 * tier;

const centre = { x: width / 2, y: height / 2 };
const outerRing = Math.min(width, height) * 0.36;
const innerRing = Math.min(width, height) * 0.14;
const satelliteRing = 44;
const margin = 28;
const passes = 60;

const polar = (radius: number, angle: number) => ({
  x: centre.x + radius * Math.cos(angle),
  y: centre.y + radius * Math.sin(angle),
});

export function layout(document: NetworkDocument): Layout {
  const sectors =
    document.sectors.length > 0 ? document.sectors : [{ id: "", label: "" }];
  const orgsBySector = new Map(
    sectors.map((s) => [s.id, [] as typeof document.organizations]),
  );
  for (const org of document.organizations)
    (orgsBySector.get(org.sector) ?? orgsBySector.get("") ?? []).push(org);
  const loosePeopleBySector = new Map(
    sectors.map((s) => [s.id, [] as typeof document.people]),
  );
  const orgIds = new Set(document.organizations.map((org) => org.id));
  for (const person of document.people) {
    const home = person.orgs.find((org) => orgIds.has(org));
    if (!home)
      (
        loosePeopleBySector.get(person.sector) ??
        loosePeopleBySector.get("") ??
        []
      ).push(person);
  }

  // Each sector's share of the circle: its organizations plus its loose
  // people, with a floor so a sector of one still gets room.
  const weights = sectors.map((s) =>
    Math.max(
      2,
      (orgsBySector.get(s.id)?.length ?? 0) * 2 +
        (loosePeopleBySector.get(s.id)?.length ?? 0),
    ),
  );
  const total = weights.reduce((sum, w) => sum + w, 0);

  const nodes: Node[] = [];
  const edges: Edge[] = [];
  const sectorLabels: SectorLabel[] = [];
  const position = new Map<string, { x: number; y: number }>();

  let start = -Math.PI / 2;
  sectors.forEach((sector, index) => {
    const span = (2 * Math.PI * weights[index]) / total;
    const mid = start + span / 2;
    if (sector.id !== "" || sectors.length > 1) {
      const at = polar(outerRing + organizationRadius + 46, mid);
      const cos = Math.cos(mid);
      sectorLabels.push({
        id: sector.id,
        label: sector.label,
        x: at.x,
        y: at.y,
        anchor: cos > 0.3 ? "start" : cos < -0.3 ? "end" : "middle",
      });
    }

    const orgs = orgsBySector.get(sector.id) ?? [];
    orgs.forEach((org, i) => {
      const angle = start + (span * (i + 1)) / (orgs.length + 1);
      const at = polar(outerRing, angle);
      position.set(org.id, at);
      nodes.push({
        id: org.id,
        kind: "organization",
        label: org.name,
        sector: sector.id,
        x: at.x,
        y: at.y,
        r: organizationRadius,
      });
    });

    const loose = loosePeopleBySector.get(sector.id) ?? [];
    loose.forEach((person, i) => {
      const angle = start + (span * (i + 1)) / (loose.length + 1);
      const at = polar(innerRing, angle);
      nodes.push({
        id: person.id,
        kind: "person",
        label: person.name,
        sector: sector.id,
        x: at.x,
        y: at.y,
        r: personRadius(person.tier),
        status: person.status,
        tier: person.tier,
      });
    });

    start += span;
  });

  // People around their first organization, spread on its satellite ring,
  // starting on the side that faces away from the centre so the ring
  // reads as an outer fringe.
  const members = new Map<string, typeof document.people>();
  for (const person of document.people) {
    const home = person.orgs.find((org) => orgIds.has(org));
    if (!home) continue;
    if (!members.has(home)) members.set(home, []);
    members.get(home)?.push(person);
  }
  for (const [orgId, people] of members) {
    const hub = position.get(orgId);
    if (!hub) continue;
    const outward = Math.atan2(hub.y - centre.y, hub.x - centre.x);
    const spread =
      people.length === 1
        ? 0
        : Math.min(2 * Math.PI, (people.length * 2.3 * Math.PI) / 7);
    people.forEach((person, i) => {
      const angle =
        outward +
        (people.length === 1
          ? 0
          : -spread / 2 + (spread * i) / (people.length - 1));
      const ring =
        satelliteRing + (people.length > 6 ? 10 * Math.floor(i / 6) : 0);
      const at = polar(0, 0);
      at.x = hub.x + ring * Math.cos(angle);
      at.y = hub.y + ring * Math.sin(angle);
      nodes.push({
        id: person.id,
        kind: "person",
        label: person.name,
        sector: person.sector,
        x: at.x,
        y: at.y,
        r: personRadius(person.tier),
        status: person.status,
        tier: person.tier,
      });
    });
  }

  for (const person of document.people)
    for (const org of person.orgs)
      if (orgIds.has(org)) edges.push({ from: person.id, to: org });

  relax(nodes);
  return { nodes, edges, sectors: sectorLabels };
}

/**
 * Push overlapping nodes apart, a little per pass, and keep everything
 * inside the box. Organizations are the heavier: a person moves away from
 * one more than it moves away from the person.
 */
function relax(nodes: Node[]) {
  for (let pass = 0; pass < passes; pass++) {
    for (let i = 0; i < nodes.length; i++) {
      for (let j = i + 1; j < nodes.length; j++) {
        const a = nodes[i];
        const b = nodes[j];
        const dx = b.x - a.x;
        const dy = b.y - a.y;
        const gap = 6;
        const minimum = a.r + b.r + gap;
        const distance = Math.hypot(dx, dy);
        if (distance >= minimum) continue;
        // Two nodes on the same point: split them along a fixed direction.
        const ux = distance === 0 ? 1 : dx / distance;
        const uy = distance === 0 ? 0 : dy / distance;
        const push = (minimum - distance) / 2;
        const aWeight = a.kind === "organization" ? 0.25 : 1;
        const bWeight = b.kind === "organization" ? 0.25 : 1;
        const sum = aWeight + bWeight;
        a.x -= (ux * push * 2 * aWeight) / sum;
        a.y -= (uy * push * 2 * aWeight) / sum;
        b.x += (ux * push * 2 * bWeight) / sum;
        b.y += (uy * push * 2 * bWeight) / sum;
      }
    }
    for (const node of nodes) {
      node.x = Math.min(
        width - margin - node.r,
        Math.max(margin + node.r, node.x),
      );
      node.y = Math.min(
        height - margin - node.r,
        Math.max(margin + node.r, node.y),
      );
    }
  }
  for (const node of nodes) {
    node.x = Math.round(node.x * 10) / 10;
    node.y = Math.round(node.y * 10) / 10;
  }
}
