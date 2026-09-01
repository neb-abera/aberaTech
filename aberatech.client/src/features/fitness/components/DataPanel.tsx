import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
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
  onDataChanged,
}: {
  hevyApi: boolean;
  settings: SettingsDto;
  /**
   * Anything that changes what the dashboard would say. Every mutation on this
   * panel calls it, because the dashboard is built once when the page loads:
   * an import that only refreshed the list below it left the charts showing
   * the state of things before the upload, on the tab the athlete actually
   * reads.
   */
  onDataChanged: () => void;
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

  const [busy, setBusy] = React.useState(false);
  const [dragging, setDragging] = React.useState(false);

  // One file at a time, but every file in turn, so dropping the whole set of
  // downloads works rather than silently importing the first.
  const send = React.useCallback(
    async (files: FileList | null) => {
      const chosen = Array.from(files ?? []);
      if (chosen.length === 0) {
        return;
      }

      setBusy(true);
      const notes: string[] = [];
      let failed = false;

      for (const file of chosen) {
        try {
          const result = await uploadFile(file);
          // Reconciliation is said out loud. Uploading both of the files
          // Garmin offers should visibly resolve, not look like half of it
          // quietly did nothing.
          const reconciled = [
            result.skipped > 0 && `${result.skipped} already in your export`,
            result.superseded > 0 && `${result.superseded} replaced`,
          ].filter(Boolean);
          notes.push(
            `${file.name} — ${result.kind}: ${result.parsed} activities, ${result.added} new` +
              (reconciled.length > 0 ? ` (${reconciled.join(", ")}).` : "."),
          );
        } catch (error) {
          failed = true;
          notes.push(`${file.name}: ${(error as Error).message}`);
        }
      }

      setBusy(false);
      setStatus({ ok: !failed, text: notes.join(" ") });
      refresh();
      onDataChanged();
    },
    [refresh, onDataChanged],
  );

  const onDrop = async (event: React.DragEvent) => {
    event.preventDefault();
    setDragging(false);
    await send(event.dataTransfer.files);
  };

  const runHevySync = async () => {
    try {
      const result = await syncHevy();
      setStatus({
        ok: true,
        text: `Hevy: ${result.fetched} workouts fetched, ${result.added} new.`,
      });
      refresh();
      onDataChanged();
    } catch (error) {
      setStatus({
        ok: false,
        text: `Hevy sync failed: ${(error as Error).message}`,
      });
    }
  };

  return (
    <Stack spacing={3}>
      <ProfileCard settings={settings} onSaved={onDataChanged} />

      <Card variant="outlined">
        <CardContent>
          <Typography variant="h6" sx={{ mb: 0.5 }}>
            Bring data in
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
            Whatever they sent you. Garmin&apos;s &ldquo;Export Your Data&rdquo;
            archive, a single activity&apos;s .fit file, an activities CSV from
            the Connect website, or Hevy&apos;s export — drop the file in as it
            arrived and it will be read for what it is. Re-importing the same
            file never duplicates anything.
          </Typography>

          {/* A drop target that is also a button, because the archive arrives
              as a download and dragging it here is one gesture rather than
              four dialogs. */}
          <Box
            onDragOver={(event: React.DragEvent) => {
              event.preventDefault();
              setDragging(true);
            }}
            onDragLeave={() => setDragging(false)}
            onDrop={onDrop}
            sx={{
              border: "1px dashed",
              borderColor: dragging ? "primary.main" : "divider",
              bgcolor: dragging ? "action.hover" : "transparent",
              borderRadius: 1,
              px: 2,
              py: 3,
              textAlign: "center",
              transition: "border-color 120ms, background-color 120ms",
            }}
          >
            <Stack spacing={1.5} sx={{ alignItems: "center" }}>
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                Drop files here
              </Typography>
              <Button variant="outlined" component="label" disabled={busy}>
                {busy ? "Reading…" : "Choose files"}
                <input
                  type="file"
                  hidden
                  multiple
                  accept=".zip,.csv,.fit,.json"
                  onChange={async (
                    event: React.ChangeEvent<HTMLInputElement>,
                  ) => {
                    const files = event.target.files;
                    event.target.value = "";
                    await send(files);
                  }}
                />
              </Button>
            </Stack>
          </Box>

          {hevyApi && (
            <Button variant="contained" sx={{ mt: 2 }} onClick={runHevySync}>
              Sync Hevy now
            </Button>
          )}
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

      {/* A weigh-in moves the dashboard too: VDOT is per kilogram. */}
      <WeighIn
        onSaved={(text) => {
          setStatus({ ok: true, text });
          onDataChanged();
        }}
      />

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
