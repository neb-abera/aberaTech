import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import type { SettingsDto } from "../core/api";
import {
  type ActivityRow,
  fetchActivities,
  saveBodyMetric,
  syncHevy,
  uploadFile,
} from "../core/api";
import { formatSeconds, lbToKg } from "../core/format";
import ProfileCard from "./ProfileCard";

/**
 * The pipes: file uploads for the zero-cost paths, the Hevy API button when a
 * key is configured, weigh-ins, and a view of what actually landed.
 */
export default function DataPanel({
  hevyApi,
  settings,
  onProfileSaved,
}: {
  hevyApi: boolean;
  settings: SettingsDto;
  onProfileSaved: () => void;
}) {
  const [status, setStatus] = React.useState<{
    ok: boolean;
    text: string;
  } | null>(null);
  const [activities, setActivities] = React.useState<ActivityRow[]>([]);

  const refresh = React.useCallback(() => {
    fetchActivities()
      .then(setActivities)
      .catch(() => setActivities([]));
  }, []);

  React.useEffect(refresh, [refresh]);

  const upload =
    (kind: "hevy-csv" | "garmin-csv" | "fit") =>
    async (event: React.ChangeEvent<HTMLInputElement>) => {
      const file = event.target.files?.[0];
      event.target.value = "";
      if (!file) {
        return;
      }
      try {
        const result = await uploadFile(kind, file);
        setStatus({
          ok: true,
          text: `${file.name}: ${result.parsed} parsed, ${result.added} new.`,
        });
        refresh();
      } catch (error) {
        setStatus({
          ok: false,
          text: `${file.name}: ${(error as Error).message}`,
        });
      }
    };

  const runHevySync = async () => {
    try {
      const result = await syncHevy();
      setStatus({
        ok: true,
        text: `Hevy: ${result.fetched} workouts fetched, ${result.added} new.`,
      });
      refresh();
    } catch (error) {
      setStatus({
        ok: false,
        text: `Hevy sync failed: ${(error as Error).message}`,
      });
    }
  };

  return (
    <Stack spacing={3}>
      <ProfileCard settings={settings} onSaved={onProfileSaved} />

      <Card variant="outlined">
        <CardContent>
          <Typography variant="h6" sx={{ mb: 0.5 }}>
            Bring data in
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
            Garmin Connect: Activities page → export CSV for bulk history, or a
            single activity&apos;s .fit file for full detail. Hevy: Settings →
            Export Data emails a CSV — free tier included.
          </Typography>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <Button variant="outlined" component="label">
              Garmin activities CSV
              <input
                type="file"
                hidden
                accept=".csv"
                onChange={upload("garmin-csv")}
              />
            </Button>
            <Button variant="outlined" component="label">
              Garmin .fit file
              <input
                type="file"
                hidden
                accept=".fit"
                onChange={upload("fit")}
              />
            </Button>
            <Button variant="outlined" component="label">
              Hevy export CSV
              <input
                type="file"
                hidden
                accept=".csv"
                onChange={upload("hevy-csv")}
              />
            </Button>
            {hevyApi && (
              <Button variant="contained" onClick={runHevySync}>
                Sync Hevy now
              </Button>
            )}
          </Stack>
          {!hevyApi && (
            <Typography
              variant="caption"
              sx={{ color: "text.secondary", display: "block", mt: 1 }}
            >
              Live Hevy sync appears here once a Hevy Pro API key is configured
              (~$24/year, optional).
            </Typography>
          )}
        </CardContent>
      </Card>

      <WeighIn onSaved={(text) => setStatus({ ok: true, text })} />

      {status && (
        <Alert severity={status.ok ? "success" : "error"}>{status.text}</Alert>
      )}

      <Card variant="outlined">
        <CardContent>
          <Typography variant="h6" sx={{ mb: 1 }}>
            Latest activities
          </Typography>
          {activities.length === 0 ? (
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              Nothing imported yet.
            </Typography>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>When</TableCell>
                  <TableCell>Sport</TableCell>
                  <TableCell>Name</TableCell>
                  <TableCell align="right">Distance</TableCell>
                  <TableCell align="right">Time</TableCell>
                  <TableCell align="right">Avg HR</TableCell>
                  <TableCell>Source</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {activities.map((activity) => (
                  <TableRow key={activity.id}>
                    <TableCell>{activity.startedAt.slice(0, 10)}</TableCell>
                    <TableCell>{activity.sport}</TableCell>
                    <TableCell>{activity.name}</TableCell>
                    <TableCell align="right">
                      {activity.distanceMeters === null
                        ? "—"
                        : `${(activity.distanceMeters / 1000).toFixed(2)} km`}
                    </TableCell>
                    <TableCell align="right">
                      {formatSeconds(activity.durationSeconds)}
                    </TableCell>
                    <TableCell align="right">
                      {activity.averageHr ?? "—"}
                    </TableCell>
                    <TableCell>{activity.source}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </Stack>
  );
}

function WeighIn({ onSaved }: { onSaved: (text: string) => void }) {
  const [date, setDate] = React.useState(() =>
    new Date().toISOString().slice(0, 10),
  );
  const [pounds, setPounds] = React.useState("");
  const [bodyFat, setBodyFat] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);

  const save = async () => {
    const lb = Number(pounds);
    if (!Number.isFinite(lb) || lb <= 0) {
      setError("Weight must be a number of pounds.");
      return;
    }
    const fat = bodyFat.trim() === "" ? null : Number(bodyFat);
    try {
      await saveBodyMetric(date, lbToKg(lb), fat);
      setError(null);
      onSaved(`Weigh-in saved: ${lb} lb on ${date}.`);
    } catch {
      setError("Could not save the weigh-in.");
    }
  };

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6" sx={{ mb: 0.5 }}>
          Weigh-in
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          Bodyweight feeds the run predictions (VDOT is per-kilogram). Nutrition
          planning stays in LoseIt; this is just the number.
        </Typography>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          <TextField
            label="Date"
            type="date"
            value={date}
            onChange={(event) => setDate(event.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <TextField
            label="Weight (lb)"
            value={pounds}
            onChange={(event) => setPounds(event.target.value)}
            sx={{ width: 140 }}
          />
          <TextField
            label="Body fat % (optional)"
            value={bodyFat}
            onChange={(event) => setBodyFat(event.target.value)}
            sx={{ width: 180 }}
          />
          <Button variant="contained" onClick={save}>
            Save
          </Button>
        </Stack>
        {error && (
          <Alert severity="error" sx={{ mt: 2 }}>
            {error}
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}
