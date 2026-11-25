import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import { type SettingsDto, saveSettings } from "../core/api";
import { kgToLb, lbToKg } from "../core/format";

/** The fastest plan the server accepts, in pounds a week (1.5 kg). */
const MAX_LB_PER_WEEK = 3.3;

function rateText(kgPerWeek: number | null | undefined): string {
  return kgPerWeek == null ? "" : String(Number(kgToLb(kgPerWeek).toFixed(2)));
}

/**
 * The weight change you are making on purpose: the day it began and its
 * pounds a week. The Bodyweight and Body fat forecasts read only the
 * weigh-ins from that day on, and stop at the goal weight on the profile.
 */
export default function WeightPlanCard({
  settings,
  onSaved,
}: {
  settings: SettingsDto;
  onSaved: () => void;
}) {
  const [rate, setRate] = React.useState(
    rateText(settings.weightPlanKgPerWeek),
  );
  const [since, setSince] = React.useState(settings.weightPlanStartedOn ?? "");
  const [status, setStatus] = React.useState<{
    ok: boolean;
    text: string;
  } | null>(null);

  React.useEffect(() => {
    setRate(rateText(settings.weightPlanKgPerWeek));
    setSince(settings.weightPlanStartedOn ?? "");
  }, [settings.weightPlanKgPerWeek, settings.weightPlanStartedOn]);

  const goal =
    settings.goalWeightKg == null
      ? null
      : Math.round(kgToLb(settings.goalWeightKg));

  const save = async (
    weightPlanKgPerWeek: number | null,
    weightPlanStartedOn: string | null,
    lbPerWeek: number | null,
  ) => {
    try {
      await saveSettings({ weightPlanKgPerWeek, weightPlanStartedOn });
      setStatus({
        ok: true,
        text:
          weightPlanKgPerWeek === null
            ? "Weight plan cleared. Bodyweight and body fat are not forecast without one."
            : `Saved. ${lbPerWeek} lb a week from ${weightPlanStartedOn} to ${goal} lb.`,
      });
      onSaved();
    } catch (error) {
      setStatus({ ok: false, text: (error as Error).message });
    }
  };

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    const value = Number(rate);
    if (rate.trim() === "" || !(value > 0 && value <= MAX_LB_PER_WEEK)) {
      setStatus({
        ok: false,
        text: `Pounds a week is more than 0 and at most ${MAX_LB_PER_WEEK}.`,
      });
      return;
    }
    if (!/^\d{4}-\d{2}-\d{2}$/.test(since)) {
      setStatus({ ok: false, text: "Pick the day the plan began." });
      return;
    }
    await save(lbToKg(value), since, value);
  };

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">Weight plan</Typography>
        <Typography variant="body2" sx={{ mb: 2 }}>
          The day your cut began and its pounds a week, as Lose It holds them.
          Lose It has no way to send its plan here, so it is entered once. The
          Bodyweight and Body fat forecasts read only the weigh-ins from that
          day on, and stop at{" "}
          {goal === null
            ? "the goal weight, which the profile does not have yet"
            : `${goal} lb, the goal weight on the profile`}
          .
        </Typography>

        <form onSubmit={submit}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={2}
            sx={{ alignItems: { sm: "flex-start" } }}
          >
            <TextField
              label="Loss (lb a week)"
              value={rate}
              onChange={(e) => setRate(e.target.value)}
              slotProps={{ htmlInput: { inputMode: "decimal" } }}
              placeholder="1.5"
              size="small"
            />
            <TextField
              label="Began"
              type="date"
              value={since}
              onChange={(e) => setSince(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
              size="small"
            />
            <Button type="submit" variant="outlined">
              Save weight plan
            </Button>
            {settings.weightPlanKgPerWeek != null && (
              <Button onClick={() => void save(null, null, null)}>Clear</Button>
            )}
          </Stack>
        </form>

        {status && (
          <Alert
            severity={status.ok ? "success" : "error"}
            onClose={() => setStatus(null)}
            sx={{ mt: 2 }}
          >
            {status.text}
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}
