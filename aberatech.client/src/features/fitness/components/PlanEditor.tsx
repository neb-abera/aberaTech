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
import TextField from "@mui/material/TextField";
import ToggleButton from "@mui/material/ToggleButton";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";
import Typography from "@mui/material/Typography";
import type { Dose, PlanRequest, Prediction } from "../core/api";

export type PlanMode = "total" | "zones";

export interface PlanState {
  mode: PlanMode;
  totalHours: number;
  easyHours: number;
  thresholdHours: number;
  intervalHours: number;
  strengthHours: number;
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
  };
}

const ZONE_NOTES: Record<string, string> = {
  Easy: "Conversational volume. Saturates slowly — this is the long game.",
  Threshold:
    "Comfortably hard. Most of its return is in the first ~1.2 h/week.",
  Interval: "VO2max repeats. Nearly spent after two sessions a week.",
  Strength: "Heavy lifting, for economy and durability.",
};

/**
 * The training week, stated the way it is actually lived: hours by intensity.
 * Either name them, or give a total and let the model spend it the way it
 * would advise spending it — and see, per zone, what the next hour there buys.
 */
export default function PlanEditor({
  plan,
  onChange,
  compliance,
  onComplianceChange,
  prediction,
  measured,
}: {
  plan: PlanState;
  onChange: (plan: PlanState) => void;
  compliance: number;
  onComplianceChange: (compliance: number) => void;
  prediction: Prediction | null;
  measured: Dose;
}) {
  const zoneField = (
    label: string,
    key: "easyHours" | "thresholdHours" | "intervalHours" | "strengthHours",
  ) => (
    <Grid size={{ xs: 6, sm: 3 }} key={key}>
      <TextField
        fullWidth
        size="small"
        type="number"
        label={`${label} (h/week)`}
        value={plan[key]}
        slotProps={{ htmlInput: { min: 0, max: 40, step: 0.25 } }}
        onChange={(event) =>
          onChange({
            ...plan,
            [key]: Math.max(0, Math.min(40, Number(event.target.value))),
          })
        }
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
                onChange={(_, value) =>
                  onChange({ ...plan, totalHours: value as number })
                }
              />
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Split to maximise the ceiling it supports, which lands near 80%
                easy — the distribution the elite-endurance literature observes,
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
              <TextField
                fullWidth
                size="small"
                type="number"
                label="Strength (h/week)"
                value={plan.strengthHours}
                slotProps={{ htmlInput: { min: 0, max: 10, step: 0.25 } }}
                onChange={(event) =>
                  onChange({
                    ...plan,
                    strengthHours: Math.max(
                      0,
                      Math.min(10, Number(event.target.value)),
                    ),
                  })
                }
              />
            </Grid>
          )}

          <Grid size={{ xs: 12, sm: 3 }}>
            <Typography gutterBottom variant="body2">
              Compliance: <strong>{compliance}%</strong>
            </Typography>
            <Slider
              aria-label="Compliance percent"
              min={10}
              max={100}
              step={5}
              value={compliance}
              onChange={(_, value) => onComplianceChange(value as number)}
            />
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              The share of planned sessions that actually happen. Your last 8
              weeks logged {measured.runningHours.toFixed(1)} h/week of running.
            </Typography>
          </Grid>
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

            <Table size="small" sx={{ mt: 1 }}>
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
                    <TableCell align="right">{zone.hours.toFixed(2)}</TableCell>
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
          </>
        )}
      </CardContent>
    </Card>
  );
}
