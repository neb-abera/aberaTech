import { useTheme } from "@mui/material/styles";
import type { Sensitivity } from "../core/api";
import { formatSeconds } from "../core/format";

const WIDTH = 720;
const ROW = 34;
const MARGIN = { top: 26, right: 130, bottom: 24, left: 130 };

function label(sensitivity: Sensitivity): string {
  const { factor, lowValue, highValue } = sensitivity;
  const show = (value: number) =>
    factor === "Compliance"
      ? `${Math.round(value * 100)}%`
      : factor === "Months"
        ? `${value.toFixed(0)}mo`
        : factor === "RaceMassKg"
          ? `${(value * 2.2046226218).toFixed(0)}lb`
          : `${value.toFixed(1)}h`;
  return `${show(lowValue)} → ${show(highValue)}`;
}

/**
 * How much race time each factor is worth across the range it could plausibly
 * take, longest bar first.
 *
 * This is the picture that answers "what should I change" — not by ranking
 * factors by how much they matter in principle, but by how much they move
 * *this* athlete's *this* race, from where they actually are. A bar that runs
 * left of the current prediction is time gained.
 */
export default function Tornado({
  sensitivities,
  baselineSeconds,
}: {
  sensitivities: Sensitivity[];
  baselineSeconds: number;
}) {
  const theme = useTheme();
  if (sensitivities.length === 0) {
    return null;
  }

  const height = MARGIN.top + MARGIN.bottom + sensitivities.length * ROW;
  const seconds = sensitivities
    .flatMap((s) => [s.lowSeconds, s.highSeconds])
    .concat(baselineSeconds);
  const lo = Math.min(...seconds);
  const hi = Math.max(...seconds);
  const span = hi - lo || 1;

  const x = (value: number) =>
    MARGIN.left + ((value - lo) / span) * (WIDTH - MARGIN.left - MARGIN.right);

  return (
    <svg
      viewBox={`0 0 ${WIDTH} ${height}`}
      width="100%"
      role="img"
      aria-label="Race time moved by each factor across its plausible range, largest first"
    >
      <line
        x1={x(baselineSeconds)}
        x2={x(baselineSeconds)}
        y1={MARGIN.top - 8}
        y2={height - MARGIN.bottom}
        stroke={theme.palette.text.secondary}
        strokeDasharray="3 3"
      />
      <text
        x={x(baselineSeconds)}
        y={MARGIN.top - 14}
        fontSize={11}
        textAnchor="middle"
        fill={theme.palette.text.secondary}
      >
        now {formatSeconds(baselineSeconds)}
      </text>

      {sensitivities.map((sensitivity, index) => {
        const y = MARGIN.top + index * ROW;
        const left = Math.min(sensitivity.lowSeconds, sensitivity.highSeconds);
        const right = Math.max(sensitivity.lowSeconds, sensitivity.highSeconds);
        const faster = Math.min(left, baselineSeconds);

        return (
          <g key={sensitivity.factor}>
            <text
              x={MARGIN.left - 10}
              y={y + 15}
              fontSize={12}
              textAnchor="end"
              fill={theme.palette.text.primary}
            >
              {sensitivity.label}
            </text>
            {/* The half that buys time, then the half that costs it. */}
            <rect
              x={x(faster)}
              y={y}
              width={Math.max(1, x(baselineSeconds) - x(faster))}
              height={ROW - 12}
              fill={theme.palette.success.main}
              opacity={0.65}
            />
            <rect
              x={x(Math.min(baselineSeconds, right))}
              y={y}
              width={Math.max(
                1,
                x(right) - x(Math.min(baselineSeconds, right)),
              )}
              height={ROW - 12}
              fill={theme.palette.warning.main}
              opacity={0.55}
            />
            <text
              x={WIDTH - MARGIN.right + 8}
              y={y + 15}
              fontSize={11}
              fill={theme.palette.text.secondary}
            >
              {label(sensitivity)} · {Math.round(sensitivity.swing)}s
            </text>
          </g>
        );
      })}

      <text
        x={MARGIN.left}
        y={height - 6}
        fontSize={11}
        fill={theme.palette.text.secondary}
      >
        {formatSeconds(lo)} (faster)
      </text>
      <text
        x={WIDTH - MARGIN.right}
        y={height - 6}
        fontSize={11}
        textAnchor="end"
        fill={theme.palette.text.secondary}
      >
        {formatSeconds(hi)} (slower)
      </text>
    </svg>
  );
}
