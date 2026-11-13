import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Grid from "@mui/material/Grid";
import Slider from "@mui/material/Slider";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import ToggleButton from "@mui/material/ToggleButton";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";
import Typography from "@mui/material/Typography";
import type { Dose, PlanRequest, Prediction } from "../core/api";
import type { MeasuredCompliance } from "../core/compliance";
import { ATHLETIC, EVOKE, type WeekPreset } from "./doses";
import { NumberInput } from "./NumberInput";

export type PlanMode = "total" | "zones";

export interface PlanState {
  mode: PlanMode;
  totalHours: number;
  easyHours: number;
  thresholdHours: number;
  intervalHours: number;
  strengthHours: number;
  /**
   * The week is a written training plan's (the Evoke plan), so some of its
   * sessions may be missed and compliance applies. A week stated as hours
   * (a total, the Athletic default, the log, or any edit) is the hours
   * trained, and compliance would count the misses twice. Neb, 2026-11-13.
   */
  written: boolean;
}

export function toRequest(plan: PlanState): PlanRequest {
  return plan.mode === "total"
    ? { weeklyHours: plan.totalHours, strengthHours: plan.strengthHours }
    : {
        easyHours: plan.easyHours,
        thresholdHours: plan.thresholdHours,
        intervalHours: plan.intervalHours,
        strengthHours: plan.strengthHours,
      };
}

export function planFromDose(dose: Dose, mode: PlanMode = "zones"): PlanState {
  return {
    mode,
    totalHours: Math.max(1, Number(dose.runningHours.toFixed(2))),
    easyHours: Number(dose.easyHours.toFixed(2)),
    thresholdHours: Number(dose.thresholdHours.toFixed(2)),
    intervalHours: Number(dose.intervalHours.toFixed(2)),
    strengthHours: Number(dose.strengthHours.toFixed(2)),
    written: false,
  };
}

/**
 * A preset week as a plan: running split near 80% easy, the way the model
 * would spend a total, and strength as stated. The Evoke plan's base blocks
 * are aerobic running, so its hours all go to easy.
 */
export function planFromPreset(
  preset: WeekPreset,
  mode: PlanMode = "total",
): PlanState {
  const run = preset.weeklyHours;
  const allEasy = preset === EVOKE;
  const round = (x: number) => Math.round(x * 4) / 4;
  const threshold = allEasy ? 0 : round(run * 0.125);
  const interval = allEasy ? 0 : round(run * 0.075);
  return {
    mode,
    totalHours: run,
    easyHours: allEasy ? run : round(run - threshold - interval),
    thresholdHours: threshold,
    intervalHours: interval,
    strengthHours: preset.strengthHours,
    written: preset === EVOKE,
  };
}

const ZONE_NOTES: Record<string, string> = {
  Easy: "Conversational volume. Saturates slowly. This is the long game.",
  Threshold:
    "Comfortably hard. Most of its return is in the first ~1.2 h/week.",
  Interval: "VO2max repeats. Nearly spent after two sessions a week.",
  Strength: "Heavy lifting, for economy and durability.",
};

/**
 * The training week, stated the way it is actually lived: hours by intensity.
 * Either name them, or give a total and let the model spend it the way it
 * would advise spending it. Then see, per zone, what the next hour there buys.
 */
export default function PlanEditor({
  plan,
  onChange,
  compliance,
  onComplianceChange,
  measuredCompliance = null,
  prediction,
  measured,
}: {
  plan: PlanState;
  onChange: (plan: PlanState) => void;
  compliance: number;
  onComplianceChange: (compliance: number) => void;
  /** What the log shows of the written plan so far, when it can be read. */
  measuredCompliance?: MeasuredCompliance | null;
  prediction: Prediction | null;
  measured: Dose;
}) {
  // Any hour typed in is the athlete's own week, not the plan's.
  const edit = (patch: Partial<PlanState>) =>
    onChange({ ...plan, ...patch, written: false });
  const zoneField = (
    label: string,
    key: "easyHours" | "thresholdHours" | "intervalHours" | "strengthHours",
  ) => (
    <Grid size={{ xs: 6, sm: 3 }} key={key}>
      <NumberInput
        label={`${label} (h/week)`}
        value={plan[key]}
        min={0}
        max={40}
        step={0.25}
        onCommit={(value) => edit({ [key]: value })}
      />
    </Grid>
  );

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          sx={{ mb: 2, alignItems: { sm: "center" } }}
        >
          <Typography variant="h6" sx={{ flexGrow: 1 }}>
            Your training week
          </Typography>
          <ToggleButtonGroup
            size="small"
            exclusive
            value={plan.mode}
            onChange={(_, mode: PlanMode | null) =>
              mode && onChange({ ...plan, mode })
            }
            aria-label="How to state the week"
          >
            <ToggleButton value="total">
              Give a total, split it for me
            </ToggleButton>
            <ToggleButton value="zones">I'll set the hours</ToggleButton>
          </ToggleButtonGroup>
          {[ATHLETIC, EVOKE].map((preset) => (
            <Button
              key={preset.label}
              size="small"
              title={preset.source}
              onClick={() => onChange(planFromPreset(preset, plan.mode))}
            >
              {preset.label}: {preset.weeklyHours} h + {preset.strengthHours} h
            </Button>
          ))}
          <Button
            size="small"
            onClick={() => onChange(planFromDose(measured, plan.mode))}
            disabled={measured.runningHours <= 0}
          >
            Use my logged week
          </Button>
        </Stack>

        <Grid container spacing={3}>
          {plan.mode === "total" ? (
            <Grid size={{ xs: 12, sm: 6 }}>
              <Typography gutterBottom variant="body2">
                Weekly running hours:{" "}
                <strong>{plan.totalHours.toFixed(2)}</strong>
              </Typography>
              <Slider
                aria-label="Weekly running hours"
                min={1}
                max={20}
                step={0.25}
                value={plan.totalHours}
                onChange={(_, value) => edit({ totalHours: value as number })}
              />
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Split to maximize the ceiling it supports, which lands near 80%
                easy, the distribution the elite-endurance literature observes,
                arrived at rather than assumed.
              </Typography>
            </Grid>
          ) : (
            <>
              {zoneField("Easy", "easyHours")}
              {zoneField("Threshold", "thresholdHours")}
              {zoneField("Interval", "intervalHours")}
              {zoneField("Strength", "strengthHours")}
            </>
          )}

          {plan.mode === "total" && (
            <Grid size={{ xs: 12, sm: 3 }}>
              <NumberInput
                label="Strength (h/week)"
                value={plan.strengthHours}
                min={0}
                max={10}
                step={0.25}
                onCommit={(value) => edit({ strengthHours: value })}
              />
            </Grid>
          )}

          {plan.written && (
            <Grid size={{ xs: 12, sm: 3 }}>
              <Typography gutterBottom variant="body2">
                Compliance: <strong>{compliance}%</strong>
              </Typography>
              <Slider
                aria-label="Compliance percent"
                min={0}
                max={100}
                step={1}
                value={compliance}
                onChange={(_, value) => onComplianceChange(value as number)}
              />
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {measuredCompliance ? (
                  <>
                    Measured: your log holds{" "}
                    {Math.round(measuredCompliance.loggedMinutes)} of the
                    plan&apos;s {Math.round(measuredCompliance.plannedMinutes)}{" "}
                    running and rucking minutes from {measuredCompliance.from}{" "}
                    through {measuredCompliance.through},{" "}
                    {Math.round(measuredCompliance.share * 100)}%. Optional
                    sessions are left out. Move the slider to ask what if.
                  </>
                ) : (
                  <>
                    The share of the plan&apos;s running and rucking that
                    happens. It is measured from your log once a planned day has
                    passed.
                  </>
                )}
              </Typography>
              {measuredCompliance &&
                compliance !== Math.round(measuredCompliance.share * 100) && (
                  <Button
                    size="small"
                    onClick={() =>
                      onComplianceChange(
                        Math.round(measuredCompliance.share * 100),
                      )
                    }
                  >
                    Back to measured
                  </Button>
                )}
            </Grid>
          )}
        </Grid>

        {prediction && (
          <>
            <Typography variant="body2" sx={{ mt: 2, color: "text.secondary" }}>
              {prediction.effective.runningHours.toFixed(1)} h/week actually
              trained, costing{" "}
              <strong>{prediction.effective.strain.toFixed(1)}</strong>{" "}
              easy-hour equivalents of recovery,{" "}
              {(prediction.effective.easyShare * 100).toFixed(0)}% of it easy.
              It supports a ceiling of{" "}
              <strong>VDOT {prediction.ceiling.toFixed(1)}</strong>, and one
              more hour a week would buy{" "}
              <strong>+{prediction.hourPrice.toFixed(2)} VDOT</strong>.
              {prediction.rampMonths > 0.1 && (
                <>
                  {" "}
                  Building to it from your logged week at 8% a week takes about{" "}
                  {prediction.rampMonths.toFixed(1)} months, which every date
                  below already accounts for.
                </>
              )}
            </Typography>

            <Box sx={{ overflowX: "auto", mt: 1 }}>
              <Table size="small" sx={{ minWidth: 520 }}>
                <TableHead>
                  <TableRow>
                    <TableCell>Zone</TableCell>
                    <TableCell align="right">Hours</TableCell>
                    <TableCell align="right">Recovery cost</TableCell>
                    <TableCell align="right">Next hour buys</TableCell>
                    <TableCell>What it is for</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {prediction.effective.zones.map((zone) => (
                    <TableRow key={zone.zone}>
                      <TableCell>{zone.zone}</TableCell>
                      <TableCell align="right">
                        {zone.hours.toFixed(2)}
                      </TableCell>
                      <TableCell align="right">
                        {zone.strain.toFixed(1)}
                      </TableCell>
                      <TableCell align="right">
                        +{zone.marginalVdotPerHour.toFixed(2)} VDOT
                      </TableCell>
                      <TableCell sx={{ color: "text.secondary" }}>
                        {ZONE_NOTES[zone.zone]}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
          </>
        )}
      </CardContent>
    </Card>
  );
}
