import Box from "@mui/material/Box";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import Stack from "@mui/material/Stack";
import { useTheme } from "@mui/material/styles";
import Typography from "@mui/material/Typography";
import type { Durability } from "../core/api";
import MathPanel from "./MathPanel";

const WIDTH = 720;
const HEIGHT = 120;
const MARGIN = { top: 8, right: 8, bottom: 22, left: 36 };

/** One number with the word that goes with it. */
function Figure({
  label,
  value,
  note,
}: {
  label: string;
  value: string;
  note?: string;
}) {
  return (
    <Box sx={{ minWidth: 120 }}>
      <Typography variant="caption" sx={{ color: "text.secondary" }}>
        {label}
      </Typography>
      <Typography variant="h6" sx={{ lineHeight: 1.2 }}>
        {value}
      </Typography>
      {note && (
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          {note}
        </Typography>
      )}
    </Box>
  );
}

function acwrChip(acwr: number | null) {
  if (acwr === null)
    return { label: "needs 4 weeks of log", color: "default" as const };
  if (acwr > 1.5) return { label: "spike", color: "error" as const };
  if (acwr > 1.3) return { label: "ramping", color: "warning" as const };
  if (acwr < 0.8) return { label: "undercut", color: "warning" as const };
  return { label: "in the band", color: "success" as const };
}

/**
 * How the load is being carried: this week against the four behind it, how
 * evenly it was spread, and how long since a day off from impact. The
 * numbers injury-risk work watches, next to the volume chart that only
 * says how much.
 */
export default function DurabilityCard({
  durability,
}: {
  durability: Durability;
}) {
  const theme = useTheme();
  const chip = acwrChip(durability.acwr);
  const top = Math.max(1, ...durability.days.map((d) => d.load));
  const band = (WIDTH - MARGIN.left - MARGIN.right) / durability.days.length;
  const y = (v: number) =>
    HEIGHT - MARGIN.bottom - (v / top) * (HEIGHT - MARGIN.top - MARGIN.bottom);

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack
          direction="row"
          spacing={1}
          sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 1, mb: 0.5 }}
        >
          <Typography variant="h6">Durability</Typography>
          <Chip size="small" label={chip.label} color={chip.color} />
        </Stack>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          Load is hours weighted by what each zone costs to recover from. The
          ratio is this week over the average of the last four (Gabbett: safe
          0.8–1.3, risk climbs past 1.5); monotony is the week&apos;s mean daily
          load over its spread (Foster: over 2.0 the week has no contrast).
        </Typography>

        <Stack
          direction="row"
          spacing={3}
          sx={{ flexWrap: "wrap", rowGap: 2, mb: 2 }}
        >
          <Figure
            label="Acute : chronic"
            value={durability.acwr === null ? "—" : durability.acwr.toFixed(2)}
            note={`${durability.acuteLoad.toFixed(1)} this week vs ${durability.chronicLoad.toFixed(1)} avg`}
          />
          <Figure
            label="Monotony"
            value={
              durability.monotony === null
                ? "—"
                : durability.monotony.toFixed(1)
            }
            note={
              durability.weeklyStrain === null
                ? undefined
                : `strain ${durability.weeklyStrain.toFixed(1)}`
            }
          />
          <Figure
            label="Impact streak"
            value={`${durability.impactStreakDays} d`}
            note={`${durability.restDaysLast7} rest days in the last 7`}
          />
        </Stack>

        <svg
          viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
          width="100%"
          role="img"
          aria-label="Daily training load over the last 28 days"
        >
          <text
            x={MARGIN.left - 6}
            y={y(top) + 4}
            textAnchor="end"
            fontSize={11}
            fill={theme.palette.text.secondary}
          >
            {top.toFixed(1)}
          </text>
          <text
            x={MARGIN.left - 6}
            y={y(0) + 4}
            textAnchor="end"
            fontSize={11}
            fill={theme.palette.text.secondary}
          >
            0
          </text>
          {durability.days.map((day, i) => (
            <rect
              key={day.date}
              x={MARGIN.left + i * band + 1}
              y={y(day.load)}
              width={Math.max(1, band - 2)}
              height={y(0) - y(day.load)}
              fill={
                i >= durability.days.length - 7
                  ? theme.palette.primary.main
                  : theme.palette.text.disabled
              }
              opacity={day.impact ? 1 : 0.6}
            >
              <title>{`${day.date}: ${day.load.toFixed(2)}${day.impact ? " (impact)" : ""}`}</title>
            </rect>
          ))}
          <text
            x={MARGIN.left}
            y={HEIGHT - 6}
            fontSize={11}
            fill={theme.palette.text.secondary}
          >
            {durability.days[0]?.date ?? ""}
          </text>
          <text
            x={WIDTH - MARGIN.right}
            y={HEIGHT - 6}
            textAnchor="end"
            fontSize={11}
            fill={theme.palette.text.secondary}
          >
            {durability.days[durability.days.length - 1]?.date ?? ""}
          </text>
        </svg>

        <MathPanel title="How the load was read" steps={durability.steps} />
      </CardContent>
    </Card>
  );
}
