import { useTheme } from "@mui/material/styles";
import type { AerobicPoint, ProjectionPoint, WeekVolume } from "../core/api";
import { formatPace } from "../core/format";

/**
 * Hand-rolled SVG charts, themed from the MUI palette. Deliberately not a
 * charting dependency: three small charts do not justify one, and the strict
 * CSP stays untouched.
 */

const WIDTH = 720;
const HEIGHT = 260;
const MARGIN = { top: 16, right: 96, bottom: 34, left: 56 };

function scale(domainMin: number, domainMax: number, rangeMin: number, rangeMax: number) {
  const span = domainMax - domainMin || 1;
  return (value: number) => rangeMin + ((value - domainMin) / span) * (rangeMax - rangeMin);
}

interface AxisProps {
  ticks: { at: number; label: string }[];
  x?: boolean;
  position: number;
  color: string;
}

function Axis({ ticks, x = false, position, color }: AxisProps) {
  return (
    <g>
      {ticks.map((tick) =>
        x ? (
          <text
            key={tick.label + tick.at}
            x={tick.at}
            y={position}
            textAnchor="middle"
            fontSize={11}
            fill={color}
          >
            {tick.label}
          </text>
        ) : (
          <text
            key={tick.label + tick.at}
            x={position}
            y={tick.at + 4}
            textAnchor="end"
            fontSize={11}
            fill={color}
          >
            {tick.label}
          </text>
        ),
      )}
    </g>
  );
}

export function AerobicTrendChart({ points }: { points: AerobicPoint[] }) {
  const theme = useTheme();
  if (points.length === 0) {
    return null;
  }

  const paces = points.map((p) => p.medianSecPerKm);
  const lo = Math.min(...paces) - 20;
  const hi = Math.max(...paces) + 20;
  // Faster on top: the y scale is inverted on purpose.
  const y = scale(lo, hi, HEIGHT - MARGIN.bottom, MARGIN.top);
  const x = scale(0, Math.max(points.length - 1, 1), MARGIN.left, WIDTH - MARGIN.right);

  const path = points
    .map((p, i) => `${i === 0 ? "M" : "L"}${x(i).toFixed(1)} ${y(p.medianSecPerKm).toFixed(1)}`)
    .join(" ");

  return (
    <svg
      viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
      width="100%"
      role="img"
      aria-label="Monthly median heart-rate-normalized pace, faster toward the top"
    >
      <Axis
        color={theme.palette.text.secondary}
        position={MARGIN.left - 8}
        ticks={[lo + 20, (lo + hi) / 2, hi - 20].map((v) => ({
          at: y(v),
          label: formatPace(v),
        }))}
      />
      <path d={path} fill="none" stroke={theme.palette.primary.main} strokeWidth={2} />
      {points.map((p, i) => (
        <g key={p.month}>
          <circle
            cx={x(i)}
            cy={y(p.medianSecPerKm)}
            r={4}
            fill={theme.palette.primary.main}
          >
            <title>{`${p.month}: ${formatPace(p.medianSecPerKm)} median of ${p.runs} runs`}</title>
          </circle>
          <text
            x={x(i)}
            y={HEIGHT - 12}
            textAnchor="middle"
            fontSize={11}
            fill={theme.palette.text.secondary}
          >
            {p.month.slice(2)}
          </text>
        </g>
      ))}
    </svg>
  );
}

export function VolumeChart({
  weeks,
  planMinutes,
}: {
  weeks: WeekVolume[];
  planMinutes: number;
}) {
  const theme = useTheme();
  if (weeks.length === 0) {
    return null;
  }

  const shown = weeks.slice(-26);
  const top = Math.max(planMinutes * 1.3, ...shown.map((w) => w.minutes));
  const y = scale(0, top, HEIGHT - MARGIN.bottom, MARGIN.top);
  const band = (WIDTH - MARGIN.left - MARGIN.right) / shown.length;

  return (
    <svg
      viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
      width="100%"
      role="img"
      aria-label="Weekly endurance minutes against the plan target"
    >
      <Axis
        color={theme.palette.text.secondary}
        position={MARGIN.left - 8}
        ticks={[0, planMinutes, top].map((v) => ({ at: y(v), label: `${Math.round(v)}m` }))}
      />
      {shown.map((week, i) => (
        <rect
          key={week.weekStart}
          x={MARGIN.left + i * band + 1}
          y={y(week.minutes)}
          width={Math.max(band - 2, 1)}
          height={y(0) - y(week.minutes)}
          rx={2}
          fill={theme.palette.primary.main}
          opacity={week.minutes >= planMinutes ? 1 : 0.55}
        >
          <title>{`Week of ${week.weekStart}: ${Math.round(week.minutes)} min`}</title>
        </rect>
      ))}
      <line
        x1={MARGIN.left}
        x2={WIDTH - MARGIN.right}
        y1={y(planMinutes)}
        y2={y(planMinutes)}
        stroke={theme.palette.info.main}
        strokeDasharray="6 4"
        strokeWidth={2}
      />
      <text
        x={WIDTH - MARGIN.right + 6}
        y={y(planMinutes) + 4}
        fontSize={11}
        fill={theme.palette.info.main}
      >
        plan {Math.round(planMinutes)}m
      </text>
    </svg>
  );
}

export interface GoalLine {
  vdot: number;
  label: string;
}

export function ProjectionChart({
  curve,
  goals,
  engineVdot,
}: {
  curve: ProjectionPoint[];
  goals: GoalLine[];
  engineVdot: number | null;
}) {
  const theme = useTheme();
  if (curve.length === 0) {
    return null;
  }

  const values = curve.map((p) => p.vdot).concat(goals.map((g) => g.vdot));
  const lo = Math.min(...values) - 1.5;
  const hi = Math.max(...values) + 1.5;
  const y = scale(lo, hi, HEIGHT - MARGIN.bottom, MARGIN.top);
  const months = curve[curve.length - 1].months;
  const x = scale(0, months, MARGIN.left, WIDTH - MARGIN.right);

  const path = curve
    .map((p, i) => `${i === 0 ? "M" : "L"}${x(p.months).toFixed(1)} ${y(p.vdot).toFixed(1)}`)
    .join(" ");

  return (
    <svg
      viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
      width="100%"
      role="img"
      aria-label="Projected VDOT over the coming months against goal thresholds"
    >
      <Axis
        color={theme.palette.text.secondary}
        position={MARGIN.left - 8}
        ticks={[lo + 1.5, (lo + hi) / 2, hi - 1.5].map((v) => ({
          at: y(v),
          label: v.toFixed(0),
        }))}
      />
      <Axis
        x
        color={theme.palette.text.secondary}
        position={HEIGHT - 10}
        ticks={[0, 6, 12, 18, 24, 30]
          .filter((m) => m <= months)
          .map((m) => ({ at: x(m), label: `+${m}mo` }))}
      />
      {engineVdot !== null && engineVdot > lo && engineVdot < hi && (
        <g>
          <line
            x1={MARGIN.left}
            x2={WIDTH - MARGIN.right}
            y1={y(engineVdot)}
            y2={y(engineVdot)}
            stroke={theme.palette.text.disabled}
            strokeDasharray="2 4"
          />
          <text
            x={WIDTH - MARGIN.right + 6}
            y={y(engineVdot) + 4}
            fontSize={11}
            fill={theme.palette.text.secondary}
          >
            engine {engineVdot.toFixed(0)}
          </text>
        </g>
      )}
      {goals.map((goal) => (
        <g key={goal.label}>
          <line
            x1={MARGIN.left}
            x2={WIDTH - MARGIN.right}
            y1={y(goal.vdot)}
            y2={y(goal.vdot)}
            stroke={theme.palette.warning.main}
            strokeDasharray="5 4"
          />
          <text
            x={WIDTH - MARGIN.right + 6}
            y={y(goal.vdot) + 4}
            fontSize={11}
            fill={theme.palette.warning.main}
          >
            {goal.label}
          </text>
        </g>
      ))}
      <path d={path} fill="none" stroke={theme.palette.primary.main} strokeWidth={2.5} />
    </svg>
  );
}
