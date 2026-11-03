import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Stack from "@mui/material/Stack";
import ToggleButton from "@mui/material/ToggleButton";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";
import Typography from "@mui/material/Typography";
import * as React from "react";
import type { DayVolume, WeekVolume } from "../core/api";
import { RollingVolumeChart, rollingSums, VolumeChart } from "./charts";

type View = "rolling" | "weeks";

/**
 * Endurance minutes against the plan. Rolling windows lead: a seven-day
 * block is the same length every day. Calendar weeks are the alternate,
 * for reading the log against the plan's weeks.
 */
export default function TrainingDoseCard({
  days,
  weeks,
  planMinutes,
}: {
  days: DayVolume[];
  weeks: WeekVolume[];
  planMinutes: number;
}) {
  const [view, setView] = React.useState<View>("rolling");

  const seven = rollingSums(days, 7);
  const last7 = seven[seven.length - 1] ?? 0;
  const previous7 = seven[seven.length - 8] ?? 0;
  const month = rollingSums(days, 28);
  const perWeek28 = (month[month.length - 1] ?? 0) / 4;

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack
          direction="row"
          sx={{
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: 1,
          }}
        >
          <Typography variant="h6">Training dose</Typography>
          <ToggleButtonGroup
            size="small"
            exclusive
            value={view}
            onChange={(_, next: View | null) => next && setView(next)}
            aria-label="How endurance minutes are grouped"
          >
            <ToggleButton value="rolling">Rolling</ToggleButton>
            <ToggleButton value="weeks">By week</ToggleButton>
          </ToggleButtonGroup>
        </Stack>

        {view === "rolling" ? (
          <>
            <Typography variant="body2" sx={{ mt: 1 }}>
              Last 7 days <strong>{Math.round(last7)} min</strong> · 7 before{" "}
              {Math.round(previous7)} min · 28-day average{" "}
              <strong>{Math.round(perWeek28)} min/wk</strong> · plan{" "}
              {Math.round(planMinutes)} min/wk
            </Typography>
            <Typography
              variant="caption"
              sx={{ color: "text.secondary", display: "block", mb: 1 }}
            >
              Solid line: each day&apos;s last 7 days. Dotted line: the last 28
              days, per week.
            </Typography>
            <RollingVolumeChart days={days} planMinutes={planMinutes} />
          </>
        ) : (
          <>
            <Typography variant="body2" sx={{ color: "text.secondary", my: 1 }}>
              Endurance minutes per calendar week, Monday to Sunday, against the
              plan&apos;s {Math.round(planMinutes)}.
            </Typography>
            <VolumeChart weeks={weeks} planMinutes={planMinutes} />
          </>
        )}
      </CardContent>
    </Card>
  );
}
