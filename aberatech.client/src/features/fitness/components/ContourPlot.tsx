import { useTheme } from "@mui/material/styles";
import type { FactorName, SurfaceResult } from "../core/api";
import { formatSeconds } from "../core/format";

const WIDTH = 720;
const HEIGHT = 320;
const MARGIN = { top: 16, right: 108, bottom: 44, left: 66 };

function axisLabel(factor: FactorName, value: number): string {
  switch (factor) {
    case "Compliance":
      return `${Math.round(value * 100)}%`;
    case "Months":
      return `${value.toFixed(0)}mo`;
    case "RaceMassKg":
      return `${(value * 2.2046226218).toFixed(0)}lb`;
    default:
      return `${value.toFixed(1)}h`;
  }
}

/**
 * The isoline: every combination of the two factors that lands exactly on the
 * target, traced through the grid by marching squares.
 *
 * This is the line worth having. A single answer says "nine hours a week";
 * this says "nine hours at your current compliance, or seven if you never miss
 * a session, or eight if you also drop four pounds" — the trade the athlete is
 * actually making, drawn rather than described.
 */
function isoline(
  grid: number[][],
  target: number,
  x: (column: number) => number,
  y: (row: number) => number,
): string[] {
  const segments: string[] = [];
  const rows = grid.length;
  const columns = grid[0]?.length ?? 0;

  for (let row = 0; row < rows - 1; row++) {
    for (let column = 0; column < columns - 1; column++) {
      const corners = [
        { v: grid[row][column], x: column, y: row },
        { v: grid[row][column + 1], x: column + 1, y: row },
        { v: grid[row + 1][column + 1], x: column + 1, y: row + 1 },
        { v: grid[row + 1][column], x: column, y: row + 1 },
      ];

      // Where the target falls between two neighbouring corners, the crossing
      // is linearly interpolated along that edge.
      const crossings: { x: number; y: number }[] = [];
      for (let i = 0; i < 4; i++) {
        const a = corners[i];
        const b = corners[(i + 1) % 4];
        if (a.v === b.v) continue;
        const t = (target - a.v) / (b.v - a.v);
        if (t < 0 || t > 1) continue;
        crossings.push({
          x: x(a.x + (b.x - a.x) * t),
          y: y(a.y + (b.y - a.y) * t),
        });
      }

      if (crossings.length >= 2) {
        segments.push(
          `M${crossings[0].x.toFixed(1)} ${crossings[0].y.toFixed(1)}L${crossings[1].x.toFixed(1)} ${crossings[1].y.toFixed(1)}`,
        );
      }
    }
  }

  return segments;
}

/**
 * Two factors swept at once, as a field of predicted race times, with the line
 * that hits the target drawn across it and the current scenario marked.
 *
 * Clicking anywhere on the field moves both factors there, which is what turns
 * a chart into a control.
 */
export default function ContourPlot({
  surface,
  currentAcross,
  currentDown,
  onPick,
}: {
  surface: SurfaceResult;
  currentAcross: number;
  currentDown: number;
  onPick?: (across: number, down: number) => void;
}) {
  const theme = useTheme();
  const { acrossValues, downValues, seconds } = surface;
  if (seconds.length === 0 || acrossValues.length === 0) {
    return null;
  }

  const columns = acrossValues.length;
  const rows = downValues.length;
  const plotWidth = WIDTH - MARGIN.left - MARGIN.right;
  const plotHeight = HEIGHT - MARGIN.top - MARGIN.bottom;

  const cellWidth = plotWidth / (columns - 1);
  const cellHeight = plotHeight / (rows - 1);

  const x = (column: number) => MARGIN.left + column * cellWidth;
  const y = (row: number) => MARGIN.top + row * cellHeight;

  const flat = seconds.flat();
  const fastest = Math.min(...flat);
  const slowest = Math.max(...flat);
  const span = slowest - fastest || 1;

  // A single-hue ramp: darker is faster. One hue keeps it readable in both
  // themes and avoids implying categories where there is a continuum.
  const shade = (value: number) => {
    const t = 1 - (value - fastest) / span;
    return `color-mix(in srgb, ${theme.palette.primary.main} ${(12 + t * 74).toFixed(0)}%, ${theme.palette.background.paper})`;
  };

  const acrossAt = (value: number) =>
    x(
      ((value - acrossValues[0]) /
        (acrossValues[columns - 1] - acrossValues[0] || 1)) *
        (columns - 1),
    );
  const downAt = (value: number) =>
    y(
      ((value - downValues[0]) / (downValues[rows - 1] - downValues[0] || 1)) *
        (rows - 1),
    );

  const pick = (event: React.MouseEvent<HTMLElement>) => {
    if (!onPick) return;
    const box = event.currentTarget.getBoundingClientRect();
    const px = ((event.clientX - box.left) / box.width) * WIDTH;
    const py = ((event.clientY - box.top) / box.height) * HEIGHT;

    const across =
      acrossValues[0] +
      ((px - MARGIN.left) / plotWidth) *
        (acrossValues[columns - 1] - acrossValues[0]);
    const down =
      downValues[0] +
      ((py - MARGIN.top) / plotHeight) * (downValues[rows - 1] - downValues[0]);

    if (
      px < MARGIN.left ||
      px > WIDTH - MARGIN.right ||
      py < MARGIN.top ||
      py > HEIGHT - MARGIN.bottom
    ) {
      return;
    }
    onPick(across, down);
  };

  // Arrow keys nudge both factors a cell at a time, so the field is usable
  // without a pointer rather than being a picture with a mouse-only shortcut.
  const nudge = (event: React.KeyboardEvent<HTMLElement>) => {
    if (!onPick) return;
    const acrossStep =
      (acrossValues[columns - 1] - acrossValues[0]) / (columns - 1);
    const downStep = (downValues[rows - 1] - downValues[0]) / (rows - 1);

    const moves: Record<string, [number, number]> = {
      ArrowLeft: [-acrossStep, 0],
      ArrowRight: [acrossStep, 0],
      ArrowUp: [0, -downStep],
      ArrowDown: [0, downStep],
    };

    const move = moves[event.key];
    if (!move) return;
    event.preventDefault();
    onPick(currentAcross + move[0], currentDown + move[1]);
  };

  const ticks = [0, Math.floor((columns - 1) / 2), columns - 1];
  const downTicks = [0, Math.floor((rows - 1) / 2), rows - 1];

  const field = (
    <svg
      viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
      width="100%"
      role="img"
      aria-label={`Predicted race time over ${surface.across} and ${surface.down}, with the line that hits the target`}
      style={{ display: "block" }}
    >
      {seconds.map((row, rowIndex) =>
        row.map((value, columnIndex) =>
          rowIndex < rows - 1 && columnIndex < columns - 1 ? (
            <rect
              key={`${downValues[rowIndex]}-${acrossValues[columnIndex]}`}
              x={x(columnIndex)}
              y={y(rowIndex)}
              width={cellWidth + 0.5}
              height={cellHeight + 0.5}
              fill={shade(value)}
            />
          ) : null,
        ),
      )}

      {surface.targetSeconds !== null &&
        isoline(seconds, surface.targetSeconds, x, y).map((segment) => (
          <path
            key={segment}
            d={segment}
            stroke={theme.palette.warning.main}
            strokeWidth={2.5}
            fill="none"
          />
        ))}

      <circle
        cx={acrossAt(currentAcross)}
        cy={downAt(currentDown)}
        r={5}
        fill={theme.palette.background.paper}
        stroke={theme.palette.text.primary}
        strokeWidth={2}
      />

      {ticks.map((column) => (
        <text
          key={`x-${column}`}
          x={x(column)}
          y={HEIGHT - 22}
          fontSize={11}
          textAnchor="middle"
          fill={theme.palette.text.secondary}
        >
          {axisLabel(surface.across, acrossValues[column])}
        </text>
      ))}
      {downTicks.map((row) => (
        <text
          key={`y-${row}`}
          x={MARGIN.left - 8}
          y={y(row) + 4}
          fontSize={11}
          textAnchor="end"
          fill={theme.palette.text.secondary}
        >
          {axisLabel(surface.down, downValues[row])}
        </text>
      ))}

      <text
        x={WIDTH - MARGIN.right + 10}
        y={MARGIN.top + 12}
        fontSize={11}
        fill={theme.palette.text.secondary}
      >
        {formatSeconds(fastest)}
      </text>
      <text
        x={WIDTH - MARGIN.right + 10}
        y={HEIGHT - MARGIN.bottom}
        fontSize={11}
        fill={theme.palette.text.secondary}
      >
        {formatSeconds(slowest)}
      </text>
      {surface.targetSeconds !== null && (
        <text
          x={WIDTH - MARGIN.right + 10}
          y={HEIGHT / 2}
          fontSize={11}
          fill={theme.palette.warning.main}
        >
          target {formatSeconds(surface.targetSeconds)}
        </text>
      )}
    </svg>
  );

  // A real button rather than a clickable graphic: it takes focus, it answers
  // to the keyboard, and the arrow keys move both factors a cell at a time, so
  // the field is a control for everyone rather than a picture with a
  // mouse-only shortcut bolted on.
  return onPick ? (
    <button
      type="button"
      onClick={pick}
      onKeyDown={nudge}
      aria-label={`Pick a combination of ${surface.across} and ${surface.down}. Arrow keys move both.`}
      style={{
        display: "block",
        width: "100%",
        padding: 0,
        border: 0,
        background: "none",
        cursor: "crosshair",
      }}
    >
      {field}
    </button>
  ) : (
    field
  );
}
