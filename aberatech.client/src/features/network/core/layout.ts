import type { NetworkDocument, Status } from "./model";

/**
 * Where everything is drawn. Deterministic: the same document always gives
 * the same picture, so a node stays where the eye left it after an edit,
 * and a test can say where a node is.
 *
 * A clustered map. Each sector has an anchor on an ellipse around the
 * centre. Organizations and people start near their sector's anchor, then
 * a fixed number of passes pull a person toward each organization it
 * belongs to, pull everything gently toward its anchor, push every pair
 * apart so nothing overlaps, and keep the whole thing inside the box. A
 * person in two sectors' organizations ends up between them, which is
 * what a bridge looks like. Around each sector's nodes a hull is drawn,
 * so the sector reads as one shape. Labels are placed after the nodes,
 * each where it does not cover a node or another label.
 */

export const width = 1000;
export const height = 780;

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
  /** An organization that is an event: drawn as a square hub. */
  event?: boolean;
}

export interface Edge {
  from: string;
  to: string;
}

export interface Hull {
  id: string;
  label: string;
  /** The SVG path of the padded hull. Empty for a sector with no nodes. */
  path: string;
  /** Where the sector's caption sits: at the hull's edge, clear of every node. */
  x: number;
  y: number;
  anchor: "start" | "middle" | "end";
}

export interface Label {
  id: string;
  text: string;
  x: number;
  y: number;
  anchor: "start" | "middle" | "end";
  size: number;
}

export interface Layout {
  nodes: Node[];
  edges: Edge[];
  hulls: Hull[];
  labels: Label[];
}

const organizationRadius = 13;
/** A person's node grows with the tier: 7 px at tier 1, 15 px at tier 5. */
export const personRadius = (tier: number): number => 5 + 2 * tier;

const centre = { x: width / 2, y: height / 2 };
const margin = 34;
const passes = 320;
const edgeLength = 42;
const hullPadding = 26;

/** A small, fixed pseudo-random number from a string, in [0, 1). */
function hashUnit(text: string): number {
  let hash = 2166136261;
  for (let i = 0; i < text.length; i++) {
    hash ^= text.charCodeAt(i);
    hash = Math.imul(hash, 16777619);
  }
  return ((hash >>> 0) % 10000) / 10000;
}

export function layout(document: NetworkDocument): Layout {
  const sectors =
    document.sectors.length > 0 ? document.sectors : [{ id: "", label: "" }];
  const orgIds = new Set(document.organizations.map((org) => org.id));
  const sectorOf = new Map<string, string>();
  for (const org of document.organizations) sectorOf.set(org.id, org.sector);

  // How much each sector holds decides how much of the ellipse it gets.
  const weight = new Map(sectors.map((s) => [s.id, 2]));
  for (const org of document.organizations)
    weight.set(org.sector, (weight.get(org.sector) ?? 2) + 3);
  for (const person of document.people)
    weight.set(person.sector, (weight.get(person.sector) ?? 2) + 1);
  const total = sectors.reduce((sum, s) => sum + (weight.get(s.id) ?? 2), 0);

  const anchor = new Map<string, { x: number; y: number }>();
  let start = -Math.PI / 2;
  for (const sector of sectors) {
    const span = (2 * Math.PI * (weight.get(sector.id) ?? 2)) / total;
    const mid = start + span / 2;
    // The ellipse: the heavier the sector, the further out its anchor, so
    // a big cluster has room and a small one sits close in.
    const share = (weight.get(sector.id) ?? 2) / total;
    const reach = 0.58 + Math.min(0.3, share * 1.4);
    anchor.set(sector.id, {
      x: centre.x + (width / 2 - margin) * reach * 1.05 * Math.cos(mid),
      y: centre.y + (height / 2 - margin) * reach * Math.sin(mid),
    });
    start += span;
  }

  const nodes: Node[] = [];
  const index = new Map<string, number>();
  const place = (node: Node) => {
    index.set(node.id, nodes.length);
    nodes.push(node);
  };
  for (const org of document.organizations) {
    const at = anchor.get(org.sector) ?? centre;
    const angle = hashUnit(`o:${org.id}`) * 2 * Math.PI;
    const distance = 30 + hashUnit(`d:${org.id}`) * 70;
    place({
      id: org.id,
      kind: "organization",
      label: org.name,
      sector: org.sector,
      x: at.x + distance * Math.cos(angle),
      y: at.y + distance * Math.sin(angle),
      r: organizationRadius,
      ...(org.kind === "event" ? { event: true } : {}),
    });
  }
  for (const person of document.people) {
    const home = person.orgs.find((org) => orgIds.has(org));
    const near = home
      ? nodes[index.get(home) ?? 0]
      : (anchor.get(person.sector) ?? centre);
    const angle = hashUnit(`p:${person.id}`) * 2 * Math.PI;
    const distance = home ? edgeLength : 20 + hashUnit(`q:${person.id}`) * 60;
    place({
      id: person.id,
      kind: "person",
      label: person.name,
      sector: person.sector,
      x: near.x + distance * Math.cos(angle),
      y: near.y + distance * Math.sin(angle),
      r: personRadius(person.tier),
      status: person.status,
      tier: person.tier,
    });
  }

  const edges: Edge[] = [];
  for (const person of document.people)
    for (const org of person.orgs)
      if (orgIds.has(org)) edges.push({ from: person.id, to: org });

  relax(nodes, edges, index, anchor);

  const hulls = sectors
    .map((sector) => hull(sector.id, sector.label, nodes))
    .filter((h): h is Hull => h !== null);
  const labels = placeLabels(nodes, hulls.map(captionBox));
  return { nodes, edges, hulls, labels };
}

function relax(
  nodes: Node[],
  edges: Edge[],
  index: Map<string, number>,
  anchor: Map<string, { x: number; y: number }>,
) {
  const vx = new Float64Array(nodes.length);
  const vy = new Float64Array(nodes.length);
  for (let pass = 0; pass < passes; pass++) {
    // Cool down: large moves early, small ones late, so the picture settles.
    const heat = 1 - pass / passes;
    vx.fill(0);
    vy.fill(0);

    // Toward the sector's anchor, organizations harder than people.
    for (let i = 0; i < nodes.length; i++) {
      const node = nodes[i];
      const at = anchor.get(node.sector) ?? centre;
      const pull = node.kind === "organization" ? 0.04 : 0.03;
      vx[i] += (at.x - node.x) * pull;
      vy[i] += (at.y - node.y) * pull;
    }

    // Along each membership: a spring to edgeLength.
    for (const edge of edges) {
      const a = index.get(edge.from);
      const b = index.get(edge.to);
      if (a === undefined || b === undefined) continue;
      const dx = nodes[b].x - nodes[a].x;
      const dy = nodes[b].y - nodes[a].y;
      const distance = Math.hypot(dx, dy) || 1;
      // A bridge between sectors pulls a quarter as hard, so a person who
      // belongs to two worlds leans toward the other without leaving home.
      const weight = nodes[a].sector === nodes[b].sector ? 1 : 0.25;
      const stretch = ((distance - edgeLength) / distance) * weight;
      vx[a] += dx * stretch * 0.08;
      vy[a] += dy * stretch * 0.08;
      vx[b] -= dx * stretch * 0.02;
      vy[b] -= dy * stretch * 0.02;
    }

    // Apart: a charge that falls off with distance, and a hard minimum gap.
    for (let i = 0; i < nodes.length; i++) {
      for (let j = i + 1; j < nodes.length; j++) {
        const a = nodes[i];
        const b = nodes[j];
        let dx = b.x - a.x;
        let dy = b.y - a.y;
        let distance = Math.hypot(dx, dy);
        if (distance === 0) {
          // Split two nodes on one point along a fixed direction.
          dx = 1;
          dy = 0;
          distance = 1;
        }
        const minimum = a.r + b.r + 8;
        const charge = Math.min(2.5, 900 / (distance * distance));
        let push = charge;
        if (distance < minimum) push += (minimum - distance) * 0.5;
        const ux = dx / distance;
        const uy = dy / distance;
        vx[i] -= ux * push;
        vy[i] -= uy * push;
        vx[j] += ux * push;
        vy[j] += uy * push;
      }
    }

    for (let i = 0; i < nodes.length; i++) {
      const node = nodes[i];
      const step = 0.35 + 0.65 * heat;
      node.x += vx[i] * step;
      node.y += vy[i] * step;
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
  // A last pass that only separates, so no two nodes overlap whatever the
  // forces left.
  for (let pass = 0; pass < 40; pass++) {
    for (let i = 0; i < nodes.length; i++) {
      for (let j = i + 1; j < nodes.length; j++) {
        const a = nodes[i];
        const b = nodes[j];
        const dx = b.x - a.x || 1;
        const dy = b.y - a.y;
        const distance = Math.hypot(dx, dy);
        const minimum = a.r + b.r + 6;
        if (distance >= minimum) continue;
        const push = (minimum - distance) / 2;
        const ux = dx / distance;
        const uy = dy / distance;
        a.x -= ux * push;
        a.y -= uy * push;
        b.x += ux * push;
        b.y += uy * push;
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

/** The convex hull of a sector's nodes, padded, as a rounded SVG path. */
function hull(id: string, label: string, nodes: Node[]): Hull | null {
  const own = nodes.filter((node) => node.sector === id);
  if (own.length === 0) return null;
  // Pad every node into a small ring of points, then hull those: the result
  // keeps a margin round each node, round or square alike.
  const points: { x: number; y: number }[] = [];
  for (const node of own) {
    const pad = node.r + hullPadding;
    for (let k = 0; k < 12; k++) {
      const angle = (k / 12) * 2 * Math.PI;
      points.push({
        x: node.x + pad * Math.cos(angle),
        y: node.y + pad * Math.sin(angle),
      });
    }
  }
  const outline = convexHull(points);
  const path = roundedPath(outline);
  const top = outline.reduce(
    (best, p) => (p.y < best.y ? p : best),
    outline[0],
  );
  const bottom = outline.reduce(
    (best, p) => (p.y > best.y ? p : best),
    outline[0],
  );
  const left = outline.reduce(
    (best, p) => (p.x < best.x ? p : best),
    outline[0],
  );
  const right = outline.reduce(
    (best, p) => (p.x > best.x ? p : best),
    outline[0],
  );
  // The caption goes at the hull's edge, first above, then below, then to
  // either side, wherever it covers no node at all.
  const tries: { x: number; y: number; anchor: Hull["anchor"] }[] = [
    { x: top.x, y: top.y - 8, anchor: "middle" },
    { x: bottom.x, y: bottom.y + 18, anchor: "middle" },
    { x: left.x - 8, y: left.y + 5, anchor: "end" },
    { x: right.x + 8, y: right.y + 5, anchor: "start" },
  ];
  const boxes = nodes.map(nodeBox);
  for (const t of tries) {
    const box = captionBox({ id, label, path, ...t });
    if (box.x1 < 2 || box.x2 > width - 2 || box.y1 < 2 || box.y2 > height - 2)
      continue;
    if (boxes.some((b) => overlaps(box, b))) continue;
    return { id, label, path, ...t };
  }
  return { id, label, path, ...tries[0] };
}

const captionSize = 13;

/** The rectangle a sector caption covers, as the graph draws it. */
function captionBox(hull: Hull) {
  const w = hull.label.length * captionSize * 0.72;
  const x1 =
    hull.anchor === "middle"
      ? hull.x - w / 2
      : hull.anchor === "start"
        ? hull.x
        : hull.x - w;
  return { x1, y1: hull.y - captionSize, x2: x1 + w, y2: hull.y + 3 };
}

const nodeBox = (node: Node) => ({
  x1: node.x - node.r,
  y1: node.y - node.r,
  x2: node.x + node.r,
  y2: node.y + node.r,
});

function convexHull(points: { x: number; y: number }[]) {
  const sorted = [...points].sort((a, b) => a.x - b.x || a.y - b.y);
  const cross = (
    o: { x: number; y: number },
    a: { x: number; y: number },
    b: { x: number; y: number },
  ) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
  const lower: typeof sorted = [];
  for (const p of sorted) {
    while (
      lower.length >= 2 &&
      cross(lower[lower.length - 2], lower[lower.length - 1], p) <= 0
    )
      lower.pop();
    lower.push(p);
  }
  const upper: typeof sorted = [];
  for (let i = sorted.length - 1; i >= 0; i--) {
    const p = sorted[i];
    while (
      upper.length >= 2 &&
      cross(upper[upper.length - 2], upper[upper.length - 1], p) <= 0
    )
      upper.pop();
    upper.push(p);
  }
  upper.pop();
  lower.pop();
  return [...lower, ...upper];
}

/** A closed path through the points with the corners softened. */
function roundedPath(points: { x: number; y: number }[]): string {
  if (points.length < 3) return "";
  const n = points.length;
  const parts: string[] = [];
  for (let i = 0; i < n; i++) {
    const p0 = points[(i + n - 1) % n];
    const p1 = points[i];
    const p2 = points[(i + 1) % n];
    const a = { x: (p0.x + p1.x) / 2, y: (p0.y + p1.y) / 2 };
    const b = { x: (p1.x + p2.x) / 2, y: (p1.y + p2.y) / 2 };
    if (i === 0) parts.push(`M${a.x.toFixed(1)} ${a.y.toFixed(1)}`);
    parts.push(
      `Q${p1.x.toFixed(1)} ${p1.y.toFixed(1)} ${b.x.toFixed(1)} ${b.y.toFixed(1)}`,
    );
  }
  return `${parts.join(" ")} Z`;
}

/**
 * Captions for organizations and the people worth naming at rest (tier 4
 * and 5): each tried below, above, right, then left of its node, and kept
 * where it covers no node and no earlier caption. One that fits nowhere is
 * left off; the node still names itself on hover and in the list.
 */
function placeLabels(
  nodes: Node[],
  taken: { x1: number; y1: number; x2: number; y2: number }[],
): Label[] {
  const placed: Label[] = [];
  const boxes = [...nodes.map(nodeBox), ...taken];
  const wanted = nodes
    .filter((node) => node.kind === "organization" || (node.tier ?? 0) >= 4)
    .sort(
      (a, b) =>
        (b.kind === "organization" ? 1 : 0) -
          (a.kind === "organization" ? 1 : 0) || (b.tier ?? 0) - (a.tier ?? 0),
    );
  for (const node of wanted) {
    const size = node.kind === "organization" ? 11.5 : 10;
    const textWidth = node.label.length * size * 0.56;
    const gap = 4;
    // Below, above, right, left, then the four diagonals, each at the
    // node's edge and again a little further out.
    const tries: { x: number; y: number; anchor: Label["anchor"] }[] = [];
    for (const away of [0, 6, 14]) {
      const d = node.r + gap + away;
      tries.push(
        { x: node.x, y: node.y + d + size, anchor: "middle" },
        { x: node.x, y: node.y - d, anchor: "middle" },
        { x: node.x + d, y: node.y + size * 0.35, anchor: "start" },
        { x: node.x - d, y: node.y + size * 0.35, anchor: "end" },
        {
          x: node.x + d * 0.7,
          y: node.y + d * 0.7 + size * 0.7,
          anchor: "start",
        },
        {
          x: node.x - d * 0.7,
          y: node.y + d * 0.7 + size * 0.7,
          anchor: "end",
        },
        {
          x: node.x + d * 0.7,
          y: node.y - d * 0.7 + size * 0.2,
          anchor: "start",
        },
        {
          x: node.x - d * 0.7,
          y: node.y - d * 0.7 + size * 0.2,
          anchor: "end",
        },
      );
    }
    for (const t of tries) {
      const x1 =
        t.anchor === "middle"
          ? t.x - textWidth / 2
          : t.anchor === "start"
            ? t.x
            : t.x - textWidth;
      const box = { x1, y1: t.y - size, x2: x1 + textWidth, y2: t.y + 2 };
      if (box.x1 < 4 || box.x2 > width - 4 || box.y1 < 4 || box.y2 > height - 4)
        continue;
      if (boxes.some((b) => overlaps(box, b))) continue;
      placed.push({
        id: node.id,
        text: node.label,
        x: t.x,
        y: t.y,
        anchor: t.anchor,
        size,
      });
      boxes.push(box);
      break;
    }
  }
  return placed;
}

const overlaps = (
  a: { x1: number; y1: number; x2: number; y2: number },
  b: { x1: number; y1: number; x2: number; y2: number },
) => a.x1 < b.x2 && a.x2 > b.x1 && a.y1 < b.y2 && a.y2 > b.y1;
