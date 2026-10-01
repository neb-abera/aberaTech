import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import type { AlertsState, StateChangeResult } from "../../alerts/core/api";
import {
  type Countdown,
  type CountdownFields,
  countdownBounds,
  createCountdown,
  deleteCountdown,
  updateCountdown,
} from "../../alerts/core/countdowns";
import {
  browserZone,
  canonicalZone,
  describeRemaining,
  formatTarget,
  isoToZoned,
  zonedToIso,
} from "../core/countdown";

/** What the section calls. The page passes the real ones, a test its own. */
export interface CountdownsApi {
  create: (fields: CountdownFields) => Promise<StateChangeResult>;
  update: (id: string, fields: CountdownFields) => Promise<StateChangeResult>;
  remove: (id: string) => Promise<StateChangeResult>;
}

export const countdownsApi: CountdownsApi = {
  create: createCountdown,
  update: updateCountdown,
  remove: deleteCountdown,
};

interface Draft {
  label: string;
  date: string;
  time: string;
  timeZone: string;
}

const blank = (): Draft => ({
  label: "",
  date: "",
  time: "00:00",
  timeZone: browserZone(),
});

function draftOf(countdown: Countdown): Draft {
  return {
    label: countdown.label,
    ...isoToZoned(countdown.targetAt, countdown.timeZone),
    timeZone: countdown.timeZone,
  };
}

function explain(result: Exclude<StateChangeResult, { ok: true }>): string {
  switch (result.reason) {
    case "full":
      return (
        result.detail ??
        `At most ${countdownBounds.max} countdowns. Delete one first.`
      );
    case "missing":
      return "That countdown is gone. The list below is current.";
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

/** The time now, once a second while the section is shown. */
function useNow(initial?: number): number {
  const [now, setNow] = React.useState(() => initial ?? Date.now());
  React.useEffect(() => {
    if (initial !== undefined) return;
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, [initial]);
  return now;
}

/** Every zone this browser knows, for the zone field's suggestions. */
const zones: readonly string[] =
  (
    Intl as { supportedValuesOf?: (key: "timeZone") => string[] }
  ).supportedValuesOf?.("timeZone") ?? [];

/**
 * The owner's countdowns: each with its clock, its target in its own zone,
 * Edit and Delete that asks first. The form adds one, or changes the one
 * being edited. The paired phone shows the same list.
 */
export default function CountdownsSection({
  countdowns,
  onState,
  api = countdownsApi,
  now: fixedNow,
}: {
  countdowns: Countdown[];
  onState: (state: AlertsState) => void;
  api?: CountdownsApi;
  /** A fixed time for tests. Left out, the clocks tick. */
  now?: number;
}) {
  const now = useNow(fixedNow);
  const [draft, setDraft] = React.useState<Draft>(blank);
  const [editing, setEditing] = React.useState<Countdown | null>(null);
  const [confirming, setConfirming] = React.useState<string | null>(null);
  const [busy, setBusy] = React.useState(false);
  const [problem, setProblem] = React.useState<string | null>(null);
  const [errors, setErrors] = React.useState<Record<string, string[]>>({});
  const [done, setDone] = React.useState<string | null>(null);

  const run = async (
    press: () => Promise<StateChangeResult>,
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
    const timeZone = canonicalZone(draft.timeZone.trim());
    if (!timeZone) {
      setErrors({ timeZone: ["A time zone such as Asia/Amman."] });
      return;
    }
    const targetAt = zonedToIso(draft.date, draft.time, timeZone);
    if (!targetAt) {
      setErrors({ targetAt: ["A date and a time."] });
      return;
    }
    setErrors({});
    const fields: CountdownFields = {
      label: draft.label.trim(),
      targetAt,
      timeZone,
    };
    const name = fields.label || countdownBounds.defaultLabel;
    const saved = editing
      ? await run(() => api.update(editing.id, fields), `${name} is saved.`)
      : await run(() => api.create(fields), `${name} is added.`);
    if (saved) {
      setEditing(null);
      setDraft(blank());
    }
  };

  const help = (field: string, text: string) => errors[field]?.[0] ?? text;

  return (
    <Box component="section" aria-labelledby="countdowns-heading">
      <Typography
        id="countdowns-heading"
        variant="h2"
        sx={{ fontSize: "1.25rem", mb: 1 }}
      >
        Countdowns
      </Typography>
      <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
        Kept on abera.tech. The paired phone shows the same countdowns. Once a
        date passes, its clock counts the time since.
      </Typography>

      {countdowns.length === 0 ? (
        <Typography variant="body1" sx={{ color: "text.secondary", mb: 2 }}>
          No countdowns.
        </Typography>
      ) : (
        <Stack
          component="ul"
          aria-label="Countdowns"
          spacing={1}
          sx={{ listStyle: "none", p: 0, m: 0, mb: 2 }}
        >
          {countdowns.map((countdown) => {
            const clock = describeRemaining(
              Date.parse(countdown.targetAt),
              now,
            );
            return (
              <Box
                component="li"
                key={countdown.id}
                aria-label={countdown.label}
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
                    {countdown.label}
                  </Typography>
                  <Typography
                    variant="body1"
                    data-testid="countdown-clock"
                    sx={{ fontVariantNumeric: "tabular-nums" }}
                  >
                    {clock}
                  </Typography>
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    {formatTarget(countdown.targetAt, countdown.timeZone)} (
                    {countdown.timeZone})
                  </Typography>
                </Box>
                <Button
                  size="small"
                  variant="outlined"
                  disabled={busy}
                  onClick={() => {
                    setEditing(countdown);
                    setDraft(draftOf(countdown));
                    setErrors({});
                    setDone(null);
                  }}
                  aria-label={`Edit ${countdown.label}`}
                >
                  Edit
                </Button>
                {confirming === countdown.id ? (
                  <>
                    <Button
                      size="small"
                      color="error"
                      variant="contained"
                      disabled={busy}
                      onClick={() => {
                        setConfirming(null);
                        if (editing?.id === countdown.id) {
                          setEditing(null);
                          setDraft(blank());
                        }
                        void run(
                          () => api.remove(countdown.id),
                          `${countdown.label} is deleted.`,
                        );
                      }}
                      aria-label={`Yes, delete ${countdown.label}`}
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
                    onClick={() => setConfirming(countdown.id)}
                    aria-label={`Delete ${countdown.label}`}
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
        aria-label={editing ? "Edit countdown" : "Add countdown"}
        noValidate
      >
        <Typography variant="body1" sx={{ fontWeight: 600 }}>
          {editing ? `Edit ${editing.label}` : "Add countdown"}
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
            `At most ${countdownBounds.label} characters. Empty is saved as ${countdownBounds.defaultLabel}.`,
          )}
          slotProps={{ htmlInput: { maxLength: countdownBounds.label } }}
        />
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          <TextField
            label="Date"
            type="date"
            value={draft.date}
            required
            onChange={(event) =>
              setDraft((current) => ({ ...current, date: event.target.value }))
            }
            error={Boolean(errors.targetAt)}
            helperText={help("targetAt", "The day it counts to.")}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <TextField
            label="Time"
            type="time"
            value={draft.time}
            required
            onChange={(event) =>
              setDraft((current) => ({ ...current, time: event.target.value }))
            }
            helperText="24 hours. 00:00 is the start of the day."
            slotProps={{ inputLabel: { shrink: true } }}
          />
        </Stack>
        <TextField
          label="Time zone"
          value={draft.timeZone}
          onChange={(event) =>
            setDraft((current) => ({
              ...current,
              timeZone: event.target.value,
            }))
          }
          error={Boolean(errors.timeZone)}
          helperText={help(
            "timeZone",
            "Where the date and time are, such as Asia/Amman or America/New_York.",
          )}
          slotProps={{
            htmlInput: { list: "countdown-zones", spellCheck: false },
          }}
        />
        <datalist id="countdown-zones">
          {zones.map((zone) => (
            <option key={zone} value={zone} />
          ))}
        </datalist>
        <Stack direction="row" spacing={1}>
          <Button type="submit" variant="contained" disabled={busy}>
            {editing ? "Save countdown" : "Add countdown"}
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
