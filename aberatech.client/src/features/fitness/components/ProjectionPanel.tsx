import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Grid from "@mui/material/Grid";
import MenuItem from "@mui/material/MenuItem";
import Slider from "@mui/material/Slider";
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
  fetchPrediction,
  fetchRequiredDose,
  type Prediction,
  type RequiredDose,
  type Summary,
} from "../core/api";
import {
  formatSeconds,
  kgToLb,
  lbToKg,
  metricLabel,
  monthsFromNow,
} from "../core/format";
import { ProjectionChart } from "./charts";

const MILE = 1609.344;

const GOAL_DISTANCES: Record<string, number> = {
  "run-1.5mi": 1.5 * MILE,
  "run-2mi": 2 * MILE,
  "run-5mi": 5 * MILE,
  "run-10mi": 10 * MILE,
};

/**
 * The adjustable-factors panel: dose, compliance and bodyweight in, projected
 * times and goal dates out — and the inverse, a goal and a date in, the
 * required dose out. All math happens server-side; this renders it.
 */
export default function ProjectionPanel({ summary }: { summary: Summary }) {
  const currentWeightLb = summary.settings.currentWeightKg
    ? Math.round(kgToLb(summary.settings.currentWeightKg))
    : null;

  // What the imports actually show, so the sliders start at reality rather
  // than at optimism. NN/g's calculator guidance: defaults set expectations,
  // so make them measured ones.
  const recentWeeks = summary.weeklyVolume.slice(-4);
  const measuredMinutes =
    recentWeeks.length > 0
      ? recentWeeks.reduce((total, week) => total + week.minutes, 0) /
        recentWeeks.length
      : null;
  const measuredCompliance =
    measuredMinutes !== null && summary.settings.planMinutesPerWeek > 0
      ? Math.min(
          100,
          Math.max(
            10,
            Math.round(
              (100 * measuredMinutes) / summary.settings.planMinutesPerWeek / 5,
            ) * 5,
          ),
        )
      : null;

  const easyPace = summary.trainingPaces.find((pace) => pace.zone === "E");
  const easyPaceMidSecPerKm = easyPace
    ? (easyPace.fastSecPerKm + easyPace.slowSecPerKm) / 2
    : null;

  const [weeklyHours, setWeeklyHours] = React.useState(6.75);
  const [compliance, setCompliance] = React.useState(measuredCompliance ?? 85);
  const [targetWeightLb, setTargetWeightLb] = React.useState<number | null>(
    currentWeightLb,
  );
  const [prediction, setPrediction] = React.useState<Prediction | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let cancelled = false;
    const handle = window.setTimeout(() => {
      const weightKg =
        targetWeightLb !== null && targetWeightLb !== currentWeightLb
          ? lbToKg(targetWeightLb)
          : null;
      fetchPrediction(weeklyHours, compliance / 100, weightKg)
        .then((result) => {
          if (!cancelled) {
            setPrediction(result);
            setError(null);
          }
        })
        .catch(() => {
          if (!cancelled) {
            setError("Could not compute the projection.");
          }
        });
    }, 250);
    return () => {
      cancelled = true;
      window.clearTimeout(handle);
    };
  }, [weeklyHours, compliance, targetWeightLb, currentWeightLb]);

  return (
    <Stack spacing={3}>
      <Card variant="outlined">
        <CardContent>
          <Typography variant="h6">Where you start</Typography>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Every projection below launches from your measured anchor:{" "}
            <strong>VDOT {summary.settings.startVdot.toFixed(1)}</strong>
            {summary.settings.vdotMeasuredOn
              ? ` (time trial on ${summary.settings.vdotMeasuredOn})`
              : " (default — record a fresh time trial in settings to reset it)"}
            {currentWeightLb !== null && <> at {currentWeightLb} lb</>}
            {prediction?.reclaimVdot != null && (
              <>
                . You have held{" "}
                <strong>VDOT {prediction.reclaimVdot.toFixed(1)}</strong> before
                — everything below that line comes back at the retraining rate,
                not the beginner rate
              </>
            )}
            {prediction && prediction.altitudePenaltyPercent > 0 && (
              <>
                . Times shown for your home altitude (+
                {prediction.altitudePenaltyPercent.toFixed(1)}% vs sea level)
              </>
            )}
            {prediction && (
              <>
                {" "}
                — equivalent today to a{" "}
                {formatSeconds(prediction.checkpoints[0].twoMileSeconds)} 2-mile
                and a {formatSeconds(prediction.checkpoints[0].fiveMileSeconds)}{" "}
                5-mile.
              </>
            )}
          </Typography>
        </CardContent>
      </Card>

      <Card variant="outlined">
        <CardContent>
          <Typography variant="h6" sx={{ mb: 2 }}>
            The factors you control
          </Typography>
          <Grid container spacing={4}>
            <Grid size={{ xs: 12, sm: 4 }}>
              <Typography gutterBottom variant="body2">
                Weekly endurance hours:{" "}
                <strong>{weeklyHours.toFixed(2)}</strong>
              </Typography>
              <Slider
                aria-label="Weekly endurance hours"
                min={1}
                max={12}
                step={0.25}
                value={weeklyHours}
                onChange={(_, value) => setWeeklyHours(value as number)}
              />
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Endurance sessions you plan to schedule.
                {easyPaceMidSecPerKm !== null &&
                  ` ~${Math.round((weeklyHours * 3600) / (easyPaceMidSecPerKm * 1.609))} mi/week at your Easy pace.`}
                {measuredMinutes !== null &&
                  ` Logged over your last 4 weeks: ${(measuredMinutes / 60).toFixed(1)} h/week.`}
              </Typography>
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <Typography gutterBottom variant="body2">
                Compliance: <strong>{compliance}%</strong>
              </Typography>
              <Slider
                aria-label="Compliance percent"
                min={10}
                max={100}
                step={5}
                value={compliance}
                onChange={(_, value) => setCompliance(value as number)}
              />
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                The share of planned sessions that actually happen.
                {measuredCompliance !== null
                  ? ` Measured from your imports: ~${measuredCompliance}% of the plan's ${Math.round(summary.settings.planMinutesPerWeek)} min/week.`
                  : " No imports yet to measure it from."}
              </Typography>
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <Typography gutterBottom variant="body2">
                Race weight:{" "}
                <strong>
                  {targetWeightLb === null ? "—" : `${targetWeightLb} lb`}
                </strong>
              </Typography>
              <Slider
                aria-label="Race weight in pounds"
                min={currentWeightLb ? currentWeightLb - 17 : 140}
                max={currentWeightLb ? currentWeightLb + 17 : 210}
                step={1}
                disabled={currentWeightLb === null}
                value={targetWeightLb ?? 174}
                onChange={(_, value) => setTargetWeightLb(value as number)}
              />
              {currentWeightLb === null && (
                <Typography variant="caption" sx={{ color: "text.secondary" }}>
                  Log a weigh-in on the Data tab to unlock the weight factor.
                </Typography>
              )}
            </Grid>
          </Grid>
        </CardContent>
      </Card>

      {error && <Alert severity="error">{error}</Alert>}

      {prediction && (
        <>
          <Card variant="outlined">
            <CardContent>
              <Typography variant="h6">Projected fitness</Typography>
              <Typography
                variant="body2"
                sx={{ color: "text.secondary", mb: 1 }}
              >
                Effective dose {prediction.effectiveHours.toFixed(1)} h/week,
                supporting a ceiling of VDOT {prediction.ceiling.toFixed(1)}.
              </Typography>
              <ProjectionChart
                curve={prediction.curve}
                engineVdot={43}
                reclaimVdot={prediction.reclaimVdot}
                goals={prediction.goals
                  .filter((g) => GOAL_DISTANCES[g.metric])
                  .map((g) => ({
                    vdot: g.targetVdot,
                    label: `${metricLabel(g.metric)} ${formatSeconds(
                      g.targetValue,
                    )}`,
                  }))}
              />
              <Table size="small" sx={{ mt: 1 }}>
                <TableHead>
                  <TableRow>
                    <TableCell>Horizon</TableCell>
                    <TableCell align="right">VDOT</TableCell>
                    <TableCell align="right">1.5 mile</TableCell>
                    <TableCell align="right">2 mile</TableCell>
                    <TableCell align="right">5 mile</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {prediction.checkpoints.map((point) => (
                    <TableRow key={point.months}>
                      <TableCell>
                        {point.months === 0
                          ? "Now"
                          : monthsFromNow(point.months)}
                      </TableCell>
                      <TableCell align="right">
                        {point.vdot.toFixed(1)}
                      </TableCell>
                      <TableCell align="right">
                        {formatSeconds(point.oneAndAHalfMileSeconds)}
                      </TableCell>
                      <TableCell align="right">
                        {formatSeconds(point.twoMileSeconds)}
                      </TableCell>
                      <TableCell align="right">
                        {formatSeconds(point.fiveMileSeconds)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>

          {prediction.goals.length > 0 && (
            <Card variant="outlined">
              <CardContent>
                <Typography variant="h6" sx={{ mb: 1 }}>
                  Your goals at this dose
                </Typography>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Goal</TableCell>
                      <TableCell align="right">Target</TableCell>
                      <TableCell align="right">Arrives</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {prediction.goals.map((goal) => (
                      <TableRow key={goal.metric}>
                        <TableCell>{metricLabel(goal.metric)}</TableCell>
                        <TableCell align="right">
                          {formatSeconds(goal.targetValue)}
                        </TableCell>
                        <TableCell align="right">
                          {goal.monthsToReach === null
                            ? "not at this dose"
                            : monthsFromNow(goal.monthsToReach)}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}

          <Card variant="outlined">
            <CardContent>
              <Typography variant="h6" sx={{ mb: 0.5 }}>
                Reality check
              </Typography>
              {prediction.realityCheck.measuredPacePercent === null ? (
                <Typography variant="body2" sx={{ color: "text.secondary" }}>
                  Import two or more months of runs and this compares the
                  model&apos;s slope against your actual measured improvement.
                </Typography>
              ) : (
                <Typography variant="body2" sx={{ color: "text.secondary" }}>
                  Your HR-normalized pace improved{" "}
                  <strong>
                    {prediction.realityCheck.measuredPacePercent.toFixed(1)}%
                  </strong>{" "}
                  over roughly {prediction.realityCheck.measuredOverDays} days
                  of imported runs. At the sliders above, the model projects{" "}
                  <strong>
                    {prediction.realityCheck.modelPacePercentNext90Days.toFixed(
                      1,
                    )}
                    %
                  </strong>{" "}
                  over the next 90 days. If your measured rate keeps beating the
                  model, the dates above are conservative — trust the
                  measurements.
                </Typography>
              )}
            </CardContent>
          </Card>

          <Card variant="outlined">
            <CardContent>
              <Typography variant="h6" sx={{ mb: 0.5 }}>
                Model assumptions
              </Typography>
              {prediction.assumptions.map((assumption) => (
                <Typography
                  key={assumption}
                  variant="body2"
                  sx={{ color: "text.secondary", mb: 0.5 }}
                >
                  {assumption}
                </Typography>
              ))}
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                Bracketed keys are cited in full under Sources.
              </Typography>
            </CardContent>
          </Card>
        </>
      )}

      <GoalSeek compliance={compliance} />
    </Stack>
  );
}

/** The inverse question: name the goal and the date, get the required dose. */
function GoalSeek({ compliance }: { compliance: number }) {
  const [metric, setMetric] = React.useState("run-2mi");
  const [minutes, setMinutes] = React.useState("12:15");
  const [months, setMonths] = React.useState(18);
  const [result, setResult] = React.useState<RequiredDose | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  const compute = async () => {
    const parts = minutes.split(":").map(Number);
    const seconds =
      parts.length === 2 && parts.every((p) => Number.isFinite(p))
        ? parts[0] * 60 + parts[1]
        : Number.NaN;
    if (!Number.isFinite(seconds) || seconds <= 0) {
      setError("Time must look like 12:15.");
      return;
    }

    try {
      setResult(
        await fetchRequiredDose(
          GOAL_DISTANCES[metric],
          seconds,
          months,
          compliance / 100,
        ),
      );
      setError(null);
    } catch {
      setError("Could not compute the required dose.");
    }
  };

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6" sx={{ mb: 0.5 }}>
          What would it take?
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          Pick the goal and the deadline; the model answers with the weekly dose
          that gets there from your current anchor, at the compliance set above
          ({compliance}%).
        </Typography>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          sx={{ mb: 2 }}
        >
          <TextField
            select
            label="Goal"
            value={metric}
            onChange={(event) => setMetric(event.target.value)}
            sx={{ minWidth: 160 }}
          >
            {Object.keys(GOAL_DISTANCES).map((key) => (
              <MenuItem key={key} value={key}>
                {metricLabel(key)}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            label="Target time"
            value={minutes}
            onChange={(event) => setMinutes(event.target.value)}
            sx={{ width: 140 }}
          />
          <TextField
            label="Months from now"
            type="number"
            value={months}
            onChange={(event) => setMonths(Number(event.target.value))}
            sx={{ width: 160 }}
          />
          <Box sx={{ display: "flex", alignItems: "center" }}>
            <Button variant="contained" onClick={compute}>
              Compute
            </Button>
          </Box>
        </Stack>
        {error && <Alert severity="error">{error}</Alert>}
        {result && (
          <Alert
            severity={
              result.requiredEffectiveHours === null ? "warning" : "info"
            }
          >
            From VDOT {result.startVdot.toFixed(1)} to{" "}
            {result.targetVdot.toFixed(1)}:{" "}
            {result.requiredWeeklyHoursAtCompliance !== null &&
              result.requiredEffectiveHours !== null &&
              result.requiredEffectiveHours > 0 && (
                <>
                  Requires about{" "}
                  <strong>
                    {result.requiredWeeklyHoursAtCompliance.toFixed(1)} planned
                    hours/week
                  </strong>{" "}
                  ({result.requiredEffectiveHours.toFixed(1)} effective).{" "}
                </>
              )}
            {result.verdict}
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}
