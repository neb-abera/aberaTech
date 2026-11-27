import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import Switch from "@mui/material/Switch";
import TextField from "@mui/material/TextField";
import ToggleButton from "@mui/material/ToggleButton";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";
import Typography from "@mui/material/Typography";
import * as React from "react";
import { type AlertsState, isUnreachable } from "../core/api";
import {
  createRoutine,
  dayNames,
  deleteRoutine,
  type RepeatFields,
  type Routine,
  type RoutineFields,
  type RoutineResult,
  type RoutineUnit,
  repeatErrors,
  repeatOf,
  repeatSummary,
  routineBounds,
  routineTime,
  routineUnits,
  startDay,
  stepSummary,
  todayDay,
  updateRoutine,
} from "../core/routines";
import SyncNote from "./SyncNote";
import { type SendOutcome, usePendingChanges } from "./usePendingChanges";

/** What the section calls. The page passes the real ones, a test its own. */
export interface RoutinesApi {
  create: (fields: RoutineFields) => Promise<RoutineResult>;
  update: (id: string, fields: RoutineFields) => Promise<RoutineResult>;
  remove: (id: string) => Promise<RoutineResult>;
}

export const routinesApi: RoutinesApi = {
  create: createRoutine,
  update: updateRoutine,
  remove: deleteRoutine,
};

const dayLongNames = [
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
  "Sunday",
] as const;

/** The form's three ways to repeat: once, on weekdays, or every N units. */
type Mode = "none" | "weekdays" | "interval";

interface Draft {
  time: string;
  mode: Mode;
  days: number[];
  /** The N of "every N", as typed. Weeks on weekdays, the unit on an interval. */
  every: string;
  unit: RoutineUnit;
  /** "yyyy-MM-dd": the date an interval, or weekdays every N weeks, counts from. */
  startsOn: string;
  label: string;
  snooze: string;
}

/** A new alarm: no repeat, and a start date of today on this computer. */
const blank = (): Draft => ({
  time: "07:00",
  mode: "none",
  days: [],
  every: "1",
  unit: "weeks",
  startsOn: todayDay(),
  label: "",
  snooze: String(routineBounds.defaultSnooze),
});

function draftOf(routine: Routine): Draft {
  const repeat = repeatOf(routine);
  const interval = repeat.repeat === "interval";
  return {
    time: routineTime(routine.hour, routine.minute),
    mode: interval ? "interval" : routine.days.length > 0 ? "weekdays" : "none",
    days: interval ? [] : [...routine.days],
    every: String(repeat.every),
    unit: repeat.unit,
    startsOn: repeat.startsOn ?? todayDay(),
    label: routine.label,
    snooze: String(routine.snoozeMinutes),
  };
}

/** The repeat a draft would save, with the days it would send. */
function repeatFrom(draft: Draft): RepeatFields & { days: number[] } {
  if (draft.mode === "interval") {
    return {
      days: [],
      repeat: "interval",
      every: Number(draft.every),
      unit: draft.unit,
      startsOn: draft.startsOn || null,
    };
  }
  const every = draft.mode === "none" ? 1 : Number(draft.every);
  return {
    days: draft.mode === "none" ? [] : [...draft.days].sort((a, b) => a - b),
    repeat: "weekdays",
    every,
    unit: "weeks",
    // Every week counts from nothing, and the server stores no date for it.
    startsOn: every === 1 ? null : draft.startsOn || null,
  };
}

/** The sentence under the repeat controls: what the alarm will do as the form stands. */
function repeatHelp(draft: Draft): string {
  if (draft.mode === "none") {
    return "No repeat. It rings at the next time shown until you stop it, then switches off.";
  }
  const repeat = repeatFrom(draft);
  if (draft.mode === "weekdays" && repeat.days.length === 0) {
    return "Pick the days it rings on.";
  }
  if (Object.keys(repeatErrors(repeat)).length > 0) {
    return draft.mode === "interval"
      ? "Every so many minutes, hours, days, weeks, months, or years, counted from the start date."
      : "Every so many weeks on the days picked, counted from the week of the start date.";
  }
  const from =
    draft.mode === "interval"
      ? `, from ${startDay(repeat.startsOn)} at ${draft.time}`
      : repeat.startsOn
        ? `, from ${startDay(repeat.startsOn)}`
        : "";
  return `Repeats: ${repeatSummary(repeat)}${from}. It rings until you stop it.`;
}

function fieldsOf(routine: Routine): RoutineFields {
  return {
    label: routine.label,
    hour: routine.hour,
    minute: routine.minute,
    days: routine.days,
    enabled: routine.enabled,
    snoozeMinutes: routine.snoozeMinutes,
  };
}

function explain(result: Exclude<RoutineResult, { ok: true }>): string {
  switch (result.reason) {
    case "full":
      return (
        result.detail ??
        `At most ${routineBounds.max} routine alarms. Delete one first.`
      );
    case "missing":
      return "That routine alarm is gone. The list below is current.";
    case "throttled":
      return "Too many presses. Wait a minute.";
    case "visitor":
      return "The session expired. Reload and sign in again.";
    case "invalid":
      return "Check the fields marked below.";
    case "refused":
      return "The server refused. Refresh and try again.";
    default:
      return "No answer from the server. Check the connection and try again.";
  }
}

/**
 * Routine alarms: the phone's everyday alarms, kept here so they can be
 * seen and changed from any computer. Each shows its time, how it
 * repeats, its label and an on or off switch, with Edit and Delete. The
 * form adds one, or changes the one being edited. It repeats on days of
 * the week, every week or every N weeks, or every N minutes, hours, days,
 * weeks, months or years from a start date.
 */
export default function RoutinesSection({
  routines,
  onState,
  api = routinesApi,
}: {
  routines: Routine[];
  onState: (state: AlertsState) => void;
  api?: RoutinesApi;
}) {
  const [draft, setDraft] = React.useState<Draft>(blank);
  const [editing, setEditing] = React.useState<Routine | null>(null);
  const [confirming, setConfirming] = React.useState<string | null>(null);
  const [busy, setBusy] = React.useState(false);
  const [problem, setProblem] = React.useState<string | null>(null);
  const [errors, setErrors] = React.useState<Record<string, string[]>>({});
  const [done, setDone] = React.useState<string | null>(null);

  // The switch moves when pressed. The server's answer only confirms it.
  const latest = React.useRef(routines);
  React.useEffect(() => {
    latest.current = routines;
  });
  const switches = usePendingChanges<boolean>(
    async (id, enabled): Promise<SendOutcome> => {
      const routine = latest.current.find((r) => r.id === id);
      // Deleted while the switch waited: nothing is left to send.
      if (!routine) return { ok: true };
      const result = await api.update(id, { ...fieldsOf(routine), enabled });
      if (result.ok) {
        if (result.state) onState(result.state);
        const time = routineTime(routine.hour, routine.minute);
        setDone(`${routine.label} at ${time} is ${enabled ? "on" : "off"}.`);
        return { ok: true };
      }
      if (isUnreachable(result)) return { ok: false, offline: true };
      return { ok: false, offline: false, problem: explain(result) };
    },
    (_id, reason) => setProblem(reason),
  );
  /** The routine as the page shows it: a switch pressed and not yet confirmed wins. */
  const shown = (routine: Routine): Routine => {
    const change = switches.pending[routine.id];
    return change ? { ...routine, enabled: change.value } : routine;
  };

  const run = async (
    press: () => Promise<RoutineResult>,
    message: string,
  ): Promise<boolean> => {
    setBusy(true);
    setProblem(null);
    setDone(null);
    const result = await press();
    setBusy(false);
    if (result.ok) {
      if (result.state) onState(result.state);
      setDone(message);
      return true;
    }
    if (result.reason === "invalid") setErrors(result.errors ?? {});
    setProblem(explain(result));
    return false;
  };

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    const match = /^(\d{2}):(\d{2})/.exec(draft.time);
    if (!match) {
      setErrors({ hour: ["A time of day."] });
      return;
    }
    const { days, ...repeat } = repeatFrom(draft);
    // The server's rules, checked here first so the reason shows at once.
    const refused =
      draft.mode === "weekdays" && days.length === 0 && repeat.every === 1
        ? { days: ["Pick at least one day, or choose No repeat."] }
        : repeatErrors({ days, ...repeat });
    if (Object.keys(refused).length > 0) {
      setErrors(refused);
      setProblem("Check the fields marked below.");
      return;
    }
    setErrors({});
    const fields: RoutineFields = {
      label: draft.label.trim(),
      hour: Number(match[1]),
      minute: Number(match[2]),
      days,
      ...repeat,
      enabled: editing ? editing.enabled : true,
      snoozeMinutes: Number(draft.snooze),
    };
    const time = routineTime(fields.hour, fields.minute);
    const saved = editing
      ? await run(
          () => api.update(editing.id, fields),
          `The routine alarm at ${time} is saved.`,
        )
      : await run(
          () => api.create(fields),
          `A routine alarm at ${time} is added.`,
        );
    if (saved) {
      setEditing(null);
      setDraft(blank());
    }
  };

  // A day pressed is a repeat on days of the week, with no trip to the
  // choice above it. The last day pressed off is no repeat again.
  const toggleDay = (day: number) =>
    setDraft((current) => {
      const days = current.days.includes(day)
        ? current.days.filter((d) => d !== day)
        : [...current.days, day];
      return { ...current, days, mode: days.length > 0 ? "weekdays" : "none" };
    });

  const chooseMode = (mode: Mode) =>
    setDraft((current) => ({
      ...current,
      mode,
      days: mode === "none" ? [] : current.days,
      // The number belongs to its unit, so another way to repeat starts at 1.
      every: mode === current.mode ? current.every : "1",
    }));

  const everyField = (
    <TextField
      label="Every"
      type="number"
      value={draft.every}
      onChange={(event) =>
        setDraft((current) => ({ ...current, every: event.target.value }))
      }
      error={Boolean(errors.every)}
      helperText={errors.every?.[0]}
      slotProps={{
        htmlInput: {
          min: routineBounds.every.min,
          max: routineBounds.every.max,
          step: 1,
        },
      }}
      sx={{ width: "9rem" }}
    />
  );
  const startField = (
    <TextField
      label="Starting"
      type="date"
      value={draft.startsOn}
      onChange={(event) =>
        setDraft((current) => ({ ...current, startsOn: event.target.value }))
      }
      error={Boolean(errors.startsOn)}
      helperText={errors.startsOn?.[0]}
      slotProps={{ inputLabel: { shrink: true } }}
      sx={{ width: "12rem" }}
    />
  );

  const help = (field: string, text: string) => errors[field]?.[0] ?? text;
  const timeError = errors.hour ?? errors.minute;

  return (
    <Box component="section" aria-labelledby="routines-heading">
      <Typography
        id="routines-heading"
        variant="h2"
        sx={{ fontSize: "1.25rem", mb: 1 }}
      >
        Routine alarms
      </Typography>
      <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
        Routine alarms ring on the paired phone like Clock alarms. They also
        ring through Pushover, and here when Ring in this browser is on.
        Acknowledging one anywhere stops it everywhere.
      </Typography>

      {routines.length === 0 ? (
        <Typography variant="body1" sx={{ color: "text.secondary", mb: 2 }}>
          No routine alarms.
        </Typography>
      ) : (
        <Stack
          component="ul"
          aria-label="Routine alarms"
          spacing={1}
          sx={{ listStyle: "none", p: 0, m: 0, mb: 2 }}
        >
          {routines.map((stored) => {
            const routine = shown(stored);
            const time = routineTime(routine.hour, routine.minute);
            const name = `${routine.label} at ${time}`;
            return (
              <Box
                component="li"
                key={routine.id}
                sx={{
                  border: 1,
                  borderColor: "divider",
                  borderRadius: 1,
                  p: 1.5,
                  display: "flex",
                  gap: 1,
                  alignItems: "center",
                  flexWrap: "wrap",
                }}
              >
                <Box sx={{ flex: "1 1 14rem", minWidth: 0 }}>
                  <Typography variant="body1" sx={{ fontWeight: 600 }}>
                    {time}
                  </Typography>
                  <Typography variant="body2">
                    {repeatSummary(routine)}, {routine.label}
                  </Typography>
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    {routine.startsOn
                      ? `From ${startDay(routine.startsOn)}. `
                      : ""}
                    Snooze {routine.snoozeMinutes} min.{" "}
                    {routine.enabled ? "On" : "Off"}
                  </Typography>
                  {routine.steps && routine.steps.length > 0 && (
                    <Box
                      component="ol"
                      aria-label={`Steps of ${name}`}
                      sx={{ m: 0, mt: 0.5, pl: 3 }}
                    >
                      {routine.steps.map((step) => (
                        <Typography
                          component="li"
                          variant="body2"
                          key={step.id}
                        >
                          {stepSummary(step)}
                        </Typography>
                      ))}
                    </Box>
                  )}
                  <SyncNote status={switches.pending[routine.id]?.status} />
                </Box>
                <Switch
                  checked={routine.enabled}
                  onChange={(_, checked) => {
                    setProblem(null);
                    setDone(null);
                    switches.change(routine.id, checked);
                  }}
                  slotProps={{ input: { "aria-label": name } }}
                />
                <Button
                  size="small"
                  variant="outlined"
                  disabled={busy}
                  onClick={() => {
                    setEditing(routine);
                    setDraft(draftOf(routine));
                    setErrors({});
                    setDone(null);
                  }}
                  aria-label={`Edit ${name}`}
                >
                  Edit
                </Button>
                {confirming === routine.id ? (
                  <>
                    <Button
                      size="small"
                      color="error"
                      variant="contained"
                      disabled={busy}
                      onClick={() => {
                        setConfirming(null);
                        if (editing?.id === routine.id) {
                          setEditing(null);
                          setDraft(blank());
                        }
                        void run(
                          () => api.remove(routine.id),
                          `${name} is deleted.`,
                        );
                      }}
                      aria-label={`Yes, delete ${name}`}
                    >
                      Yes, delete
                    </Button>
                    <Button size="small" onClick={() => setConfirming(null)}>
                      Cancel
                    </Button>
                  </>
                ) : (
                  <Button
                    size="small"
                    variant="outlined"
                    color="error"
                    disabled={busy}
                    onClick={() => setConfirming(routine.id)}
                    aria-label={`Delete ${name}`}
                  >
                    Delete
                  </Button>
                )}
              </Box>
            );
          })}
        </Stack>
      )}

      {problem && (
        <Alert
          severity="warning"
          sx={{ mb: 2 }}
          onClose={() => setProblem(null)}
        >
          {problem}
        </Alert>
      )}
      {done && (
        <Alert severity="info" sx={{ mb: 2 }} onClose={() => setDone(null)}>
          {done}
        </Alert>
      )}

      <Stack
        component="form"
        spacing={2}
        onSubmit={(event) => void submit(event)}
        aria-label={editing ? "Edit routine alarm" : "Add routine alarm"}
        noValidate
      >
        <Typography variant="body1" sx={{ fontWeight: 600 }}>
          {editing
            ? `Edit the routine alarm at ${routineTime(editing.hour, editing.minute)}`
            : "Add routine alarm"}
        </Typography>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          <TextField
            label="Time"
            type="time"
            value={draft.time}
            required
            onChange={(event) =>
              setDraft((current) => ({ ...current, time: event.target.value }))
            }
            error={Boolean(timeError)}
            helperText={timeError?.[0] ?? "On the phone's clock, 24 hours."}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <TextField
            label="Snooze in minutes"
            type="number"
            value={draft.snooze}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                snooze: event.target.value,
              }))
            }
            error={Boolean(errors.snoozeMinutes)}
            helperText={help(
              "snoozeMinutes",
              `${routineBounds.snooze.min} to ${routineBounds.snooze.max}.`,
            )}
            slotProps={{
              htmlInput: {
                min: routineBounds.snooze.min,
                max: routineBounds.snooze.max,
              },
            }}
          />
        </Stack>
        <Stack
          direction="row"
          spacing={1}
          useFlexGap
          sx={{ alignItems: "center", flexWrap: "wrap" }}
        >
          <Typography variant="body2" id="routine-repeat-label">
            Repeat:
          </Typography>
          <ToggleButtonGroup
            exclusive
            size="small"
            value={draft.mode}
            onChange={(_, value: Mode | null) => value && chooseMode(value)}
            aria-labelledby="routine-repeat-label"
            // The theme's toggle text measured 4.26:1 picked in the dark
            // scheme and 4.45:1 unpicked in the light one. The page's own
            // text color clears 4.5:1 in both, and the fill marks the pick.
            sx={{
              "& .MuiToggleButton-root, & .MuiToggleButton-root.Mui-selected": {
                color: "text.primary",
              },
              "& .MuiToggleButton-root.Mui-selected": { fontWeight: 600 },
            }}
          >
            <ToggleButton value="none">No repeat</ToggleButton>
            <ToggleButton value="weekdays">Days of the week</ToggleButton>
            <ToggleButton value="interval">Every…</ToggleButton>
          </ToggleButtonGroup>
        </Stack>
        {draft.mode !== "interval" && (
          <Stack
            direction="row"
            spacing={1}
            useFlexGap
            role="group"
            aria-label="Days"
            sx={{ alignItems: "center", flexWrap: "wrap" }}
          >
            <Typography variant="body2">Days:</Typography>
            {dayNames.map((day, index) => {
              const number = index + 1;
              const on = draft.days.includes(number);
              return (
                <Chip
                  key={day}
                  label={day}
                  aria-label={`Repeat on ${dayLongNames[index]}`}
                  aria-pressed={on}
                  color={on ? "primary" : "default"}
                  variant={on ? "filled" : "outlined"}
                  onClick={() => toggleDay(number)}
                />
              );
            })}
          </Stack>
        )}
        {errors.days && (
          <Typography variant="body2" role="alert" sx={{ color: "error.main" }}>
            {errors.days[0]}
          </Typography>
        )}
        {draft.mode === "weekdays" && (
          <Stack
            direction="row"
            spacing={2}
            useFlexGap
            sx={{ alignItems: "flex-start", flexWrap: "wrap" }}
          >
            {everyField}
            <Typography variant="body1" sx={{ pt: 2 }}>
              {Number(draft.every) === 1 ? "week" : "weeks"}
            </Typography>
            {Number(draft.every) > 1 && startField}
          </Stack>
        )}
        {draft.mode === "interval" && (
          <Stack
            direction="row"
            spacing={2}
            useFlexGap
            sx={{ alignItems: "flex-start", flexWrap: "wrap" }}
          >
            {everyField}
            <TextField
              select
              label="Unit"
              value={draft.unit}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  unit: event.target.value as RoutineUnit,
                }))
              }
              error={Boolean(errors.unit)}
              helperText={errors.unit?.[0]}
              sx={{ width: "10rem" }}
            >
              {routineUnits.map((choice) => (
                <MenuItem key={choice.value} value={choice.value}>
                  {Number(draft.every) === 1 ? choice.one : choice.many}
                </MenuItem>
              ))}
            </TextField>
            {startField}
          </Stack>
        )}
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {errors.repeat?.[0] ?? repeatHelp(draft)}
        </Typography>
        <TextField
          label="Label"
          value={draft.label}
          onChange={(event) =>
            setDraft((current) => ({ ...current, label: event.target.value }))
          }
          error={Boolean(errors.label)}
          helperText={help(
            "label",
            `At most ${routineBounds.label} characters. Empty is saved as Alarm.`,
          )}
          slotProps={{ htmlInput: { maxLength: routineBounds.label } }}
        />
        <Stack direction="row" spacing={1}>
          <Button type="submit" variant="contained" disabled={busy}>
            {editing ? "Save routine alarm" : "Add routine alarm"}
          </Button>
          {editing && (
            <Button
              onClick={() => {
                setEditing(null);
                setDraft(blank());
                setErrors({});
              }}
            >
              Cancel edit
            </Button>
          )}
        </Stack>
      </Stack>
    </Box>
  );
}
