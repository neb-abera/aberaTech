import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlineOutlined";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
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
  saveActivityLoad,
  saveAftResult,
  saveBodyMetric,
  syncHevy,
  uploadFile,
} from "../core/api";
import { formatSeconds, kgToLb, lbToKg, parseClock } from "../core/format";
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

/**
 * The load a ruck was carried at, typed in pounds or kilograms. No watch
 * records it, and without it the models read a ruck as a walk.
 */
function LoadField({
  activity,
  onSaved,
  onError,
}: {
  activity: ActivityRow;
  onSaved: () => void;
  onError: (text: string) => void;
}) {
  const [unit, setUnit] = React.useState<"lb" | "kg">("lb");
  const [text, setText] = React.useState(() =>
    activity.loadKg === null ? "" : String(Math.round(kgToLb(activity.loadKg))),
  );

  const shown = (loadKg: number | null) =>
    loadKg === null
      ? ""
      : unit === "lb"
        ? String(Math.round(kgToLb(loadKg)))
        : String(Number(loadKg.toFixed(1)));

  const switchUnit = () => {
    const next = unit === "lb" ? "kg" : "lb";
    const value = Number(text);
    setUnit(next);
    if (text.trim() !== "" && Number.isFinite(value) && value > 0) {
      setText(
        next === "kg"
          ? String(Number(lbToKg(value).toFixed(1)))
          : String(Math.round(kgToLb(value))),
      );
    }
  };

  const save = async () => {
    const value = text.trim() === "" ? null : Number(text);
    if (value !== null && (!Number.isFinite(value) || value <= 0)) {
      onError("A load is a positive number of pounds or kilograms.");
      return;
    }
    const loadKg =
      value === null ? null : unit === "lb" ? lbToKg(value) : value;
    if (
      (loadKg === null && activity.loadKg === null) ||
      (loadKg !== null &&
        activity.loadKg !== null &&
        Math.abs(loadKg - activity.loadKg) < 0.05)
    ) {
      return;
    }
    try {
      await saveActivityLoad(activity.id, loadKg);
      onSaved();
    } catch (error) {
      onError((error as Error).message);
      setText(shown(activity.loadKg));
    }
  };

  return (
    <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end" }}>
      <TextField
        size="small"
        variant="standard"
        value={text}
        onChange={(event) => setText(event.target.value)}
        onBlur={() => void save()}
        onKeyDown={(event) => {
          if (event.key === "Enter") (event.target as HTMLInputElement).blur();
        }}
        slotProps={{
          htmlInput: {
            "aria-label": `Load for ${activity.name || "ruck"} of ${activity.startedAt.slice(0, 10)}`,
            inputMode: "decimal",
            style: { textAlign: "right", width: 48 },
          },
        }}
      />
      <Button size="small" onClick={switchUnit} aria-label="Switch load unit">
        {unit}
      </Button>
    </Stack>
  );
}

/**
 * One Army Fitness Test, as taken. The five raw results; the scoring happens
 * on the server against the published tables and comes back with the save.
 */
function AftEntry({ onSaved }: { onSaved: () => void }) {
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
