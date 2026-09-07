import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import { saveAftResult } from "../core/api";
import { lbToKg, parseClock } from "../core/format";

/**
 * One Army Fitness Test, as taken. The five raw results; the scoring happens
 * on the server against the published tables and comes back with the save.
 */
export default function AftEntry({ onSaved }: { onSaved: () => void }) {
  const [date, setDate] = React.useState(() => {
    const now = new Date();
    const local = new Date(now.getTime() - now.getTimezoneOffset() * 60_000);
    return local.toISOString().slice(0, 10);
  });
  const [deadliftLb, setDeadliftLb] = React.useState("");
  const [pushUps, setPushUps] = React.useState("");
  const [sprintDragCarry, setSprintDragCarry] = React.useState("");
  const [plank, setPlank] = React.useState("");
  const [twoMile, setTwoMile] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const [saved, setSaved] = React.useState<string | null>(null);

  const save = async () => {
    const lb = Number(deadliftLb);
    const reps = Number(pushUps);
    const sdc = parseClock(sprintDragCarry);
    const held = parseClock(plank);
    const run = parseClock(twoMile);
    if (
      !Number.isFinite(lb) ||
      lb <= 0 ||
      !Number.isInteger(reps) ||
      reps < 0
    ) {
      setError("Deadlift is pounds; push-ups are a count.");
      return;
    }
    if (sdc === null || held === null || run === null) {
      setError("Sprint-drag-carry, plank and the run are times like 2:10.");
      return;
    }
    try {
      const result = await saveAftResult({
        date,
        deadliftKg: lbToKg(lb),
        handReleasePushUps: reps,
        sprintDragCarrySeconds: sdc,
        plankSeconds: held,
        twoMileSeconds: run,
      });
      setError(null);
      setSaved(
        `Scored ${result.total} on the ${result.ageBand} band — combat standard ${result.meetsCombatStandard ? "met" : "not met"}.`,
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
          Army Fitness Test
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          The five raw results as taken. Scored against the published tables on
          your age band, and used as measurements by the readiness gates.
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
              label="Deadlift 3RM (lb)"
              value={deadliftLb}
              onChange={(event) => setDeadliftLb(event.target.value)}
              sx={{ width: 160 }}
            />
            <TextField
              label="Hand-release push-ups"
              value={pushUps}
              onChange={(event) => setPushUps(event.target.value)}
              sx={{ width: 190 }}
            />
          </Stack>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              label="Sprint-drag-carry (m:ss)"
              value={sprintDragCarry}
              onChange={(event) => setSprintDragCarry(event.target.value)}
              sx={{ width: 200 }}
            />
            <TextField
              label="Plank (m:ss)"
              value={plank}
              onChange={(event) => setPlank(event.target.value)}
              sx={{ width: 140 }}
            />
            <TextField
              label="Two-mile run (m:ss)"
              value={twoMile}
              onChange={(event) => setTwoMile(event.target.value)}
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
