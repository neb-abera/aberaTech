import { useTheme } from "@mui/material/styles";
import type { ProjectionPoint } from "../core/api";
import { formatSeconds } from "../core/format";

const WIDTH = 720;
const HEIGHT = 260;
const MARGIN = { top: 16, right: 84, bottom: 34, left: 62 };

/**
 * Predicted race time over the coming months, with the 80% band around it.
 *
 * Faster is up, because that is the direction improvement feels like; the axis
 * is therefore inverted against the numbers, and labelled in times rather than
 * seconds so nobody has to do the conversion in their head.
 */
export default function FanChart({
  fan,
  targetSeconds,
  atMonths,
}: {
  fan: ProjectionPoint[];
  targetSeconds: number | null;
  atMonths: number;
}) {
  const theme = useTheme();
  if (fan.length < 2) {
    return null;
  }

  const values = fan
    .flatMap((p) => [p.low, p.high])
    .concat(targetSeconds === null ? [] : [targetSeconds]);
  const fastest = Math.min(...values);
  const slowest = Math.max(...values);
  const pad = (slowest - fastest) * 0.08 || 10;

  const months = fan[fan.length - 1].months || 1;
  const x = (m: number) =>
    MARGIN.left + (m / months) * (WIDTH - MARGIN.left - MARGIN.right);
  // Inverted: a smaller number of seconds sits higher.
  const y = (seconds: number) =>
    MARGIN.top +
    ((seconds - (fastest - pad)) / (slowest + pad - (fastest - pad))) *
      (HEIGHT - MARGIN.top - MARGIN.bottom);

  const line = fan
    .map(
      (p, i) =>
        `${i === 0 ? "M" : "L"}${x(p.months).toFixed(1)} ${y(p.vdot).toFixed(1)}`,
    )
    .join(" ");

  const band = [
    ...fan.map(
      (p, i) =>
        `${i === 0 ? "M" : "L"}${x(p.months).toFixed(1)} ${y(p.low).toFixed(1)}`,
    ),
    ...fan
      .slice()
      .reverse()
      .map((p) => `L${x(p.months).toFixed(1)} ${y(p.high).toFixed(1)}`),
    "Z",
  ].join(" ");

  const gridlines = [fastest, (fastest + slowest) / 2, slowest];

  return (
    <svg
      viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
      width="100%"
      role="img"
      aria-label="Predicted race time over the coming months, with its 80% interval"
    >
      {gridlines.map((seconds) => (
        <g key={seconds}>
          <line
            x1={MARGIN.left}
            x2={WIDTH - MARGIN.right}
            y1={y(seconds)}
            y2={y(seconds)}
            stroke={theme.palette.divider}
          />
          <text
            x={MARGIN.left - 8}
            y={y(seconds) + 4}
            fontSize={11}
            textAnchor="end"
            fill={theme.palette.text.secondary}
          >
            {formatSeconds(seconds)}
          </text>
        </g>
      ))}

      <path d={band} fill={theme.palette.primary.main} opacity={0.18} />
      <path
        d={line}
        stroke={theme.palette.primary.main}
        strokeWidth={2}
        fill="none"
      />

      {targetSeconds !== null && (
        <g>
          <line
            x1={MARGIN.left}
            x2={WIDTH - MARGIN.right}
            y1={y(targetSeconds)}
            y2={y(targetSeconds)}
            stroke={theme.palette.warning.main}
            strokeDasharray="4 4"
          />
          <text
            x={WIDTH - MARGIN.right + 6}
            y={y(targetSeconds) + 4}
            fontSize={11}
            fill={theme.palette.warning.main}
          >
            target
          </text>
        </g>
      )}

      <line
        x1={x(atMonths)}
        x2={x(atMonths)}
        y1={MARGIN.top}
        y2={HEIGHT - MARGIN.bottom}
        stroke={theme.palette.text.secondary}
        strokeDasharray="2 4"
      />

      {[0, months / 2, months].map((m) => (
        <text
          key={m}
          x={x(m)}
          y={HEIGHT - 12}
          fontSize={11}
          textAnchor="middle"
          fill={theme.palette.text.secondary}
        >
          +{m.toFixed(0)}mo
        </text>
      ))}
    </svg>
  );
}
