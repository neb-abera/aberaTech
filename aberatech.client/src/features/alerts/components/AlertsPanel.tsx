import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import * as React from "react";
import SignInToSee from "../../progress/components/SignInToSee";
import {
  type ActionResult,
  type AlertItem,
  type AlertsState,
  type AlertsView,
  fetchAlerts,
  formatWhen,
  muteAlerts,
  sendTestAlert,
  skipAlert,
  unmuteAlerts,
  unskipAlert,
} from "../core/api";

/** How often the page asks again on its own. */
const refreshEvery = 60_000;

type View = { status: "loading" } | AlertsView;

/**
 * The owner's calendar alerts: whether they are on, the next ones, and the
 * buttons. Plain requests and a timer, so it works from a work computer
 * that blocks everything else.
 */
export default function AlertsPanel() {
  const [view, setView] = React.useState<View>({ status: "loading" });
  const [busy, setBusy] = React.useState(false);
  const [problem, setProblem] = React.useState<string | null>(null);
  const [notice, setNotice] = React.useState<string | null>(null);
  const alive = React.useRef(true);

  const refresh = React.useCallback(async () => {
    const next = await fetchAlerts();
    if (alive.current) setView(next);
  }, []);

  React.useEffect(() => {
    alive.current = true;
    let timer = 0;
    const tick = async () => {
      await refresh();
      if (alive.current) timer = window.setTimeout(tick, refreshEvery);
    };
    void tick();
    return () => {
      alive.current = false;
      window.clearTimeout(timer);
    };
  }, [refresh]);

  const act = async (
    press: () => Promise<ActionResult>,
    done: () => string,
  ) => {
    setBusy(true);
    setProblem(null);
    setNotice(null);
    const result = await press();
    if (!alive.current) return;
    setBusy(false);
    if (result.ok) {
      const state = result.state;
      if (state) setView({ status: "owner", state });
      setNotice(done());
      return;
    }
    setProblem(explain(result));
  };

  if (view.status === "loading") {
    return <CircularProgress size={28} aria-label="Loading" />;
  }

  if (view.status === "visitor") {
    return (
      <SignInToSee
        message="This page is the owner's. Sign in to see it."
        returnUrl="/alerts"
      />
    );
  }

  if (view.status === "unconfigured") {
    return <Unconfigured missing={view.missing} />;
  }

  if (view.status === "error") {
    return (
      <Stack spacing={2} sx={{ alignItems: "flex-start" }}>
        <Alert severity="error">The server did not answer.</Alert>
        <Button variant="outlined" onClick={() => void refresh()}>
          Refresh
        </Button>
      </Stack>
    );
  }

  const { state } = view;
  const when = (iso: string) => formatWhen(iso, state.timeZone);
  const muted = state.mutedUntil !== null;

  return (
    <Stack spacing={3}>
      <Stack
        direction="row"
        spacing={1}
        useFlexGap
        sx={{ alignItems: "center", flexWrap: "wrap" }}
      >
        <Chip
          label={
            muted && state.mutedUntil
              ? `Muted until ${when(state.mutedUntil)}`
              : "Active"
          }
          color={muted ? "warning" : "success"}
          aria-label={`Alert state: ${muted ? "muted" : "active"}`}
        />
        <Button
          variant="outlined"
          disabled={busy}
          onClick={() =>
            void act(
              () => muteAlerts("hour"),
              () => "Muted. Alerts due before then are not sent.",
            )
          }
        >
          Mute 1 hour
        </Button>
        <Button
          variant="outlined"
          disabled={busy}
          onClick={() =>
            void act(
              () => muteAlerts("morning"),
              () => "Muted. Alerts due before then are not sent.",
            )
          }
        >
          Mute until tomorrow 06:00
        </Button>
        <Button
          variant="outlined"
          disabled={busy || !muted}
          onClick={() => void act(unmuteAlerts, () => "Alerts are on again.")}
        >
          Unmute
        </Button>
        <Button
          variant="contained"
          disabled={busy}
          onClick={() =>
            void act(
              sendTestAlert,
              () => "Test alert sent. Check the phone for one sound.",
            )
          }
        >
          Send test alert
        </Button>
        <Button variant="text" size="small" onClick={() => void refresh()}>
          Refresh
        </Button>
      </Stack>

      {problem && (
        <Alert severity="warning" onClose={() => setProblem(null)}>
          {problem}
        </Alert>
      )}
      {notice && (
        <Alert severity="info" onClose={() => setNotice(null)}>
          {notice}
        </Alert>
      )}

      <Calendar state={state} when={when} />

      <Box>
        <Typography variant="h2" sx={{ fontSize: "1.25rem", mb: 1 }}>
          Next alerts
        </Typography>
        {state.alerts.length === 0 ? (
          <Typography variant="body1" sx={{ color: "text.secondary" }}>
            No alerts coming up.
          </Typography>
        ) : (
          <Stack
            component="ul"
            aria-label="Next alerts"
            spacing={1.5}
            sx={{ listStyle: "none", p: 0, m: 0 }}
          >
            {state.alerts.map((alert) => (
              <Item
                key={alert.key}
                alert={alert}
                when={when}
                defaultLead={state.defaultLeadMinutes}
                busy={busy}
                onSkip={() =>
                  void act(
                    () => skipAlert(alert.key),
                    () => `${alert.title} will not alert.`,
                  )
                }
                onUndo={() =>
                  void act(
                    () => unskipAlert(alert.key),
                    () => `${alert.title} will alert again.`,
                  )
                }
              />
            ))}
          </Stack>
        )}
      </Box>

      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        One Pushover message per event, priority 1: one sound, no repeats. Mute
        and Skip are checked just before each send. Times are in{" "}
        {state.timeZone}, the calendar's own zone.
      </Typography>
    </Stack>
  );
}

function explain(result: Exclude<ActionResult, { ok: true }>): string {
  switch (result.reason) {
    case "throttled":
      return "Too many presses. Wait a minute.";
    case "visitor":
      return "The session expired. Reload and sign in again.";
    case "pushover":
      return `Pushover refused the test: ${result.detail ?? "no reason given"}.`;
    case "refused":
      return "The server refused. Refresh and try again.";
    default:
      return "No answer from the server. Check the connection and try again.";
  }
}

function Calendar({
  state,
  when,
}: {
  state: AlertsState;
  when: (iso: string) => string;
}) {
  return (
    <Stack spacing={1}>
      <Typography variant="body1">
        {state.lastFetchAt === null
          ? "Calendar not read yet. The first read is within a minute of the server starting."
          : `Calendar read ${when(state.lastSuccessAt ?? state.lastFetchAt)}, every ${state.pollMinutes} minutes.`}
      </Typography>
      {state.lastFetchError && state.lastFetchAt && (
        <Alert severity="warning">
          The last read, {when(state.lastFetchAt)}, failed:{" "}
          {state.lastFetchError}. The list is from{" "}
          {state.lastSuccessAt
            ? `${when(state.lastSuccessAt)}.`
            : "no earlier read, so it is empty."}
        </Alert>
      )}
      {state.lastSend && (
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Last send: {state.lastSend.title}, {when(state.lastSend.at)},{" "}
          {state.lastSend.outcome}.
        </Typography>
      )}
    </Stack>
  );
}

function Item({
  alert,
  when,
  defaultLead,
  busy,
  onSkip,
  onUndo,
}: {
  alert: AlertItem;
  when: (iso: string) => string;
  defaultLead: number;
  busy: boolean;
  onSkip: () => void;
  onUndo: () => void;
}) {
  const at = `${alert.title} at ${when(alert.startsAt)}`;
  return (
    <Box
      component="li"
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
      <Box sx={{ flex: "1 1 16rem", minWidth: 0 }}>
        <Typography variant="body1" sx={{ fontWeight: 600 }}>
          {alert.title}
        </Typography>
        <Typography variant="body2">
          Alert {when(alert.alertAt)}, starts {when(alert.startsAt)}.
        </Typography>
        {alert.location && (
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {alert.location}
          </Typography>
        )}
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {alert.source === "reminder"
            ? "Time from the event's reminder."
            : `${defaultLead} minutes before, the default.`}
        </Typography>
      </Box>
      {alert.skipped && <Chip size="small" label="Skipped" />}
      {alert.muted && !alert.skipped && (
        <Chip size="small" color="warning" label="Muted" />
      )}
      {alert.skipped ? (
        <Button
          size="small"
          disabled={busy}
          onClick={onUndo}
          aria-label={`Undo skip of ${at}`}
        >
          Undo skip
        </Button>
      ) : (
        <Button
          size="small"
          variant="outlined"
          disabled={busy}
          onClick={onSkip}
          aria-label={`Skip ${at}`}
        >
          Skip
        </Button>
      )}
    </Box>
  );
}

function Unconfigured({ missing }: { missing: string[] }) {
  if (missing.length === 0) {
    return (
      <Alert severity="info">
        Alerts are off on this deployment: it has no owner sign-in.
      </Alert>
    );
  }

  return (
    <Stack spacing={2}>
      <Alert severity="info">
        Alerts are off. This deployment is missing these settings:
        <Box
          component="ul"
          aria-label="Missing settings"
          sx={{ mt: 1, mb: 0, pl: 3 }}
        >
          {missing.map((name) => (
            <li key={name}>
              <code>{name}</code>
            </li>
          ))}
        </Box>
      </Alert>
      <Typography variant="body1">To switch them on:</Typography>
      <Box
        component="ol"
        aria-label="Steps to switch on"
        sx={{ m: 0, pl: 3, "& li": { mb: 1 } }}
      >
        <li>
          In Google Calendar, open Settings, pick the calendar, then Integrate
          calendar. Copy the secret address in iCal format.
        </li>
        <li>
          In Pushover, copy the user key from the dashboard. Create an
          application and copy its API token.
        </li>
        <li>
          Set each missing name on the container app as a secret reference.
          README.md, under Calendar alerts, has the commands.
        </li>
      </Box>
    </Stack>
  );
}
