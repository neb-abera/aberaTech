import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogTitle from "@mui/material/DialogTitle";
import FormControl from "@mui/material/FormControl";
import FormControlLabel from "@mui/material/FormControlLabel";
import FormLabel from "@mui/material/FormLabel";
import Radio from "@mui/material/Radio";
import RadioGroup from "@mui/material/RadioGroup";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import {
  type AlertItem,
  type AlertsState,
  deleteEvent,
  type EventEdit,
  type EventScope,
  type EventWriteResult,
  editEvent,
} from "../core/api";
import { newEventBounds } from "./NewEventForm";

/** "2026-10-28T09:00": an instant as a datetime-local input shows it, in this browser's zone. */
export function localInput(iso: string): string {
  const at = new Date(iso);
  const pad = (value: number) => String(value).padStart(2, "0");
  return `${at.getFullYear()}-${pad(at.getMonth() + 1)}-${pad(at.getDate())}T${pad(at.getHours())}:${pad(at.getMinutes())}`;
}

function minutesBetween(from: string, to: string): number {
  return Math.round(
    (new Date(to).getTime() - new Date(from).getTime()) / 60_000,
  );
}

/** Why an edit or a deletion did not happen, in the page's words. */
export function explainWrite(
  result: Exclude<EventWriteResult, { ok: true }>,
): string {
  switch (result.reason) {
    case "conflict":
    case "google":
      return result.detail ?? "Google Calendar did not take the change.";
    case "gone":
      return "This event is no longer listed. Close this and refresh.";
    case "throttled":
      return "Too many presses. Wait a minute.";
    case "visitor":
      return "The session expired. Reload and sign in again.";
    case "refused":
      return "The server refused. Refresh and try again.";
    default:
      return "No answer from the server. Check the connection and try again.";
  }
}

/** "This event" or "All events", for an event that repeats. */
function ScopeChoice({
  label,
  scope,
  onChange,
}: {
  label: string;
  scope: EventScope;
  onChange: (scope: EventScope) => void;
}) {
  return (
    <FormControl>
      <FormLabel>{label}</FormLabel>
      <RadioGroup
        row
        value={scope}
        onChange={(event) => onChange(event.target.value as EventScope)}
      >
        <FormControlLabel
          value="occurrence"
          control={<Radio />}
          label="This event"
        />
        <FormControlLabel
          value="series"
          control={<Radio />}
          label="All events"
        />
      </RadioGroup>
    </FormControl>
  );
}

/**
 * Edit one listed event: the New event form's fields, filled in. For an
 * event that repeats, the change is for this event or all of them. The
 * type is not here: the list's own buttons set it.
 */
export function EditEventDialog({
  alert,
  when,
  onClose,
  onSaved,
  edit = editEvent,
}: {
  alert: AlertItem;
  when: (iso: string) => string;
  onClose: () => void;
  onSaved: (state: AlertsState, notice: string) => void;
  edit?: (body: EventEdit) => Promise<EventWriteResult>;
}) {
  const startLength = alert.endsAt
    ? String(minutesBetween(alert.startsAt, alert.endsAt))
    : "";
  const startLead =
    alert.source === "reminder"
      ? String(minutesBetween(alert.alertAt, alert.startsAt))
      : "";
  const [title, setTitle] = React.useState(alert.title);
  const [start, setStart] = React.useState(() => localInput(alert.startsAt));
  const [duration, setDuration] = React.useState(startLength);
  const [location, setLocation] = React.useState(alert.location ?? "");
  const [lead, setLead] = React.useState(startLead);
  const [scope, setScope] = React.useState<EventScope>("occurrence");
  const [busy, setBusy] = React.useState(false);
  const [problem, setProblem] = React.useState<string | null>(null);
  const [errors, setErrors] = React.useState<Record<string, string[]>>({});

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    setProblem(null);
    const starts = new Date(start);
    if (Number.isNaN(starts.getTime())) {
      setErrors({ startsAt: ["A date and a time."] });
      return;
    }
    setErrors({});
    setBusy(true);
    // A length or a reminder left as it was is left out, so the event
    // keeps its own.
    const result = await edit({
      key: alert.key,
      scope,
      title: title.trim(),
      startsAt: starts.toISOString(),
      location: location.trim() === "" ? null : location.trim(),
      ...(duration.trim() === "" || duration === startLength
        ? {}
        : { durationMinutes: Number(duration) }),
      ...(lead.trim() === "" || lead === startLead
        ? {}
        : { leadMinutes: Number(lead) }),
    });
    setBusy(false);
    if (result.ok) {
      onSaved(
        result.state,
        scope === "series"
          ? `${title.trim()} is changed, every occurrence.`
          : `${title.trim()} is changed.`,
      );
      return;
    }
    if (result.reason === "invalid") {
      setErrors(result.errors ?? {});
      return;
    }
    setProblem(explainWrite(result));
  };

  const help = (field: string, text: string) => errors[field]?.[0] ?? text;

  return (
    <Dialog open onClose={busy ? undefined : onClose} fullWidth maxWidth="sm">
      <form onSubmit={(event) => void save(event)} noValidate>
        <DialogTitle>Edit {alert.title}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              Now {when(alert.startsAt)}. The change goes to Google Calendar.
              The type stays as it is.
            </Typography>
            {alert.recurring && (
              <ScopeChoice
                label="Change which events"
                scope={scope}
                onChange={setScope}
              />
            )}
            <TextField
              label="Title"
              value={title}
              required
              onChange={(event) => setTitle(event.target.value)}
              error={Boolean(errors.title)}
              helperText={help(
                "title",
                `1 to ${newEventBounds.title} characters.`,
              )}
              slotProps={{ htmlInput: { maxLength: newEventBounds.title } }}
            />
            <TextField
              label="Starts"
              type="datetime-local"
              value={start}
              required
              onChange={(event) => setStart(event.target.value)}
              error={Boolean(errors.startsAt)}
              helperText={help(
                "startsAt",
                scope === "series"
                  ? "In this browser's time zone. Every event moves by the same change in date and time."
                  : "In this browser's time zone.",
              )}
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              label="Duration in minutes"
              type="number"
              value={duration}
              onChange={(event) => setDuration(event.target.value)}
              error={Boolean(errors.durationMinutes)}
              helperText={help(
                "durationMinutes",
                `${newEventBounds.duration.min} to ${newEventBounds.duration.max}. Empty keeps the length.`,
              )}
              slotProps={{
                htmlInput: {
                  min: newEventBounds.duration.min,
                  max: newEventBounds.duration.max,
                },
              }}
            />
            <TextField
              label="Location"
              value={location}
              onChange={(event) => setLocation(event.target.value)}
              error={Boolean(errors.location)}
              helperText={help(
                "location",
                `Optional, at most ${newEventBounds.location} characters. Empty clears it.`,
              )}
              slotProps={{ htmlInput: { maxLength: newEventBounds.location } }}
            />
            <TextField
              label="Reminder in minutes before"
              type="number"
              value={lead}
              onChange={(event) => setLead(event.target.value)}
              error={Boolean(errors.leadMinutes)}
              helperText={help(
                "leadMinutes",
                `${newEventBounds.lead.min} to ${newEventBounds.lead.max}. Empty keeps the event's reminders.`,
              )}
              slotProps={{
                htmlInput: {
                  min: newEventBounds.lead.min,
                  max: newEventBounds.lead.max,
                },
              }}
            />
            {problem && <Alert severity="warning">{problem}</Alert>}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={busy}>
            Save
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}

/**
 * Delete one listed event after a yes. For an event that repeats, this
 * event or all of them.
 */
export function DeleteEventDialog({
  alert,
  when,
  onClose,
  onDeleted,
  remove = deleteEvent,
}: {
  alert: AlertItem;
  when: (iso: string) => string;
  onClose: () => void;
  onDeleted: (state: AlertsState, notice: string) => void;
  remove?: (key: string, scope: EventScope) => Promise<EventWriteResult>;
}) {
  const [scope, setScope] = React.useState<EventScope>("occurrence");
  const [busy, setBusy] = React.useState(false);
  const [problem, setProblem] = React.useState<string | null>(null);

  const confirm = async () => {
    setProblem(null);
    setBusy(true);
    const result = await remove(alert.key, scope);
    setBusy(false);
    if (result.ok) {
      onDeleted(
        result.state,
        scope === "series"
          ? `${alert.title} is deleted, every occurrence.`
          : `${alert.title} is deleted.`,
      );
      return;
    }
    setProblem(explainWrite(result));
  };

  return (
    <Dialog open onClose={busy ? undefined : onClose} fullWidth maxWidth="xs">
      <DialogTitle>Delete {alert.title}?</DialogTitle>
      <DialogContent>
        <Stack spacing={2}>
          <Typography variant="body2">
            {alert.recurring
              ? `It repeats. This event is ${when(alert.startsAt)}.`
              : `It is ${when(alert.startsAt)}.`}{" "}
            It goes from Google Calendar and no alert is sent for it.
          </Typography>
          {alert.recurring && (
            <ScopeChoice
              label="Delete which events"
              scope={scope}
              onChange={setScope}
            />
          )}
          {problem && <Alert severity="warning">{problem}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>
          Cancel
        </Button>
        <Button
          color="error"
          variant="contained"
          disabled={busy}
          onClick={() => void confirm()}
        >
          Delete
        </Button>
      </DialogActions>
    </Dialog>
  );
}
