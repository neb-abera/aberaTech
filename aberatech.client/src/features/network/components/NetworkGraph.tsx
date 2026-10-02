import Box from "@mui/material/Box";
import * as React from "react";
import { height, type Layout, type Node, width } from "../core/layout";
import type { Status } from "../core/model";

export interface NetworkGraphProps {
  layout: Layout;
  /** Node ids the filters leave out. Drawn faint, so the picture holds still. */
  dimmed: ReadonlySet<string>;
  selected: string | null;
  onSelect: (id: string | null) => void;
  colorOf: (sector: string) => string;
  /** How many people and organizations the picture holds, for its name. */
  summary: string;
}

/** How a person's standing is drawn: the ring around the node. */
const ring: Record<Status, { dash?: string; width: number; color: string }> = {
  connected: { width: 3, color: "hsl(120, 60%, 45%)" },
  pending: { width: 2, dash: "4 3", color: "hsl(45, 90%, 55%)" },
  followed: { width: 1.5, color: "hsl(210, 20%, 70%)" },
  withdrawn: { width: 1.5, dash: "1 3", color: "hsl(0, 70%, 55%)" },
  blocked: { width: 1.5, dash: "1 3", color: "hsl(0, 70%, 55%)" },
  none: { width: 0, color: "transparent" },
};

const describe = (node: Node): string =>
  node.kind === "organization"
    ? `${node.label}, organization`
    : `${node.label}, ${node.status ?? "none"}`;

/**
 * The picture: organizations on a ring, people around theirs, a line for
 * each membership. Colour is the sector, size is the tier, the ring is
 * where things stand. Every node is a button, so the keyboard reaches it
 * and a screen reader hears its name and standing; the list under the
 * picture carries the rest of what a node holds.
 */
export default function NetworkGraph({
  layout,
  dimmed,
  selected,
  onSelect,
  colorOf,
  summary,
}: NetworkGraphProps) {
  const [hovered, setHovered] = React.useState<string | null>(null);
  const byId = React.useMemo(
    () => new Map(layout.nodes.map((node) => [node.id, node])),
    [layout],
  );
  const lit = selected ?? hovered;
  const neighbours = React.useMemo(() => {
    const set = new Set<string>();
    if (!lit) return set;
    for (const edge of layout.edges) {
      if (edge.from === lit) set.add(edge.to);
      if (edge.to === lit) set.add(edge.from);
    }
    return set;
  }, [layout, lit]);

  const press = (id: string) => onSelect(selected === id ? null : id);

  return (
    <Box
      sx={{
        width: "100%",
        color: "text.primary",
        "& svg": { display: "block", width: "100%", height: "auto" },
        "& [role=button]": { cursor: "pointer", outline: "none" },
        "& [role=button]:focus-visible circle:first-of-type": {
          stroke: "currentColor",
          strokeWidth: 2,
        },
      }}
    >
      <svg
        viewBox={`0 0 ${width} ${height}`}
        role="img"
        aria-label={`Network map: ${summary}`}
        data-testid="network-graph"
      >
        <title>{`Network map: ${summary}`}</title>
        {layout.sectors.map((sector) => (
          <text
            key={sector.id}
            x={sector.x}
            y={sector.y}
            textAnchor={sector.anchor}
            fill={colorOf(sector.id)}
            fontSize={15}
            fontWeight={600}
            letterSpacing={1}
            style={{ textTransform: "uppercase" }}
          >
            {sector.label}
          </text>
        ))}
        {layout.edges.map((edge) => {
          const from = byId.get(edge.from);
          const to = byId.get(edge.to);
          if (!from || !to) return null;
          const faint = dimmed.has(edge.from) || dimmed.has(edge.to);
          const bright = lit !== null && (edge.from === lit || edge.to === lit);
          return (
            <line
              key={`${edge.from}-${edge.to}`}
              x1={from.x}
              y1={from.y}
              x2={to.x}
              y2={to.y}
              stroke={bright ? colorOf(from.sector) : "currentColor"}
              strokeOpacity={faint ? 0.05 : bright ? 0.9 : 0.22}
              strokeWidth={bright ? 2 : 1}
            />
          );
        })}
        {layout.nodes.map((node) => {
          const faint = dimmed.has(node.id);
          const isLit = lit === node.id;
          const near = neighbours.has(node.id);
          const colour = colorOf(node.sector);
          const standing = node.status ? ring[node.status] : ring.none;
          const showLabel =
            !faint &&
            (node.kind === "organization" ||
              (node.tier ?? 0) >= 4 ||
              isLit ||
              near);
          return (
            // biome-ignore lint/a11y/useSemanticElements: a node in an SVG cannot be a <button>; the group carries the role, a tab stop, a name and the key handling a button would.
            <g
              key={node.id}
              role="button"
              tabIndex={0}
              aria-label={describe(node)}
              aria-pressed={selected === node.id}
              data-node={node.id}
              data-dimmed={faint ? "true" : undefined}
              opacity={faint ? 0.15 : 1}
              transform={`translate(${node.x} ${node.y})`}
              onClick={() => press(node.id)}
              onKeyDown={(event) => {
                if (event.key === "Enter" || event.key === " ") {
                  event.preventDefault();
                  press(node.id);
                }
              }}
              onMouseEnter={() => setHovered(node.id)}
              onMouseLeave={() => setHovered(null)}
              onFocus={() => setHovered(node.id)}
              onBlur={() => setHovered(null)}
            >
              {(isLit || near) && (
                <circle
                  r={node.r + 7}
                  fill={colour}
                  fillOpacity={isLit ? 0.3 : 0.15}
                />
              )}
              {node.kind === "organization" ? (
                <>
                  <circle
                    r={node.r}
                    fill={colour}
                    fillOpacity={0.18}
                    stroke={colour}
                    strokeWidth={2.5}
                  />
                  <circle r={node.r * 0.35} fill={colour} />
                </>
              ) : (
                <circle
                  r={node.r}
                  fill={colour}
                  fillOpacity={node.status === "connected" ? 1 : 0.75}
                  stroke={standing.color}
                  strokeWidth={standing.width}
                  strokeDasharray={standing.dash}
                />
              )}
              {showLabel && (
                <text
                  y={node.r + (node.kind === "organization" ? 15 : 12)}
                  textAnchor="middle"
                  fill="currentColor"
                  fontSize={node.kind === "organization" ? 12 : 10.5}
                  fontWeight={node.kind === "organization" || isLit ? 600 : 400}
                  stroke="var(--mui-palette-background-default)"
                  strokeWidth={3}
                  paintOrder="stroke"
                >
                  {node.label}
                </text>
              )}
            </g>
          );
        })}
      </svg>
    </Box>
  );
}
