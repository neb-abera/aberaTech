import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import Stack from "@mui/material/Stack";
import Switch from "@mui/material/Switch";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import type { AlertsState } from "../core/api";
import {
  createRoutine,
  dayNames,
  daySummary,
  deleteRoutine,
  type Routine,
  type RoutineFields,
  type RoutineResult,
  routineBounds,
  routineTime,
  updateRoutine,
} from "../core/routines";

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

interface Draft {
  time: string;
  days: number[];
  label: string;
  snooze: string;
}

const blank: Draft = {
  time: "07:00",
  days: [],
  label: "",
  snooze: String(routineBounds.defaultSnooze),
};

function draftOf(routine: Routine): Draft {
  return {
    time: routineTime(routine.hour, routine.minute),
    days: [...routine.days],
    label: routine.label,
    snooze: String(routine.snoozeMinutes),
  };
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
 * seen and changed from any computer. Each shows its time, its days, its
 * label and an on or off switch, with Edit and Delete. The form adds one,
 * or changes the one being edited.
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
    setErrors({});
    const fields: RoutineFields = {
      label: draft.label.trim(),
      hour: Number(match[1]),
      minute: Number(match[2]),
      days: [...draft.days].sort((a, b) => a - b),
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
      setDraft(blank);
    }
  };

  const toggleDay = (day: number) =>
    setDraft((current) => ({
      ...current,
      days: current.days.includes(day)
        ? current.days.filter((d) => d !== day)
        : [...current.days, day],
    }));

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
        Routine alarms ring on the paired phone like Clock alarms. They do not
        go through Pushover or ring in this browser.
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
          {routines.map((routine) => {
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
                    {daySummary(routine.days)}, {routine.label}
                  </Typography>
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    Snooze {routine.snoozeMinutes} min.{" "}
                    {routine.enabled ? "On" : "Off"}
                  </Typography>
                </Box>
                <Switch
                  checked={routine.enabled}
                  disabled={busy}
                  onChange={(_, checked) =>
                    void run(
                      () =>
                        api.update(routine.id, {
                          ...fieldsOf(routine),
                          enabled: checked,
                        }),
                      `${name} is ${checked ? "on" : "off"}.`,
                    )
                  }
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
                          setDraft(blank);
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
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {draft.days.length === 0
            ? "No repeat. It rings at the next time shown until you stop it, then switches off."
            : `Repeats: ${daySummary(draft.days)}. It rings until you stop it.`}
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
                setDraft(blank);
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
