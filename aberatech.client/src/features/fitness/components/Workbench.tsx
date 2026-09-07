import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import FormControlLabel from "@mui/material/FormControlLabel";
import Grid from "@mui/material/Grid";
import MenuItem from "@mui/material/MenuItem";
import Radio from "@mui/material/Radio";
import Stack from "@mui/material/Stack";
import Switch from "@mui/material/Switch";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import {
  type FactorName,
  fetchLockedPredictions,
  fetchMeasurementPlan,
  fetchSurface,
  type LockedPrediction,
  lockPrediction,
  type MeasurePlan,
  type ScenarioRequest,
  type SolveResult,
  type SpreadValue,
  type Summary,
  type SurfaceResult,
  solve,
} from "../core/api";
import {
  dateInMonths,
  formatChance,
  formatDistance,
  formatSeconds,
  kgToLb,
  lbToKg,
  parseClock,
} from "../core/format";
import ContourPlot from "./ContourPlot";
import FanChart from "./FanChart";
import MathPanel from "./MathPanel";
import PredictionLedger from "./PredictionLedger";
import { parseDistance } from "./ProjectionPanel";
import Tornado from "./Tornado";

/** What the workbench can be asked to work out. */
const UNKNOWNS: {
  key: FactorName | "RaceTime";
  label: string;
  hint: string;
}[] = [
  { key: "RaceTime", label: "Race time", hint: "everything else fixed" },
  { key: "WeeklyHours", label: "Weekly hours", hint: "hours the target needs" },
  { key: "Compliance", label: "Compliance", hint: "share of sessions kept" },
  { key: "Months", label: "Date", hint: "when it arrives" },
  { key: "RaceMassKg", label: "Race weight", hint: "weight it would take" },
  {
    key: "StrengthHours",
    label: "Strength hours",
    hint: "lifting the target needs",
  },
];

interface WorkbenchState {
  distanceMeters: number;
  months: number;
  weeklyHours: number;
  compliance: number;
  raceMassLb: number | null;
  strengthHours: number;
  targetSeconds: number;
  startHours: number | null;
  rampPercent: number;
  useHistory: boolean;
}

function toRequest(state: WorkbenchState): ScenarioRequest {
  return {
    distanceMeters: state.distanceMeters,
    months: state.months,
    weeklyHours: state.weeklyHours,
    compliance: state.compliance,
    raceMassKg: state.raceMassLb === null ? null : lbToKg(state.raceMassLb),
    strengthHours: state.strengthHours,
    startHours: state.startHours,
    rampPerWeek: state.rampPercent / 100,
    useHistory: state.useHistory,
  };
}

/** A solved factor, in the units the input for it uses. */
function show(factor: FactorName, spread: SpreadValue): string {
  const unit = (value: number) => {
    switch (factor) {
      case "Compliance":
        return `${Math.round(value * 100)}%`;
      case "Months":
        return `${value.toFixed(1)} months`;
      case "RaceMassKg":
        return `${kgToLb(value).toFixed(0)} lb`;
      default:
        return `${value.toFixed(1)} h/week`;
    }
  };

  if (Number.isNaN(spread.median)) return "nothing reaches it";
  return `${unit(spread.median)}  (${unit(spread.low)} – ${unit(spread.high)})`;
}

/**
 * The what-if workbench: state every factor, mark the one to work out, and get
 * back a distribution rather than a number — plus the derivatives that say
 * which factor to move next and the field that shows what moving two at once
 * would do.
 */
export default function Workbench({ summary }: { summary: Summary }) {
  const currentMassLb = summary.settings.currentWeightKg
    ? Math.round(kgToLb(summary.settings.currentWeightKg))
    : null;

  const [unknown, setUnknown] = React.useState<FactorName | "RaceTime">(
    "WeeklyHours",
  );
  const [state, setState] = React.useState<WorkbenchState>({
    distanceMeters: 5 * 1609.344,
    months: 18,
    weeklyHours: Math.max(
      3,
      Number(summary.measuredDose.runningHours.toFixed(1)) || 6,
    ),
    compliance: 0.85,
    raceMassLb:
      (summary.settings.goalWeightKg
        ? Math.round(kgToLb(summary.settings.goalWeightKg))
        : null) ?? currentMassLb,
    strengthHours: Number(summary.measuredDose.strengthHours.toFixed(1)),
    targetSeconds: 34 * 60,
    // Nothing here is read off the log. A projection is of a plan, and the
    // plan starts when it starts.
    startHours: null,
    rampPercent: 0,
    useHistory: false,
  });

  const [result, setResult] = React.useState<SolveResult | null>(null);
  const [surface, setSurface] = React.useState<SurfaceResult | null>(null);
  const [plan, setPlan] = React.useState<MeasurePlan | null>(null);
  const [across, setAcross] = React.useState<FactorName>("WeeklyHours");
  const [down, setDown] = React.useState<FactorName>("Months");
  const [busy, setBusy] = React.useState(false);
  // Which scenario the locked prediction was for. Derived rather than reset by
  // an effect: change any factor and the button honestly offers itself again,
  // because it would now be writing down a different claim.
  const [lockedSignature, setLockedSignature] = React.useState<string | null>(
    null,
  );
  const [ledger, setLedger] = React.useState<LockedPrediction[] | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  const signature = `${JSON.stringify(toRequest(state))}|${unknown}`;
  const locked = lockedSignature === signature;

  const reloadLedger = React.useCallback(() => {
    fetchLockedPredictions()
      .then(setLedger)
      .catch(() => undefined);
  }, []);

  React.useEffect(reloadLedger, [reloadLedger]);

  const set = <K extends keyof WorkbenchState>(
    key: K,
    value: WorkbenchState[K],
  ) => setState((previous) => ({ ...previous, [key]: value }));

  // The factor being solved for is an output, so it cannot also be an axis to
  // drag along — picking a value for it would be picking the answer.
  const axisChoices = (
    [
      "WeeklyHours",
      "Compliance",
      "Months",
      "RaceMassKg",
      "StrengthHours",
    ] as FactorName[]
  ).filter(
    (factor) =>
      factor !== unknown &&
      !(factor === "RaceMassKg" && currentMassLb === null),
  );
  const plotAcross = axisChoices.includes(across) ? across : axisChoices[0];
  const plotDown =
    axisChoices.includes(down) && down !== plotAcross
      ? down
      : (axisChoices.find((factor) => factor !== plotAcross) ?? plotAcross);

  React.useEffect(() => {
    let cancelled = false;
    const handle = window.setTimeout(() => {
      setBusy(true);
      const request = toRequest(state);
      solve(
        request,
        unknown === "RaceTime" ? null : unknown,
        unknown === "RaceTime" ? null : state.targetSeconds,
      )
        .then((answer) => {
          if (!cancelled) {
            setResult(answer);
            setError(null);
          }
        })
        .catch(() => !cancelled && setError("Could not work that out."))
        .finally(() => !cancelled && setBusy(false));
    }, 350);
    return () => {
      cancelled = true;
      window.clearTimeout(handle);
    };
  }, [state, unknown]);

  React.useEffect(() => {
    let cancelled = false;
    const handle = window.setTimeout(() => {
      fetchSurface(
        toRequest(state),
        plotAcross,
        plotDown,
        unknown === "RaceTime" ? null : state.targetSeconds,
      )
        .then((grid) => !cancelled && setSurface(grid))
        .catch(() => undefined);
    }, 450);
    return () => {
      cancelled = true;
      window.clearTimeout(handle);
    };
  }, [state, plotAcross, plotDown, unknown]);

  React.useEffect(() => {
    let cancelled = false;
    const handle = window.setTimeout(() => {
      fetchMeasurementPlan(toRequest(state))
        .then((next) => !cancelled && setPlan(next))
        .catch(() => undefined);
    }, 600);
    return () => {
      cancelled = true;
      window.clearTimeout(handle);
    };
  }, [state]);

  const factorValue = (factor: FactorName): number => {
    switch (factor) {
      case "WeeklyHours":
        return state.weeklyHours;
      case "Compliance":
        return state.compliance;
      case "Months":
        return state.months;
      case "RaceMassKg":
        return state.raceMassLb === null ? 0 : lbToKg(state.raceMassLb);
      default:
        return state.strengthHours;
    }
  };

  // Picked off a chart, a factor arrives as whatever float the pixel worked
  // out to. Rounding it to the precision the input actually offers is the
  // difference between "9.4 h" and "9.35351589376634".
  const setFactor = (factor: FactorName, value: number) => {
    const round = (x: number, places: number) =>
      Math.round(x * 10 ** places) / 10 ** places;

    if (factor === "WeeklyHours")
      set("weeklyHours", Math.max(0, round(value, 1)));
    else if (factor === "Compliance")
      set("compliance", Math.min(1, Math.max(0.05, round(value, 2))));
    else if (factor === "Months") set("months", Math.max(0.5, round(value, 1)));
    else if (factor === "RaceMassKg")
      set("raceMassLb", Math.round(kgToLb(value)));
    else set("strengthHours", Math.max(0, round(value, 1)));
  };

  return (
    <Stack spacing={3}>
      <Card variant="outlined">
        <CardContent>
          <Typography variant="h6">What do you want to work out?</Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
            Set every factor, then mark the one to solve for. The answer comes
            back as a range because it is solved once for each of{" "}
            {result?.model.draws ?? "several thousand"} posterior draws — the
            model's own uncertainty, not a decoration.
          </Typography>

          <Grid container spacing={2}>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <TextField
                fullWidth
                size="small"
                label="Distance"
                defaultValue={formatDistance(state.distanceMeters)}
                helperText="any distance, e.g. 5 mi or 10 km"
                onBlur={(event) => {
                  const parsed = parseDistance(event.target.value);
                  if (parsed !== null) set("distanceMeters", parsed);
                }}
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <TextField
                fullWidth
                size="small"
                label="Target time"
                defaultValue={formatSeconds(state.targetSeconds)}
                disabled={unknown === "RaceTime"}
                helperText={
                  unknown === "RaceTime"
                    ? "not needed — this is the answer"
                    : "e.g. 34:00"
                }
                onBlur={(event) => {
                  const parsed = parseClock(event.target.value);
                  if (parsed !== null) set("targetSeconds", parsed);
                }}
              />
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Solve for
              </Typography>
              <Stack direction="row" sx={{ flexWrap: "wrap" }}>
                {UNKNOWNS.map((option) => (
                  <Chip
                    key={option.key}
                    size="small"
                    label={option.label}
                    variant={unknown === option.key ? "filled" : "outlined"}
                    color={unknown === option.key ? "primary" : "default"}
                    onClick={() => setUnknown(option.key)}
                    sx={{ mr: 0.5, mb: 0.5 }}
                    disabled={
                      option.key === "RaceMassKg" && currentMassLb === null
                    }
                  />
                ))}
              </Stack>
            </Grid>
          </Grid>

          <Grid container spacing={2} sx={{ mt: 1 }}>
            {UNKNOWNS.filter((o) => o.key !== "RaceTime").map((option) => {
              const factor = option.key as FactorName;
              const solving = unknown === factor;
              const disabled =
                factor === "RaceMassKg" && currentMassLb === null;

              return (
                <Grid size={{ xs: 12, sm: 6, md: 3 }} key={factor}>
                  <Card
                    variant="outlined"
                    sx={{
                      borderColor: solving ? "primary.main" : undefined,
                      opacity: disabled ? 0.5 : 1,
                    }}
                  >
                    <CardContent sx={{ pb: 1.5 }}>
                      <Stack
                        direction="row"
                        sx={{ alignItems: "center", mb: 0.5 }}
                      >
                        <Radio
                          size="small"
                          checked={solving}
                          disabled={disabled}
                          onChange={() => setUnknown(factor)}
                          slotProps={{
                            input: {
                              "aria-label": `Solve for ${option.label}`,
                            },
                          }}
                        />
                        <Typography variant="body2">{option.label}</Typography>
                      </Stack>
                      {solving ? (
                        <Typography
                          variant="body2"
                          sx={{ color: "primary.main", minHeight: 40 }}
                        >
                          <strong>
                            {result?.solved ? show(factor, result.solved) : "…"}
                          </strong>
                        </Typography>
                      ) : (
                        <TextField
                          fullWidth
                          size="small"
                          type="number"
                          disabled={disabled}
                          value={
                            factor === "Compliance"
                              ? Math.round(state.compliance * 100)
                              : factor === "RaceMassKg"
                                ? (state.raceMassLb ?? "")
                                : factorValue(factor)
                          }
                          slotProps={{
                            htmlInput: {
                              step: factor === "Compliance" ? 5 : 0.5,
                            },
                          }}
                          onChange={(event) => {
                            const raw = Number(event.target.value);
                            if (factor === "Compliance")
                              set("compliance", raw / 100);
                            else if (factor === "RaceMassKg")
                              set("raceMassLb", raw);
                            else setFactor(factor, raw);
                          }}
                        />
                      )}
                      <Typography
                        variant="caption"
                        sx={{ color: "text.secondary" }}
                      >
                        {solving ? option.hint : ""}
                      </Typography>
                    </CardContent>
                  </Card>
                </Grid>
              );
            })}
          </Grid>
        </CardContent>
      </Card>

      <Card variant="outlined">
        <CardContent>
          <Typography variant="h6">Where the plan starts</Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
            Stated, not inferred. Nothing below is read off your imports — a
            training log records what you have done, and a projection is of what
            you are going to do.
          </Typography>

          <Stack direction="row" sx={{ flexWrap: "wrap", mb: 2 }}>
            <Chip
              size="small"
              label="Start the plan as written"
              variant={state.rampPercent === 0 ? "filled" : "outlined"}
              color={state.rampPercent === 0 ? "primary" : "default"}
              onClick={() => setState((p) => ({ ...p, rampPercent: 0 }))}
              sx={{ mr: 0.5, mb: 0.5 }}
            />
            <Chip
              size="small"
              label="Build up to it"
              variant={state.rampPercent > 0 ? "filled" : "outlined"}
              color={state.rampPercent > 0 ? "primary" : "default"}
              onClick={() =>
                setState((p) => ({
                  ...p,
                  rampPercent: p.rampPercent > 0 ? p.rampPercent : 8,
                  startHours: p.startHours ?? 2,
                }))
              }
              sx={{ mr: 0.5, mb: 0.5 }}
            />
          </Stack>

          {state.rampPercent > 0 && (
            <Stack
              direction={{ xs: "column", sm: "row" }}
              spacing={2}
              sx={{ mb: 2 }}
            >
              <TextField
                size="small"
                type="number"
                label="Starting at (h/week)"
                value={state.startHours ?? 0}
                slotProps={{ htmlInput: { min: 0, max: 40, step: 0.5 } }}
                onChange={(event) =>
                  set("startHours", Math.max(0, Number(event.target.value)))
                }
                helperText={
                  summary.measuredDose.runningHours > 0
                    ? `Your log shows ${summary.measuredDose.runningHours.toFixed(1)} h/week — a suggestion, not the answer`
                    : "What you are on now, in your own estimation"
                }
                sx={{ width: 240 }}
              />
              <TextField
                size="small"
                type="number"
                label="Build at (% a week)"
                value={state.rampPercent}
                slotProps={{ htmlInput: { min: 1, max: 50, step: 1 } }}
                onChange={(event) =>
                  set("rampPercent", Math.max(1, Number(event.target.value)))
                }
                helperText="10% a week is the usual caution"
                sx={{ width: 200 }}
              />
            </Stack>
          )}

          <FormControlLabel
            control={
              <Switch
                checked={state.useHistory}
                onChange={(event) => set("useHistory", event.target.checked)}
              />
            }
            label={
              <Typography variant="body2">
                Fit the model to my imported months
              </Typography>
            }
          />
          <Typography
            variant="caption"
            sx={{ display: "block", color: "text.secondary" }}
          >
            Off by default. Months with almost no training carry almost no
            information about how you respond to training, and improvement
            during a return is detraining unwinding rather than a training
            response. Left off, the rate and responsiveness come from the
            literature and your starting fitness comes from the anchor you
            entered.
          </Typography>
        </CardContent>
      </Card>

      {error && <Alert severity="error">{error}</Alert>}
      {busy && !result && (
        <CircularProgress size={24} aria-label="Working it out" />
      )}

      {result && (
        <>
          <Answer
            result={result}
            state={state}
            unknown={unknown}
            currentMassLb={currentMassLb}
            locked={locked}
            onLock={async () => {
              await lockPrediction({
                targetDate: dateInMonths(state.months),
                distanceMeters: state.distanceMeters,
                predictedSeconds: result.predicted.median,
                predictedFastSeconds: result.predicted.low,
                predictedSlowSeconds: result.predicted.high,
                weeklyHours: state.weeklyHours,
                compliance: state.compliance,
                raceMassKg:
                  state.raceMassLb === null ? null : lbToKg(state.raceMassLb),
                note: null,
              });
              setLockedSignature(signature);
              reloadLedger();
            }}
          />

          <Card variant="outlined">
            <CardContent>
              <Typography variant="h6">Where the time goes</Typography>
              <Typography
                variant="body2"
                sx={{ color: "text.secondary", mb: 1 }}
              >
                Each factor swung across the range it could plausibly take, with
                everything else held. Longest bar first — that is the one worth
                moving.
              </Typography>
              <Tornado
                sensitivities={result.sensitivities}
                baselineSeconds={result.predicted.median}
              />
              <Table size="small" sx={{ mt: 1 }}>
                <TableHead>
                  <TableRow>
                    <TableCell>Factor</TableCell>
                    <TableCell align="right">Now</TableCell>
                    <TableCell align="right">Per unit</TableCell>
                    <TableCell align="right">Elasticity</TableCell>
                    <TableCell align="right">Swing</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {result.sensitivities.map((s) => (
                    <TableRow key={s.factor}>
                      <TableCell>{s.label}</TableCell>
                      <TableCell align="right">
                        {s.factor === "Compliance"
                          ? `${Math.round(s.value * 100)}%`
                          : s.value.toFixed(1)}
                      </TableCell>
                      <TableCell align="right">
                        {s.perUnitSeconds >= 0 ? "+" : ""}
                        {s.perUnitSeconds.toFixed(1)} s
                      </TableCell>
                      <TableCell align="right">
                        {s.elasticity.toFixed(3)}
                      </TableCell>
                      <TableCell align="right">
                        {Math.round(s.swing)} s
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Elasticity is the percentage change in race time from a one
                percent change in the factor — the derivative, made comparable
                across hours, kilograms and percentage points.
              </Typography>
            </CardContent>
          </Card>

          <Card variant="outlined">
            <CardContent>
              <Stack
                direction={{ xs: "column", sm: "row" }}
                spacing={2}
                sx={{ mb: 1 }}
              >
                <Typography variant="h6" sx={{ flexGrow: 1 }}>
                  Two at once
                </Typography>
                <TextField
                  select
                  size="small"
                  label="Across"
                  value={plotAcross}
                  onChange={(event) =>
                    setAcross(event.target.value as FactorName)
                  }
                  sx={{ minWidth: 150 }}
                >
                  {UNKNOWNS.filter(
                    (o) => o.key !== "RaceTime" && o.key !== down,
                  ).map((o) => (
                    <MenuItem key={o.key} value={o.key}>
                      {o.label}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField
                  select
                  size="small"
                  label="Down"
                  value={plotDown}
                  onChange={(event) =>
                    setDown(event.target.value as FactorName)
                  }
                  sx={{ minWidth: 150 }}
                >
                  {UNKNOWNS.filter(
                    (o) => o.key !== "RaceTime" && o.key !== across,
                  ).map((o) => (
                    <MenuItem key={o.key} value={o.key}>
                      {o.label}
                    </MenuItem>
                  ))}
                </TextField>
              </Stack>
              <Typography
                variant="body2"
                sx={{ color: "text.secondary", mb: 1 }}
              >
                Darker is faster. The orange line is every combination that hits
                your target exactly — the trade you are actually making. Click
                the field to move both factors there.
              </Typography>
              {surface && (
                <ContourPlot
                  surface={surface}
                  currentAcross={factorValue(plotAcross)}
                  currentDown={factorValue(plotDown)}
                  onPick={(a, d) => {
                    setFactor(plotAcross, a);
                    setFactor(plotDown, d);
                  }}
                />
              )}
            </CardContent>
          </Card>

          <Card variant="outlined">
            <CardContent>
              <Typography variant="h6">The approach</Typography>
              <Typography
                variant="body2"
                sx={{ color: "text.secondary", mb: 1 }}
              >
                This scenario read at every horizon, with the 80% interval.
              </Typography>
              <FanChart
                fan={result.fan}
                targetSeconds={
                  unknown === "RaceTime" ? null : state.targetSeconds
                }
                atMonths={state.months}
              />
            </CardContent>
          </Card>

          {plan && plan.options.length > 0 && (
            <Card variant="outlined">
              <CardContent>
                <Typography variant="h6">What to measure next</Typography>
                <Typography
                  variant="body2"
                  sx={{ color: "text.secondary", mb: 1 }}
                >
                  The model's real limit is how thin the record is, not its
                  mathematics. This is what each measurement would be worth,
                  before the effort is spent.
                </Typography>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Measurement</TableCell>
                      <TableCell align="right">When</TableCell>
                      <TableCell align="right">Interval now</TableCell>
                      <TableCell align="right">After</TableCell>
                      <TableCell align="right">Cuts</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {plan.options.slice(0, 6).map((option) => (
                      <TableRow key={`${option.kind}-${option.atMonths}`}>
                        <TableCell>
                          {option.kind === "TimeTrial"
                            ? "Time trial"
                            : "A month of logged runs"}
                        </TableCell>
                        <TableCell align="right">
                          {option.atMonths < 0.05
                            ? "now"
                            : `+${option.atMonths.toFixed(0)}mo`}
                        </TableCell>
                        <TableCell align="right">
                          {formatSeconds(option.widthBeforeSeconds)}
                        </TableCell>
                        <TableCell align="right">
                          {formatSeconds(option.widthAfterSeconds)}
                        </TableCell>
                        <TableCell align="right">
                          <strong>{formatChance(option.reduction)}</strong>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
                <MathPanel
                  steps={plan.steps}
                  title="Show the information calculation"
                />
              </CardContent>
            </Card>
          )}

          <ModelCardView result={result} />

          <PredictionLedger rows={ledger} onChanged={reloadLedger} />
        </>
      )}
    </Stack>
  );
}

function Answer({
  result,
  state,
  unknown,
  currentMassLb,
  onLock,
  locked,
}: {
  result: SolveResult;
  state: WorkbenchState;
  unknown: FactorName | "RaceTime";
  currentMassLb: number | null;
  onLock: () => void;
  locked: boolean;
}) {
  const predicted = result.predicted;

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6" sx={{ mb: 1 }}>
          The answer
        </Typography>
        <Stack
          direction="row"
          spacing={1}
          sx={{ flexWrap: "wrap", rowGap: 1, mb: 1 }}
        >
          <Chip
            color="primary"
            label={
              unknown === "RaceTime"
                ? `${formatDistance(state.distanceMeters)} in ${formatSeconds(predicted.median)}`
                : `${UNKNOWNS.find((u) => u.key === unknown)?.label}: ${
                    result.solved
                      ? show(unknown as FactorName, result.solved)
                      : "—"
                  }`
            }
          />
          <Chip
            variant="outlined"
            label={`80% range ${formatSeconds(predicted.low)} – ${formatSeconds(predicted.high)}`}
          />
          {result.probability !== null && (
            <Chip
              variant="outlined"
              color={result.probability >= 0.5 ? "success" : "warning"}
              label={`Chance at these settings: ${formatChance(result.probability)}`}
            />
          )}
          {result.solved && result.solved.impossible > 0 && (
            <Chip
              variant="outlined"
              color="warning"
              label={`No answer in ${formatChance(result.solved.impossible)} of draws`}
            />
          )}
          {result.solved && result.solved.alreadyMet > 0 && (
            <Chip
              variant="outlined"
              label={`Already there in ${formatChance(result.solved.alreadyMet)} of draws`}
            />
          )}
        </Stack>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {state.raceMassLb !== null &&
            currentMassLb !== null &&
            state.raceMassLb !== currentMassLb && (
              <>
                <strong>
                  Quoted at a race weight of {state.raceMassLb} lb, not
                  today&apos;s {currentMassLb} lb.
                </strong>{" "}
              </>
            )}
          At {state.weeklyHours.toFixed(1)} h/week and{" "}
          {Math.round(state.compliance * 100)}% compliance, the model puts your{" "}
          {formatDistance(state.distanceMeters)} at{" "}
          <strong>{formatSeconds(predicted.median)}</strong> in{" "}
          {state.months.toFixed(0)} months, and is 80% sure it lands between{" "}
          {formatSeconds(predicted.low)} and {formatSeconds(predicted.high)}.
        </Typography>
        <Stack direction="row" spacing={1} sx={{ mt: 1, alignItems: "center" }}>
          <Button
            size="small"
            variant="outlined"
            onClick={onLock}
            disabled={locked}
          >
            {locked
              ? "Locked — it will ask you on the day"
              : "Lock this prediction"}
          </Button>
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Writes down what the model says now, with the plan it assumed, so it
            can be scored rather than argued about.
          </Typography>
        </Stack>

        <MathPanel steps={result.steps} />
      </CardContent>
    </Card>
  );
}

function ModelCardView({ result }: { result: SolveResult }) {
  const model = result.model;

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6" sx={{ mb: 0.5 }}>
          The model behind it
        </Typography>
        <Stack
          direction="row"
          spacing={1}
          sx={{ flexWrap: "wrap", rowGap: 1, mb: 1 }}
        >
          <Chip
            size="small"
            color={model.converged ? "success" : "warning"}
            label={
              model.converged ? "Sampler converged" : "Sampler did not converge"
            }
          />
          <Chip
            size="small"
            variant="outlined"
            label={`R̂ ${model.rHat.toFixed(3)}`}
          />
          <Chip
            size="small"
            variant="outlined"
            label={`Effective draws ${Math.round(model.effectiveSampleSize)}`}
          />
          <Chip
            size="small"
            variant="outlined"
            label={`${model.observations} months, ${model.timeTrials} time trial${model.timeTrials === 1 ? "" : "s"}`}
          />
        </Stack>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Parameter</TableCell>
              <TableCell align="right">Median</TableCell>
              <TableCell align="right">80% range</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {model.parameters.map((parameter) => (
              <TableRow key={parameter.name}>
                <TableCell>{parameter.name}</TableCell>
                <TableCell align="right">
                  {parameter.median.toFixed(parameter.median < 1 ? 3 : 2)}{" "}
                  {parameter.unit}
                </TableCell>
                <TableCell align="right">
                  {parameter.low.toFixed(parameter.low < 1 ? 3 : 2)} –{" "}
                  {parameter.high.toFixed(parameter.high < 1 ? 3 : 2)}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          The parameters sit on a ridge — a proxy read low, from a low start,
          approached slowly fits about as well as an accurate one from a high
          start approached fast — so these marginals lean on their priors. What
          survives the ridge is the prediction, which is why the intervals above
          are the number to trust rather than these.
        </Typography>
        <MathPanel steps={model.steps} title="Show the fit" />
      </CardContent>
    </Card>
  );
}
