import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import {
  type AlertsState,
  type AlertType,
  type CreateResult,
  createEvent,
  type NewEvent,
} from "../core/api";
import { typeLabels } from "../core/settings";
import SpanInput from "./SpanInput";

/** What the server takes. It checks the same numbers. */
export const newEventBounds = {
  title: 200,
  location: 200,
  duration: { min: 5, max: 1440 },
  lead: { min: 0, max: 1440 },
  daysAhead: 366,
} as const;

/** "2026-10-28T08:30" for the next whole or half hour, in this browser's zone. */
function nextHalfHour(now: Date): string {
  const next = new Date(now.getTime());
  next.setSeconds(0, 0);
  next.setMinutes(next.getMinutes() < 30 ? 30 : 60);
  const pad = (value: number) => String(value).padStart(2, "0");
  return `${next.getFullYear()}-${pad(next.getMonth() + 1)}-${pad(next.getDate())}T${pad(next.getHours())}:${pad(next.getMinutes())}`;
}

/**
 * A new event on the Google calendar the alerts read, with its type and
 * its reminder. What a paired phone can do, on the page. The event is
 * listed at once: the server keeps it until Google's feed has it.
 */
export default function NewEventForm({
  defaultLead,
  onCreated,
  create = createEvent,
  now = () => new Date(),
}: {
  defaultLead: number;
  onCreated: (state: AlertsState) => void;
  create?: (event: NewEvent) => Promise<CreateResult>;
  now?: () => Date;
}) {
  const [title, setTitle] = React.useState("");
  const [when, setWhen] = React.useState(() => nextHalfHour(now()));
  const [duration, setDuration] = React.useState("30");
  const [location, setLocation] = React.useState("");
  const [type, setType] = React.useState<AlertType>("alarm");
  const [lead, setLead] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  const [problem, setProblem] = React.useState<string | null>(null);
  const [errors, setErrors] = React.useState<Record<string, string[]>>({});
  const [done, setDone] = React.useState<string | null>(null);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setProblem(null);
    setDone(null);
    const start = new Date(when);
    if (Number.isNaN(start.getTime())) {
      setErrors({ startsAt: ["A date and a time."] });
      return;
    }
    setErrors({});
    setBusy(true);
    const result = await create({
      title: title.trim(),
      startsAt: start.toISOString(),
      durationMinutes: Number(duration),
      location: location.trim() === "" ? null : location.trim(),
      type,
      ...(lead.trim() === "" ? {} : { leadMinutes: Number(lead) }),
    });
    setBusy(false);
    if (result.ok) {
      setDone(`${title.trim()} is on the calendar and listed below.`);
      setTitle("");
      setLocation("");
      onCreated(result.state);
      return;
    }
    if (result.reason === "invalid") {
      setErrors(result.errors ?? {});
      return;
    }
    setProblem(
      result.reason === "conflict" || result.reason === "google"
        ? (result.detail ?? "Google Calendar did not take the event.")
        : result.reason === "throttled"
          ? "Too many presses. Wait a minute."
          : result.reason === "visitor"
            ? "The session expired. Reload and sign in again."
            : "No answer from the server. Check the connection and try again.",
    );
  };

  const help = (field: string, text: string) => errors[field]?.[0] ?? text;

  return (
    <Box component="section" aria-labelledby="new-event-heading">
      <Typography
        id="new-event-heading"
        variant="h2"
        sx={{ fontSize: "1.25rem", mb: 1 }}
      >
        New event
      </Typography>
      <Stack
        component="form"
        spacing={2}
        onSubmit={(event) => void submit(event)}
        aria-label="New event"
        noValidate
      >
        <TextField
          label="Title"
          value={title}
          required
          onChange={(event) => setTitle(event.target.value)}
          error={Boolean(errors.title)}
          helperText={help("title", `1 to ${newEventBounds.title} characters.`)}
          slotProps={{ htmlInput: { maxLength: newEventBounds.title } }}
        />
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          <TextField
            label="Starts"
            type="datetime-local"
            value={when}
            required
            onChange={(event) => setWhen(event.target.value)}
            error={Boolean(errors.startsAt)}
            helperText={help(
              "startsAt",
              `In this browser's time zone, at most ${newEventBounds.daysAhead} days ahead.`,
            )}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <SpanInput
            label="Length"
            minutes={duration}
            onChange={setDuration}
            error={Boolean(errors.durationMinutes)}
            helperText={help(
              "durationMinutes",
              `${newEventBounds.duration.min} minutes to ${newEventBounds.duration.max / 60} hours.`,
            )}
          />
        </Stack>
        <TextField
          label="Location"
          value={location}
          onChange={(event) => setLocation(event.target.value)}
          error={Boolean(errors.location)}
          helperText={help(
            "location",
            `Optional, at most ${newEventBounds.location} characters.`,
          )}
          slotProps={{ htmlInput: { maxLength: newEventBounds.location } }}
        />
        <Stack
          direction="row"
          spacing={1}
          useFlexGap
          role="group"
          aria-label="Type of the new event"
          sx={{ alignItems: "center", flexWrap: "wrap" }}
        >
          <Typography variant="body2">Type:</Typography>
          {(["none", "notification", "alarm"] as const).map((choice) => (
            <Chip
              key={choice}
              label={typeLabels[choice]}
              aria-label={`New event type ${typeLabels[choice]}`}
              aria-pressed={type === choice}
              color={type === choice ? "primary" : "default"}
              variant={type === choice ? "filled" : "outlined"}
              onClick={() => setType(choice)}
            />
          ))}
        </Stack>
        <SpanInput
          label="Reminder before"
          minutes={lead}
          placeholder={String(defaultLead)}
          onChange={setLead}
          error={Boolean(errors.leadMinutes)}
          helperText={help(
            "leadMinutes",
            `0 to ${newEventBounds.lead.max / 60} hours. Empty uses the default lead, ${defaultLead} minutes.`,
          )}
        />
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          The event goes on the Google calendar the alerts read, with one
          reminder at that lead. Ring until stopped also writes #critical in its
          description.
        </Typography>
        {problem && <Alert severity="warning">{problem}</Alert>}
        {done && <Alert severity="info">{done}</Alert>}
        <Box>
          <Button type="submit" variant="contained" disabled={busy}>
            Add event
          </Button>
        </Box>
      </Stack>
    </Box>
  );
}
