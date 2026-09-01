import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import LinearProgress from "@mui/material/LinearProgress";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import {
  type Feasibility,
  fetchFeasibility,
  saveGoal,
  type Verdict,
} from "../core/api";
import {
  type DistanceUnit,
  dateInMonths,
  formatChance,
  formatDistance,
  formatSeconds,
  goalKey,
  monthsFromNow,
  monthsUntil,
  parseClock,
  toMeters,
} from "../core/format";
import MathPanel from "./MathPanel";

const SEVERITY: Record<Verdict, "success" | "info" | "warning" | "error"> = {
  AlreadyThere: "success",
  Reachable: "success",
  MoreHoursThanYouHave: "warning",
  NotByThatDate: "warning",
  PastAnyTrainingCeiling: "error",
  PastTheAgeGradedRecord: "error",
  PastTheWorldRecord: "error",
};

/** Common distances as a starting point, not as the list of what is allowed. */
const SHORTCUTS: { label: string; value: number; unit: DistanceUnit }[] = [
  { label: "1.5 mi", value: 1.5, unit: "mi" },
  { label: "2 mi", value: 2, unit: "mi" },
  { label: "5 km", value: 5, unit: "km" },
  { label: "5 mi", value: 5, unit: "mi" },
  { label: "10 km", value: 10, unit: "km" },
  { label: "half", value: 21.0975, unit: "km" },
];

/**
 * The inverse question: name any distance, any time and any date, and get back
 * the wall it hits or the week it needs.
 */
export default function GoalSeek({
  availableHours,
  onGoalSaved,
}: {
  availableHours: number;
  onGoalSaved: () => void;
}) {
  const [distance, setDistance] = React.useState("5");
  const [unit, setUnit] = React.useState<DistanceUnit>("mi");
  const [time, setTime] = React.useState("36:00");
  const [date, setDate] = React.useState(dateInMonths(24));
  const [hours, setHours] = React.useState(availableHours);
  const [result, setResult] = React.useState<Feasibility | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [saved, setSaved] = React.useState(false);

  const meters = Number(distance) > 0 ? toMeters(Number(distance), unit) : null;
  const seconds = parseClock(time);
  const months = monthsUntil(date);

  const compute = async () => {
    if (meters === null || meters < 400 || meters > 100_000) {
      setError("Distance must be between 400 m and 100 km.");
      return;
    }
    if (seconds === null) {
      setError("Target time should look like 36:00 or 1:22:30.");
      return;
    }
    if (months <= 0) {
      setError("Pick a date in the future.");
      return;
    }

    try {
      setResult(await fetchFeasibility(meters, seconds, months, hours));
      setError(null);
      setSaved(false);
    } catch {
      setError("Could not compute the answer.");
    }
  };

  const keep = async () => {
    if (meters === null || seconds === null) return;
    await saveGoal({
      metric: goalKey(meters),
      targetValue: seconds,
      targetDate: date,
      distanceMeters: meters,
      label: `${formatDistance(meters)} in ${formatSeconds(seconds)}`,
    });
    setSaved(true);
    onGoalSaved();
  };

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6" sx={{ mb: 0.5 }}>
          What would it take?
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          Any distance, any time, any date. The answer names the constraint that
          decides it — the record book, your trainable ceiling, the calendar, or
          the hours in your week — and shows the arithmetic.
        </Typography>

        <Stack
          direction="row"
          spacing={1}
          sx={{ mb: 2, flexWrap: "wrap", rowGap: 1 }}
        >
          {SHORTCUTS.map((shortcut) => (
            <Chip
              key={shortcut.label}
              label={shortcut.label}
              size="small"
              variant={
                unit === shortcut.unit && Number(distance) === shortcut.value
                  ? "filled"
                  : "outlined"
              }
              onClick={() => {
                setDistance(String(shortcut.value));
                setUnit(shortcut.unit);
              }}
            />
          ))}
        </Stack>

        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          sx={{ mb: 2 }}
        >
          <TextField
            label="Distance"
            type="number"
            value={distance}
            onChange={(event) => setDistance(event.target.value)}
            sx={{ width: 120 }}
            slotProps={{ htmlInput: { min: 0, step: 0.1 } }}
          />
          <TextField
            select
            label="Unit"
            value={unit}
            onChange={(event) => setUnit(event.target.value as DistanceUnit)}
            sx={{ width: 100 }}
          >
            <MenuItem value="mi">miles</MenuItem>
            <MenuItem value="km">km</MenuItem>
            <MenuItem value="m">metres</MenuItem>
          </TextField>
          <TextField
            label="Target time"
            value={time}
            onChange={(event) => setTime(event.target.value)}
            sx={{ width: 140 }}
          />
          <TextField
            label="By"
            type="date"
            value={date}
            onChange={(event) => setDate(event.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
            sx={{ width: 170 }}
          />
          <TextField
            label="Hours you can give"
            type="number"
            value={hours}
            onChange={(event) => setHours(Number(event.target.value))}
            slotProps={{ htmlInput: { min: 0, max: 40, step: 0.5 } }}
            sx={{ width: 170 }}
          />
          <Box sx={{ display: "flex", alignItems: "center" }}>
            <Button variant="contained" onClick={compute}>
              Work it out
            </Button>
          </Box>
        </Stack>

        {error && <Alert severity="error">{error}</Alert>}
        {result && <Answer result={result} onKeep={keep} saved={saved} />}
      </CardContent>
    </Card>
  );
}

function Answer({
  result,
  onKeep,
  saved,
}: {
  result: Feasibility;
  onKeep: () => void;
  saved: boolean;
}) {
  return (
    <Stack spacing={2}>
      <Alert severity={SEVERITY[result.verdict]}>
        <Typography variant="subtitle2">{result.headline}</Typography>
        <Typography variant="body2">{result.detail}</Typography>
      </Alert>

      <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", rowGap: 1 }}>
        <Chip
          size="small"
          color="default"
          label={`Binding constraint: ${result.bindingConstraint}`}
        />
        <Chip
          size="small"
          label={`Target VDOT ${result.targetVdot.toFixed(1)}`}
        />
        <Chip
          size="small"
          label={`You are at ${result.startVdot.toFixed(1)}`}
        />
        {result.achievableSecondsByDate !== null && (
          <Chip
            size="small"
            label={`Best by that date: ${formatSeconds(result.achievableSecondsByDate)}`}
          />
        )}
      </Stack>

      <Box>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Where the target sits:{" "}
          <strong>{(result.grade * 100).toFixed(0)}%</strong> of the age-graded
          record — {result.gradeBand}. A record-level run over this distance is{" "}
          {formatSeconds(result.recordEquivalentSeconds)} ({result.recordHolder}
          's standard).
        </Typography>
        <LinearProgress
          variant="determinate"
          value={Math.min(100, result.grade * 100)}
          sx={{ mt: 0.5, height: 8, borderRadius: 4 }}
          aria-label="Target as a share of the age-graded record"
        />
      </Box>

      {result.prescription && (
        <Box>
          <Typography variant="subtitle2" gutterBottom>
            The week it needs
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Zone</TableCell>
                <TableCell align="right">Hours/week</TableCell>
                <TableCell align="right">Recovery cost</TableCell>
                <TableCell align="right">Next hour buys</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {result.prescription.dose.zones
                .filter((zone) => zone.hours > 0)
                .map((zone) => (
                  <TableRow key={zone.zone}>
                    <TableCell>{zone.zone}</TableCell>
                    <TableCell align="right">{zone.hours.toFixed(2)}</TableCell>
                    <TableCell align="right">
                      {zone.strain.toFixed(1)}
                    </TableCell>
                    <TableCell align="right">
                      +{zone.marginalVdotPerHour.toFixed(2)} VDOT
                    </TableCell>
                  </TableRow>
                ))}
            </TableBody>
          </Table>
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {result.prescription.weeklyMiles !== null && (
              <>
                About {result.prescription.weeklyMiles.toFixed(0)} miles a week
                of easy running at your current easy pace.{" "}
              </>
            )}
            Built from your logged week over about{" "}
            {result.prescription.rampMonths.toFixed(1)} months.
          </Typography>
        </Box>
      )}

      <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", rowGap: 1 }}>
        <Chip
          size="small"
          color={result.probabilityByDate >= 0.5 ? "success" : "warning"}
          label={`Chance by your date: ${formatChance(result.probabilityByDate)}`}
        />
        {result.monthsForEvenOdds !== null && (
          <Chip
            size="small"
            label={`Even odds around ${monthsFromNow(result.monthsForEvenOdds)}`}
          />
        )}
        {result.earliestMonths !== null && (
          <Chip
            size="small"
            label={`Earliest at full-time volume: ${monthsFromNow(result.earliestMonths)}`}
          />
        )}
        {result.monthsAtHoursAvailable !== null && (
          <Chip
            size="small"
            label={`On the hours you have: ${monthsFromNow(result.monthsAtHoursAvailable)}`}
          />
        )}
      </Stack>

      <Box>
        <Button
          size="small"
          variant="outlined"
          onClick={onKeep}
          disabled={saved}
        >
          {saved ? "Kept as a goal" : "Keep this as a goal"}
        </Button>
      </Box>

      <MathPanel steps={result.steps} />
    </Stack>
  );
}
