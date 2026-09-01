import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import IconButton from "@mui/material/IconButton";
import Slider from "@mui/material/Slider";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import * as React from "react";
import {
  deleteGoal,
  fetchPrediction,
  type Prediction,
  type Summary,
} from "../core/api";
import {
  formatChance,
  formatDistance,
  formatSeconds,
  kgToLb,
  lbToKg,
  monthsFromNow,
  toMeters,
} from "../core/format";
import { ProjectionChart } from "./charts";
import GoalSeek from "./GoalSeek";
import MathPanel from "./MathPanel";
import NumberChips from "./NumberChips";
import PlanEditor, {
  type PlanState,
  planFromDose,
  toRequest,
} from "./PlanEditor";

const MILE = 1609.344;
const DEFAULT_DISTANCES = [1.5 * MILE, 2 * MILE, 5 * MILE];
const DEFAULT_HORIZONS = [0, 3, 6, 12, 18, 24];

/**
 * The prediction console: a training week in, projected performance with its
 * uncertainty out — and the inverse, a goal in, the week it needs out. All of
 * the arithmetic happens server-side; this renders it, including the workings.
 */
export default function ProjectionPanel({
  summary,
  onGoalsChanged,
}: {
  summary: Summary;
  onGoalsChanged: () => void;
}) {
  const currentWeightLb = summary.settings.currentWeightKg
    ? Math.round(kgToLb(summary.settings.currentWeightKg))
    : null;

  const [plan, setPlan] = React.useState<PlanState>(() =>
    summary.measuredDose.runningHours > 0.5
      ? planFromDose(summary.measuredDose, "total")
      : {
          mode: "total",
          totalHours: 6.75,
          easyHours: 5.4,
          thresholdHours: 0.8,
          intervalHours: 0.55,
          strengthHours: summary.measuredDose.strengthHours,
        },
  );
  const [compliance, setCompliance] = React.useState(85);
  // Opens on the weight the athlete is actually training towards, which they
  // have now told us, rather than on today's — and survives a reload.
  const goalWeightLb = summary.settings.goalWeightKg
    ? Math.round(kgToLb(summary.settings.goalWeightKg))
    : null;
  const [targetWeightLb, setTargetWeightLb] = React.useState<number | null>(
    goalWeightLb ?? currentWeightLb,
  );
  const [distances, setDistances] = React.useState(DEFAULT_DISTANCES);
  const [horizons, setHorizons] = React.useState(DEFAULT_HORIZONS);
  const [prediction, setPrediction] = React.useState<Prediction | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let cancelled = false;
    const handle = window.setTimeout(() => {
      const weightKg =
        targetWeightLb !== null && targetWeightLb !== currentWeightLb
          ? lbToKg(targetWeightLb)
          : null;
      fetchPrediction(
        toRequest(plan),
        compliance / 100,
        weightKg,
        distances,
        horizons,
      )
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
  }, [plan, compliance, targetWeightLb, currentWeightLb, distances, horizons]);

  return (
    <Stack spacing={3}>
      <Anchor summary={summary} prediction={prediction} />

      <PlanEditor
        plan={plan}
        onChange={setPlan}
        compliance={compliance}
        onComplianceChange={setCompliance}
        prediction={prediction}
        measured={summary.measuredDose}
      />

      {currentWeightLb !== null && (
        <Card variant="outlined">
          <CardContent>
            <Typography gutterBottom variant="body2">
              Race weight:{" "}
              <strong>
                {targetWeightLb === null ? "—" : `${targetWeightLb} lb`}
              </strong>
            </Typography>
            <Slider
              aria-label="Race weight in pounds"
              min={Math.round(
                currentWeightLb *
                  (1 - summary.settings.maxWeightAdjustmentFraction),
              )}
              max={Math.round(
                currentWeightLb *
                  (1 + summary.settings.maxWeightAdjustmentFraction),
              )}
              step={1}
              value={targetWeightLb ?? currentWeightLb}
              onChange={(_, value) => setTargetWeightLb(value as number)}
              sx={{ maxWidth: 420 }}
            />
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              Relative VO2max scales with the inverse of body mass, so shed fat
              mass moves both your current fitness and the lifetime best you can
              reclaim — that best was itself run at a bodyweight. The range is
              the ±
              {Math.round(summary.settings.maxWeightAdjustmentFraction * 100)}%
              the model will honour; past that the fat-mass-only assumption has
              nothing behind it.
              {summary.settings.pastPeakWeightKg === null && (
                <>
                  {" "}
                  Record what your lifetime best was run at, on the Data tab,
                  and the ceiling will follow this slider too.
                </>
              )}
            </Typography>
          </CardContent>
        </Card>
      )}

      {error && <Alert severity="error">{error}</Alert>}

      {prediction && (
        <>
          <Card variant="outlined">
            <CardContent>
              <Typography variant="h6">Projected fitness</Typography>
              {targetWeightLb !== null &&
                currentWeightLb !== null &&
                targetWeightLb !== currentWeightLb && (
                  <Alert severity="info" sx={{ my: 1 }}>
                    Every number below assumes you race at {targetWeightLb} lb,
                    not today's {currentWeightLb} lb
                    {summary.settings.pastPeakWeightKg === null
                      ? " — and only your current fitness is adjusted for it, because the weight your lifetime best was run at is not recorded."
                      : "."}
                  </Alert>
                )}
              <Typography
                variant="body2"
                sx={{ color: "text.secondary", mb: 1 }}
              >
                The shaded band is the 80% interval — the model's own
                uncertainty, from fitting it to your history, not a decoration.
              </Typography>
              <ProjectionChart
                curve={prediction.curve}
                engineVdot={43}
                reclaimVdot={prediction.reclaimVdot}
                goals={prediction.goals.map((goal) => ({
                  vdot: goal.targetVdot,
                  label: `${goal.label}`,
                }))}
              />

              <Stack spacing={1} sx={{ my: 2 }}>
                <NumberChips
                  label="Add distance"
                  placeholder="e.g. 10 km"
                  values={distances}
                  format={formatDistance}
                  parse={parseDistance}
                  onChange={setDistances}
                />
                <NumberChips
                  label="Add horizon"
                  placeholder="months"
                  values={horizons}
                  format={(m) => (m === 0 ? "now" : `+${m}mo`)}
                  parse={(text) => {
                    const value = Number(text);
                    return Number.isFinite(value) && value >= 0 && value <= 120
                      ? value
                      : null;
                  }}
                  max={12}
                  onChange={setHorizons}
                />
              </Stack>

              <Box sx={{ overflowX: "auto" }}>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Horizon</TableCell>
                      <TableCell align="right">VDOT (80% range)</TableCell>
                      {distances.map((meters) => (
                        <TableCell key={meters} align="right">
                          {formatDistance(meters)}
                        </TableCell>
                      ))}
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
                          {point.months > 0 && (
                            <Typography
                              component="span"
                              variant="caption"
                              sx={{ color: "text.secondary" }}
                            >
                              {" "}
                              ({point.low.toFixed(1)}–{point.high.toFixed(1)})
                            </Typography>
                          )}
                        </TableCell>
                        {point.races.map((race) => (
                          <TableCell key={race.distanceMeters} align="right">
                            {formatSeconds(race.seconds)}
                            {point.months > 0 && (
                              <Typography
                                component="span"
                                variant="caption"
                                sx={{ color: "text.secondary" }}
                              >
                                {" "}
                                ({formatSeconds(race.fastSeconds)}–
                                {formatSeconds(race.slowSeconds)})
                              </Typography>
                            )}
                          </TableCell>
                        ))}
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>

              <MathPanel steps={prediction.steps} />
            </CardContent>
          </Card>

          {prediction.goals.length > 0 && (
            <Card variant="outlined">
              <CardContent>
                <Typography variant="h6" sx={{ mb: 1 }}>
                  Your goals at this week
                </Typography>
                <Box sx={{ overflowX: "auto" }}>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>Goal</TableCell>
                        <TableCell align="right">Target</TableCell>
                        <TableCell align="right">Date</TableCell>
                        <TableCell align="right">Arrives</TableCell>
                        <TableCell align="right">Chance</TableCell>
                        <TableCell>Verdict</TableCell>
                        <TableCell />
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {prediction.goals.map((goal) => (
                        <TableRow key={goal.metric}>
                          <TableCell>{goal.label}</TableCell>
                          <TableCell align="right">
                            {formatSeconds(goal.targetValue)}
                          </TableCell>
                          <TableCell align="right">{goal.targetDate}</TableCell>
                          <TableCell align="right">
                            {goal.monthsToReach === null
                              ? "not at this week"
                              : monthsFromNow(goal.monthsToReach)}
                          </TableCell>
                          <TableCell align="right">
                            {formatChance(goal.probability)}
                          </TableCell>
                          <TableCell sx={{ color: "text.secondary" }}>
                            {goal.headline}
                          </TableCell>
                          <TableCell align="right">
                            <IconButton
                              size="small"
                              aria-label={`Delete ${goal.label}`}
                              onClick={() =>
                                deleteGoal(goal.metric).then(onGoalsChanged)
                              }
                            >
                              ×
                            </IconButton>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </Box>
              </CardContent>
            </Card>
          )}

          <FitCard prediction={prediction} />

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

      <GoalSeek
        availableHours={summary.settings.availableHoursPerWeek}
        onGoalSaved={onGoalsChanged}
      />
    </Stack>
  );
}

/** "5 mi", "10k", "3000 m", "1.5" — a distance the way it gets typed. */
export function parseDistance(text: string): number | null {
  const match = text
    .trim()
    .toLowerCase()
    .match(/^(\d*\.?\d+)\s*(mi|mile|miles|km|k|m|metres|meters)?$/);
  if (!match) return null;

  const value = Number(match[1]);
  if (!Number.isFinite(value) || value <= 0) return null;

  switch (match[2]) {
    case "km":
    case "k":
      return toMeters(value, "km");
    case "m":
    case "metres":
    case "meters":
      return toMeters(value, "m");
    default:
      return toMeters(value, "mi");
  }
}

function Anchor({
  summary,
  prediction,
}: {
  summary: Summary;
  prediction: Prediction | null;
}) {
  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">Where you start</Typography>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Every projection below launches from your measured anchor:{" "}
          <strong>VDOT {summary.settings.startVdot.toFixed(1)}</strong>
          {summary.settings.vdotMeasuredOn
            ? ` (time trial on ${summary.settings.vdotMeasuredOn})`
            : " (default — record a fresh time trial in settings to reset it)"}
          {prediction?.reclaimVdot != null && (
            <>
              . You have held{" "}
              <strong>VDOT {prediction.reclaimVdot.toFixed(1)}</strong> before —
              everything below that line comes back at the retraining rate, not
              the beginner rate
            </>
          )}
          {prediction && prediction.altitudePenaltyPercent > 0 && (
            <>
              . Times shown for your home altitude (+
              {prediction.altitudePenaltyPercent.toFixed(1)}% vs sea level)
            </>
          )}
          .
        </Typography>
        <MathPanel
          steps={summary.measuredDoseSteps}
          title="How your current week was read out of the log"
        />
      </CardContent>
    </Card>
  );
}

function FitCard({ prediction }: { prediction: Prediction }) {
  const fit = prediction.fit;
  const fitted = fit.observations >= 4;

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6" sx={{ mb: 0.5 }}>
          The model, fitted to you
        </Typography>
        {fitted ? (
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Fitted to {fit.observations} months of your imported runs by
            penalised least squares. It explains{" "}
            <strong>{(fit.rSquared * 100).toFixed(0)}%</strong> of the variance
            in your measured fitness, with a residual spread of{" "}
            {fit.residualSd.toFixed(1)} VDOT. Your approach rate is{" "}
            <strong>
              {fit.ratePerMonth.toFixed(3)} ± {fit.rateStandardError.toFixed(3)}
            </strong>{" "}
            per month and your responsiveness{" "}
            <strong>
              {fit.responsiveness.toFixed(2)} ±{" "}
              {fit.responsivenessStandardError.toFixed(2)}
            </strong>{" "}
            times the reference athlete — {(fit.dataWeight * 100).toFixed(0)}%
            of that from your own data rather than the literature priors.
          </Typography>
        ) : (
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Only {fit.observations} month
            {fit.observations === 1 ? "" : "s"} of imported runs, fewer than the
            four a three-parameter fit needs, so the rate and responsiveness
            above are the literature's rather than yours. Import more history
            and every band on this page narrows.
          </Typography>
        )}
        <Stack
          direction="row"
          spacing={1}
          sx={{ mt: 1, flexWrap: "wrap", rowGap: 1 }}
        >
          <Chip
            size="small"
            label={`Measured improvement: ${
              prediction.realityCheck.measuredPacePercent === null
                ? "not enough history"
                : `${prediction.realityCheck.measuredPacePercent.toFixed(1)}% over ${prediction.realityCheck.measuredOverDays} days`
            }`}
          />
          <Chip
            size="small"
            label={`Model over the next 90 days: ${prediction.realityCheck.modelPacePercentNext90Days.toFixed(1)}%`}
          />
        </Stack>
        <MathPanel steps={fit.steps} title="Show the fit" />
      </CardContent>
    </Card>
  );
}
