import Alert from "@mui/material/Alert";
import AlertTitle from "@mui/material/AlertTitle";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import FormControlLabel from "@mui/material/FormControlLabel";
import Switch from "@mui/material/Switch";
import Typography from "@mui/material/Typography";
import * as React from "react";
import {
  type ActionResult,
  type AlertItem,
  type AlertsState,
  acknowledgeAlert,
  formatWhen,
} from "../core/api";
import {
  askToNotify,
  dueAlarm,
  notify,
  readRingPreference,
  type Tone,
  webAudioTone,
  writeRingPreference,
} from "../core/ring";

/**
 * "Ring in this browser": while the switch is on and the tab is open, an
 * alarm that comes due rings here with a tone, a flashing title and a
 * notification, until it is acknowledged anywhere, skipped, muted, or its
 * event starts. For a work computer where the phone is out of reach and
 * nothing can be installed.
 */
export default function RingInBrowser({
  state,
  onState,
  onEnabledChange,
  now = Date.now,
  makeTone = webAudioTone,
  acknowledge = acknowledgeAlert,
}: {
  state: AlertsState;
  /** The state the server answered the acknowledgement with. */
  onState: (state: AlertsState) => void;
  /** The page asks the server every 15 s while this is on. */
  onEnabledChange: (on: boolean) => void;
  now?: () => number;
  makeTone?: () => Tone;
  acknowledge?: (key: string, via: "browser") => Promise<ActionResult>;
}) {
  const [enabled, setEnabled] = React.useState(readRingPreference);
  const [clock, setClock] = React.useState(now);
  const [busy, setBusy] = React.useState(false);
  const [problem, setProblem] = React.useState<string | null>(null);
  const tone = React.useRef<Tone | null>(null);
  const toneFor = () => {
    if (!tone.current) tone.current = makeTone();
    return tone.current;
  };

  React.useEffect(() => {
    onEnabledChange(enabled);
  }, [enabled, onEnabledChange]);

  // A second clock, so an alarm rings the second it comes due rather than
  // at the next answer from the server.
  React.useEffect(() => {
    if (!enabled) return;
    const timer = window.setInterval(() => setClock(now()), 1000);
    return () => window.clearInterval(timer);
  }, [enabled, now]);

  const due = enabled ? dueAlarm(state.alerts, clock) : null;
  const dueKey = due?.key ?? null;
  const dueTitle = due?.title ?? "";

  // The tone and the title, for as long as the same alarm is due.
  // biome-ignore lint/correctness/useExhaustiveDependencies: keyed on the alarm alone, and toneFor reads a ref.
  React.useEffect(() => {
    if (dueKey === null) return;
    const ringing = toneFor();
    ringing.start();
    const original = document.title;
    let flash = true;
    const timer = window.setInterval(() => {
      flash = !flash;
      document.title = flash ? `Ringing: ${dueTitle}` : original;
    }, 1000);
    document.title = `Ringing: ${dueTitle}`;
    return () => {
      ringing.stop();
      window.clearInterval(timer);
      document.title = original;
    };
  }, [dueKey, dueTitle]);

  // One notification per alarm.
  const notified = React.useRef<string | null>(null);
  React.useEffect(() => {
    if (!due || notified.current === due.key) return;
    notified.current = due.key;
    notify(
      `Ringing: ${due.title}`,
      `Starts ${formatWhen(due.startsAt, state.timeZone)}. Acknowledge it on abera.tech/alerts.`,
      due.key,
    );
  }, [due, state.timeZone]);

  const toggle = (on: boolean) => {
    if (on) {
      // The click is the gesture that lets the page play sound.
      toneFor().unlock();
      askToNotify();
    }
    writeRingPreference(on);
    setEnabled(on);
    setClock(now());
  };

  const answer = async (alert: AlertItem) => {
    setBusy(true);
    setProblem(null);
    const result = await acknowledge(alert.key, "browser");
    setBusy(false);
    if (result.ok) {
      if (result.state) onState(result.state);
      return;
    }
    setProblem(
      result.reason === "visitor"
        ? "The session expired. Reload and sign in again."
        : result.reason === "throttled"
          ? "Too many presses. Wait a minute."
          : "The server did not take it. Try again.",
    );
  };

  return (
    <Box component="section" aria-labelledby="ring-heading">
      <Typography
        id="ring-heading"
        variant="h2"
        sx={{ fontSize: "1.25rem", mb: 1 }}
      >
        Ring in this browser
      </Typography>
      <FormControlLabel
        control={
          <Switch
            checked={enabled}
            onChange={(event) => toggle(event.target.checked)}
          />
        }
        label="Ring in this browser"
      />
      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        Rings only while this tab is open. An event set to Ring until stopped
        plays a tone when it comes due, flashes the tab title and shows a
        notification if you allow one, until it is acknowledged here or on a
        phone, skipped, muted, or its event starts. The page checks the server
        every 15 seconds while this is on.
      </Typography>
      {due && (
        <Alert
          severity="error"
          role="alertdialog"
          aria-label={`Ringing: ${due.title}`}
          sx={{ mt: 2, alignItems: "center" }}
          action={
            <Button
              variant="contained"
              color="inherit"
              size="large"
              disabled={busy}
              onClick={() => void answer(due)}
            >
              Acknowledge
            </Button>
          }
        >
          <AlertTitle>Ringing: {due.title}</AlertTitle>
          Starts {formatWhen(due.startsAt, state.timeZone)}.
        </Alert>
      )}
      {problem && (
        <Alert
          severity="warning"
          sx={{ mt: 1 }}
          onClose={() => setProblem(null)}
        >
          {problem}
        </Alert>
      )}
    </Box>
  );
}
