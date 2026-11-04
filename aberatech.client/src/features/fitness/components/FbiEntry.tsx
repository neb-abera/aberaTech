import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import { saveFbiResult } from "../core/api";
import { parseClock } from "../core/format";

/**
 * One FBI Physical Fitness Test, as taken. Any event may be left blank.
 * FBI push-ups count only from here: training push-ups are another
 * technique (Neb, 2026-11-04).
 */
export default function FbiEntry({ onSaved }: { onSaved: () => void }) {
  const [date, setDate] = React.useState(() => {
    const now = new Date();
    const local = new Date(now.getTime() - now.getTimezoneOffset() * 60_000);
    return local.toISOString().slice(0, 10);
  });
  const [pullUps, setPullUps] = React.useState("");
  const [sprint, setSprint] = React.useState("");
  const [pushUps, setPushUps] = React.useState("");
  const [run, setRun] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const [saved, setSaved] = React.useState<string | null>(null);

  const count = (text: string) => {
    if (text.trim() === "") return null;
    const n = Number(text);
    return Number.isInteger(n) && n >= 0 ? n : Number.NaN;
  };
  const time = (text: string) =>
    text.trim() === "" ? null : (parseClock(text) ?? Number.NaN);

  const save = async () => {
    const update = {
      date,
      pullUps: count(pullUps),
      sprint300mSeconds: time(sprint),
      pushUps: count(pushUps),
      run1_5MileSeconds: time(run),
    };
    const values = [
      update.pullUps,
      update.sprint300mSeconds,
      update.pushUps,
      update.run1_5MileSeconds,
    ];
    if (values.some((v) => Number.isNaN(v))) {
      setError(
        "Pull-ups and push-ups are counts. The runs are times like 41.2 or 11:20.",
      );
      return;
    }
    if (values.every((v) => v === null)) {
      setError("Enter at least one event.");
      return;
    }
    try {
      const result = await saveFbiResult(update);
      setError(null);
      setSaved(
        `Saved: ${result.total} points over ${result.eventsRun} of 4 events.`,
      );
      onSaved();
    } catch (caught) {
      setSaved(null);
      setError((caught as Error).message || "Could not save the test.");
    }
  };

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6" sx={{ mb: 0.5 }}>
          FBI Physical Fitness Test
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          The raw results as taken. Leave an event blank if you did not run it.
          FBI push-ups count only from here.
        </Typography>
        <Stack spacing={2}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              label="Date"
              type="date"
              value={date}
              onChange={(event) => setDate(event.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              label="Pull-ups"
              value={pullUps}
              onChange={(event) => setPullUps(event.target.value)}
              sx={{ width: 120 }}
            />
            <TextField
              label="300 m sprint (s)"
              value={sprint}
              onChange={(event) => setSprint(event.target.value)}
              sx={{ width: 160 }}
            />
          </Stack>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              label="Push-ups"
              value={pushUps}
              onChange={(event) => setPushUps(event.target.value)}
              sx={{ width: 120 }}
            />
            <TextField
              label="1.5-mile run (m:ss)"
              value={run}
              onChange={(event) => setRun(event.target.value)}
              sx={{ width: 170 }}
            />
            <Button
              variant="contained"
              onClick={save}
              sx={{ alignSelf: "center" }}
            >
              Save test
            </Button>
          </Stack>
        </Stack>
        {error && (
          <Alert severity="error" sx={{ mt: 2 }}>
            {error}
          </Alert>
        )}
        {saved && !error && (
          <Alert
            severity="success"
            onClose={() => setSaved(null)}
            sx={{ mt: 2 }}
          >
            {saved}
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}
