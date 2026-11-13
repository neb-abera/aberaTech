import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import { type SettingsDto, saveSettings } from "../core/api";
import { formatPace } from "../core/format";

/** The ACSM running equation's vertical cost over its horizontal cost: 0.9 / 0.2. */
export const COST_RATIO = 4.5;

/** The steepest grade the server accepts, in percent. */
const MAX_PERCENT = 15;

/** The flat pace that costs the same as a pace at a grade. */
export function flatPace(secPerKm: number, gradePercent: number): number {
  return secPerKm / (1 + (COST_RATIO * gradePercent) / 100);
}

/**
 * The incline treadmill runs are set to. A watch cannot record it, so the
 * aerobic trend reads every treadmill run from the start date on at this
 * grade, as the flat run of the same cost. A grade in a run's title wins.
 */
export default function TreadmillGradeCard({
  settings,
  examplePace,
  onSaved,
}: {
  settings: SettingsDto;
  /** A pace to show the conversion on: the latest month's, when there is one. */
  examplePace: number | null;
  onSaved: () => void;
}) {
  const [percent, setPercent] = React.useState(
    settings.treadmillGradePercent == null
      ? ""
      : String(settings.treadmillGradePercent),
  );
  const [since, setSince] = React.useState(settings.treadmillGradeSince ?? "");
  const [status, setStatus] = React.useState<{
    ok: boolean;
    text: string;
  } | null>(null);

  React.useEffect(() => {
    setPercent(
      settings.treadmillGradePercent == null
        ? ""
        : String(settings.treadmillGradePercent),
    );
    setSince(settings.treadmillGradeSince ?? "");
  }, [settings.treadmillGradePercent, settings.treadmillGradeSince]);

  const save = async (
    treadmillGradePercent: number | null,
    treadmillGradeSince: string | null,
  ) => {
    try {
      await saveSettings({
        treadmillGradePercent,
        treadmillGradeSince,
      });
      setStatus({
        ok: true,
        text:
          treadmillGradePercent === null
            ? "Incline cleared. Treadmill runs read as 0% again."
            : `Saved. Treadmill runs from ${treadmillGradeSince} on read at ${treadmillGradePercent}%.`,
      });
      onSaved();
    } catch (error) {
      setStatus({ ok: false, text: (error as Error).message });
    }
  };

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    const value = Number(percent);
    if (percent.trim() === "" || !(value >= 0 && value <= MAX_PERCENT)) {
      setStatus({ ok: false, text: `Incline is 0-${MAX_PERCENT}%.` });
      return;
    }
    if (!/^\d{4}-\d{2}-\d{2}$/.test(since)) {
      setStatus({ ok: false, text: "Pick the date you started this incline." });
      return;
    }
    await save(value, since);
  };

  const shown = Number(percent);
  const example =
    examplePace !== null && percent.trim() !== "" && shown > 0
      ? `At ${shown}%, ${formatPace(examplePace)} on the belt reads as ${formatPace(flatPace(examplePace, shown))} flat: ${formatPace(examplePace)} ÷ (1 + ${COST_RATIO} × ${shown / 100}).`
      : null;

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">Treadmill incline</Typography>
        <Typography variant="body2" sx={{ mb: 1 }}>
          Your watch cannot record a treadmill's incline. Set the incline you
          run at and the day you started. The Aerobic base trend and the model
          then read each of those runs at the flat pace that costs the same
          (ACSM running equation: pace ÷ (1 + 4.5 × grade)). A grade in a run's
          title, such as "2%" or "0%", overrides this for that run.
        </Typography>
        {example && (
          <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
            {example}
          </Typography>
        )}

        <form onSubmit={submit}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={2}
            sx={{ alignItems: { sm: "flex-start" } }}
          >
            <TextField
              label="Incline (%)"
              value={percent}
              onChange={(e) => setPercent(e.target.value)}
              slotProps={{ htmlInput: { inputMode: "decimal" } }}
              placeholder="1"
              size="small"
            />
            <TextField
              label="Since"
              type="date"
              value={since}
              onChange={(e) => setSince(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
              size="small"
            />
            <Button type="submit" variant="outlined">
              Save incline
            </Button>
            {settings.treadmillGradePercent != null && (
              <Button onClick={() => void save(null, null)}>Clear</Button>
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
