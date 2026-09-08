import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlineOutlined";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import IconButton from "@mui/material/IconButton";
import LinearProgress from "@mui/material/LinearProgress";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import TextField from "@mui/material/TextField";
import Tooltip from "@mui/material/Tooltip";
import Typography from "@mui/material/Typography";
import * as React from "react";
import type { SettingsDto } from "../core/api";
import {
  type ActivityRow,
  deleteActivity,
  fetchActivities,
  fetchIngestStatus,
  type IngestStatus,
  type SourceStatus,
  saveBodyMetric,
  syncHevy,
  syncIntervalsIcu,
  uploadFile,
} from "../core/api";
import { formatSeconds, lbToKg } from "../core/format";
import AftEntry from "./AftEntry";
import LoadField from "./LoadField";
import ProfileCard from "./ProfileCard";

/**
 * The pipes: file uploads for the zero-cost paths, the Hevy API button when a
 * key is configured, weigh-ins, and a view of what actually landed.
 */
export default function DataPanel({
  hevyApi,
  intervalsIcu = false,
  settings,
  onDataChanged,
}: {
  hevyApi: boolean;
  /** Whether the deployment has an intervals.icu API key. */
  intervalsIcu?: boolean;
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
  const [total, setTotal] = React.useState(0);

  const refresh = React.useCallback(() => {
    fetchActivities()
      .then((page) => {
        setActivities(page.activities);
        setTotal(page.total);
      })
      .catch(() => setActivities([]));
  }, []);

  React.useEffect(refresh, [refresh]);

  const [busy, setBusy] = React.useState<{ done: number; of: number } | null>(
    null,
  );
  const [dragging, setDragging] = React.useState(false);

  // One file at a time, but every file in turn, so dropping the whole set of
  // downloads works rather than silently importing the first.
  const send = React.useCallback(
    async (files: FileList | null) => {
      const chosen = Array.from(files ?? []);
      if (chosen.length === 0) {
        return;
      }

      // A Garmin archive takes seconds to read; without this the page looked
      // inert while it worked, and a second drop mid-read started a
      // concurrent upload of the same thing.
      setBusy({ done: 0, of: chosen.length });
      const notes: string[] = [];
      let failed = false;

      for (const [index, file] of chosen.entries()) {
        setBusy({ done: index, of: chosen.length });
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

      setBusy(null);
      setStatus({ ok: !failed, text: notes.join(" ") });
      refresh();
      onDataChanged();
    },
    [refresh, onDataChanged],
  );

  const onDrop = async (event: React.DragEvent) => {
    event.preventDefault();
    setDragging(false);
    if (busy) return;
    await send(event.dataTransfer.files);
  };

  const remove = async (activity: ActivityRow) => {
    try {
      await deleteActivity(activity.id);
      setStatus({
        ok: true,
        text: `Removed ${activity.name || activity.sport} of ${activity.startedAt.slice(0, 10)}.`,
      });
      refresh();
      onDataChanged();
    } catch (error) {
      setStatus({ ok: false, text: (error as Error).message });
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
                {busy
                  ? busy.of > 1
                    ? `Reading file ${busy.done + 1} of ${busy.of}…`
                    : "Reading…"
                  : "Drop files here"}
              </Typography>
              {busy ? (
                <CircularProgress size={24} aria-label="Reading" />
              ) : (
                <Button variant="outlined" component="label">
                  Choose files
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
              )}
            </Stack>
          </Box>

          {busy && <LinearProgress sx={{ mt: 1 }} />}

          {/* The answer where the question was asked. This used to render
              below the weigh-in card — on a phone, 460px further down and off
              the bottom of the screen, so dropping a file looked like it had
              done nothing at all. */}
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

      {/* The sources that bring themselves in. */}
      <IntegrationsCard
        hevyApi={hevyApi}
        intervalsIcu={intervalsIcu}
        onDataChanged={() => {
          refresh();
          onDataChanged();
        }}
      />

      {/* A weigh-in moves the dashboard too: VDOT is per kilogram. */}
      <WeighIn onSaved={onDataChanged} />

      {/* A fitness test is a measurement the readiness gates outrank the
          model with. */}
      <AftEntry onSaved={onDataChanged} />

      <Card variant="outlined">
        <CardContent>
          <Typography variant="h6" sx={{ mb: 0.5 }}>
            Latest activities
          </Typography>
          {/* It always showed fifty and never said so, which made a truncated
              list look like the whole history. */}
          <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
            {total === 0
              ? "Nothing imported yet."
              : total > activities.length
                ? `The newest ${activities.length} of ${total}.`
                : `${total} in total.`}
          </Typography>
          {activities.length === 0 ? null : (
            /* Seven columns do not fit a phone. Without this the Card clipped
               them at the screen edge — Time, Avg HR and Source were simply
               gone, with nothing to scroll. */
            <TableContainer sx={{ overflowX: "auto" }}>
              <Table size="small" sx={{ minWidth: 560 }}>
                <TableHead>
                  <TableRow>
                    <TableCell>When</TableCell>
                    <TableCell>Sport</TableCell>
                    <TableCell>Name</TableCell>
                    <TableCell align="right">Distance</TableCell>
                    <TableCell align="right">Time</TableCell>
                    <TableCell align="right">Avg HR</TableCell>
                    <TableCell align="right">Load</TableCell>
                    <TableCell>Source</TableCell>
                    <TableCell />
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
                      <TableCell align="right">
                        {activity.sport === "ruck" ? (
                          <LoadField
                            activity={activity}
                            onSaved={() => {
                              refresh();
                              onDataChanged();
                            }}
                            onError={(text) => setStatus({ ok: false, text })}
                          />
                        ) : (
                          "—"
                        )}
                      </TableCell>
                      <TableCell>{activity.source}</TableCell>
                      <TableCell align="right" padding="none">
                        {/* A wrong file used to be correctable only in the
                          database. */}
                        <Tooltip title="Remove this activity">
                          <IconButton
                            size="small"
                            aria-label={`Remove ${activity.name || activity.sport} of ${activity.startedAt.slice(0, 10)}`}
                            onClick={() => {
                              if (
                                window.confirm(
                                  `Remove this ${activity.sport} of ${activity.startedAt.slice(0, 10)}? Re-importing the file brings it back.`,
                                )
                              ) {
                                void remove(activity);
                              }
                            }}
                          >
                            <DeleteOutlineIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </CardContent>
      </Card>
    </Stack>
  );
}

function WeighIn({ onSaved }: { onSaved: () => void }) {
  // The local date, not the UTC one. toISOString() dated a weigh-in taken
  // before 03:00 in Jordan to the day before.
  const [date, setDate] = React.useState(() => {
    const now = new Date();
    const local = new Date(now.getTime() - now.getTimezoneOffset() * 60_000);
    return local.toISOString().slice(0, 10);
  });
  const [pounds, setPounds] = React.useState("");
  const [bodyFat, setBodyFat] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const [saved, setSaved] = React.useState<string | null>(null);

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
      setSaved(`Saved: ${lb} lb on ${date}.`);
      onSaved();
    } catch {
      setSaved(null);
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

/** "3 hours ago" for a sync stamp, or "never". */
function ago(iso: string | null, now: Date = new Date()): string {
  if (iso === null) return "never";
  const minutes = Math.round(
    (now.getTime() - new Date(iso).getTime()) / 60_000,
  );
  if (minutes < 2) return "just now";
  if (minutes < 120) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 48) return `${hours} h ago`;
  return `${Math.round(hours / 24)} days ago`;
}

/**
 * The sources that bring themselves in: Hevy once a day when a key is
 * configured, intervals.icu every hour when one is. Each says when it last
 * ran and what happened, because a sync that fails silently is the old
 * export ritual with extra steps.
 */
function IntegrationsCard({
  hevyApi,
  intervalsIcu,
  onDataChanged,
}: {
  hevyApi: boolean;
  intervalsIcu: boolean;
  onDataChanged: () => void;
}) {
  const [status, setStatus] = React.useState<IngestStatus | null>(null);
  const [note, setNote] = React.useState<{ ok: boolean; text: string } | null>(
    null,
  );
  const [busy, setBusy] = React.useState<"hevy" | "icu" | null>(null);

  const refresh = React.useCallback(() => {
    fetchIngestStatus()
      .then(setStatus)
      .catch(() => setStatus(null));
  }, []);

  React.useEffect(refresh, [refresh]);

  const run = async (source: "hevy" | "icu") => {
    setBusy(source);
    try {
      const result =
        source === "hevy" ? await syncHevy() : await syncIntervalsIcu();
      setNote({
        ok: true,
        text: `${source === "hevy" ? "Hevy" : "intervals.icu"}: ${result.fetched} seen, ${result.added} new.`,
      });
      refresh();
      onDataChanged();
    } catch (error) {
      setNote({ ok: false, text: (error as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const line = (s: SourceStatus | undefined) =>
    `Last sync ${ago(s?.lastSyncedAt ?? s?.lastRunAt ?? null)}${s?.lastOutcome ? ` — ${s.lastOutcome}` : ""}.`;

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6" sx={{ mb: 0.5 }}>
          Automatic sources
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          Hevy is pulled once a day, intervals.icu every hour. Garmin Connect
          pushes to intervals.icu on its own, and intervals.icu keeps the
          original FIT file, so what arrives here is exactly what an upload
          would have been: laps and the treadmill flag included.
        </Typography>

        <Stack spacing={2}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={1}
            sx={{ alignItems: { sm: "center" } }}
          >
            <Typography variant="body2" sx={{ minWidth: 200 }}>
              <strong>Hevy</strong>{" "}
              {hevyApi ? (
                <Chip size="small" color="success" label="daily" />
              ) : (
                <Chip size="small" label="key not configured" />
              )}
            </Typography>
            <Typography
              variant="caption"
              sx={{ color: "text.secondary", flex: 1 }}
            >
              {hevyApi
                ? line(status?.hevy)
                : "Live sync appears once a Hevy Pro API key is configured (~$24/year, optional); the CSV export works without it."}
            </Typography>
            {hevyApi && (
              <Button
                size="small"
                variant="outlined"
                disabled={busy !== null}
                onClick={() => void run("hevy")}
              >
                Sync Hevy now
              </Button>
            )}
          </Stack>

          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={1}
            sx={{ alignItems: { sm: "center" } }}
          >
            <Typography variant="body2" sx={{ minWidth: 200 }}>
              <strong>intervals.icu</strong>{" "}
              {intervalsIcu ? (
                <Chip size="small" color="success" label="hourly" />
              ) : (
                <Chip size="small" label="key not configured" />
              )}
            </Typography>
            <Typography
              variant="caption"
              sx={{ color: "text.secondary", flex: 1 }}
            >
              {intervalsIcu
                ? line(status?.intervalsIcu)
                : "Appears once an intervals.icu personal API key (free; Settings → Developer) is configured and Garmin Connect is linked to intervals.icu."}
            </Typography>
            {intervalsIcu && (
              <Button
                size="small"
                variant="outlined"
                disabled={busy !== null}
                onClick={() => void run("icu")}
              >
                Sync intervals.icu now
              </Button>
            )}
          </Stack>
        </Stack>

        {note && (
          <Alert
            severity={note.ok ? "success" : "error"}
            onClose={() => setNote(null)}
            sx={{ mt: 2 }}
          >
            {note.text}
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}
