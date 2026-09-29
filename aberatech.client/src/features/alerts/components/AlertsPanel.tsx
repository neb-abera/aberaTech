import Alert from "@mui/material/Alert";
import AlertTitle from "@mui/material/AlertTitle";
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
  type AlertType,
  fetchAlerts,
  formatClock,
  formatWhen,
  muteAlerts,
  sendEventTest,
  sendTestAlert,
  sendTestNotification,
  setEventType,
  skipAlert,
  unmuteAlerts,
  unskipAlert,
} from "../core/api";
import { ringPollMs } from "../core/ring";
import { alarmLine, count, describe, typeLabels } from "../core/settings";
import AlertSettingsForm from "./AlertSettingsForm";
import HowEventsAlert from "./HowEventsAlert";
import NewEventForm from "./NewEventForm";
import PhonesSection, { type PhonesApi, phonesApi } from "./PhonesSection";
import RingInBrowser from "./RingInBrowser";

/** How often the page asks again on its own. Every 15 s while it rings in this browser. */
const refreshEvery = 60_000;

type View = { status: "loading" } | AlertsView;

/**
 * The owner's calendar alerts: whether they are on, the next ones, and the
 * buttons. Plain requests and a timer, so it works from a work computer
 * that blocks everything else.
 */
export default function AlertsPanel({
  phones = phonesApi,
}: {
  phones?: PhonesApi;
} = {}) {
  const [view, setView] = React.useState<View>({ status: "loading" });
  const [ringing, setRinging] = React.useState(false);
  const [busy, setBusy] = React.useState(false);
  const [problem, setProblem] = React.useState<string | null>(null);
  const [notice, setNotice] = React.useState<string | null>(null);
  const [calendarWrite, setCalendarWrite] = React.useState<string | null>(null);
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
      if (alive.current)
        timer = window.setTimeout(tick, ringing ? ringPollMs : refreshEvery);
    };
    void tick();
    return () => {
      alive.current = false;
      window.clearTimeout(timer);
    };
  }, [refresh, ringing]);

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
      if (state) {
        setView({ status: "owner", state });
        setCalendarWrite(state.calendarWrite ?? null);
      }
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
      <ReadFailed state={state} when={when} />
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
              () =>
                `Test alert sent as an alarm. It ${alarmLine(state.settings, state.bounds.maxEmergencySounds)}`,
            )
          }
        >
          Send test alert
        </Button>
        <Button
          variant="outlined"
          disabled={busy}
          onClick={() =>
            void act(
              sendTestNotification,
              () => "Test notification sent. Check the phone for one sound.",
            )
          }
        >
          Send test notification
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
      {calendarWrite && (
        <Alert severity="warning" onClose={() => setCalendarWrite(null)}>
          <AlertTitle>Google Calendar was not changed</AlertTitle>
          {calendarWrite} The choice is saved here and alerts follow it.
        </Alert>
      )}

      <RingInBrowser
        state={state}
        onState={(next) => {
          if (alive.current) setView({ status: "owner", state: next });
        }}
        onEnabledChange={setRinging}
      />

      <Calendar state={state} when={when} />

      <Box>
        <Typography variant="h2" sx={{ fontSize: "1.25rem", mb: 1 }}>
          Next alerts
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          Each type holds for every occurrence of the event. Alarm also adds
          #critical to the event in Google Calendar. None and Notification
          remove it.
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
                clock={(iso) => formatClock(iso, state.timeZone)}
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
                onTest={() =>
                  void act(
                    () => sendEventTest(alert.key),
                    () =>
                      `Test of ${alert.title} sent, titled "Test: ${alert.title}", as ${alert.type === "alarm" ? "an alarm" : "a notification"}.`,
                  )
                }
                onType={(type) =>
                  void act(
                    () => setEventType(alert.key, type),
                    () =>
                      type === "default"
                        ? `${alert.title} follows the calendar and the default again.`
                        : `${alert.title} is set to ${typeLabels[type]}, every occurrence.`,
                  )
                }
              />
            ))}
          </Stack>
        )}
      </Box>

      <NewEventForm
        defaultLead={state.defaultLeadMinutes}
        onCreated={(created) => {
          if (alive.current) setView({ status: "owner", state: created });
        }}
      />

      <HowEventsAlert
        settings={state.settings}
        maxSounds={state.bounds.maxEmergencySounds}
      />

      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        {describe(
          state.settings,
          state.bounds.maxEmergencySounds,
          state.timeZone,
        )}
      </Typography>

      <PhonesSection api={phones} when={when} />

      <AlertSettingsForm
        settings={state.settings}
        bounds={state.bounds}
        onSaved={(saved) => {
          if (alive.current) setView({ status: "owner", state: saved });
        }}
      />
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

/**
 * A calendar that cannot be read, first on the page. Every alert listed
 * comes from the last read that worked, and the owner has to know that.
 */
function ReadFailed({
  state,
  when,
}: {
  state: AlertsState;
  when: (iso: string) => string;
}) {
  if (!state.lastFetchError || !state.lastFetchAt) return null;
  return (
    <Alert severity="error">
      <AlertTitle>The calendar cannot be read</AlertTitle>
      The last read, {when(state.lastFetchAt)}, failed:{" "}
      {state.lastFetchError.replace(/\.$/, "")}.
      {state.lastFetchError === "HTTP 404"
        ? " Google answers 404 when the secret address is wrong or was reset."
        : ""}{" "}
      {state.lastSuccessAt
        ? `The last good read was ${when(state.lastSuccessAt)}. The alerts below are from that read.`
        : "No read has worked since the server started, so no alerts are planned."}
    </Alert>
  );
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
        {state.lastSuccessAt !== null
          ? `Calendar read ${when(state.lastSuccessAt)}, every ${state.pollMinutes} minutes.`
          : state.lastFetchAt === null
            ? "Calendar not read yet. The first read is within a minute of the server starting."
            : `No read of the calendar has worked yet. It is tried every ${state.pollMinutes} minutes.`}
      </Typography>
      {state.lastSend && (
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Last send: {state.lastSend.title}, {when(state.lastSend.at)},{" "}
          {state.lastSend.outcome}.
        </Typography>
      )}
    </Stack>
  );
}

/** Where an alert's type came from, and which settings it sends with. */
function typeLine(alert: AlertItem): string {
  const from =
    alert.typeFrom === "set"
      ? "set here"
      : alert.typeFrom === "critical"
        ? "from #critical in the calendar"
        : "the default for unmarked events";
  if (alert.type === "none")
    return `Sends nothing: ${from}. Send test is off until you choose Notification or Alarm.`;
  return `${typeLabels[alert.type]}: ${from}. The ${alert.type} settings apply.`;
}

function Item({
  alert,
  when,
  clock,
  defaultLead,
  busy,
  onSkip,
  onUndo,
  onTest,
  onType,
}: {
  alert: AlertItem;
  when: (iso: string) => string;
  clock: (iso: string) => string;
  defaultLead: number;
  busy: boolean;
  onSkip: () => void;
  onUndo: () => void;
  onTest: () => void;
  onType: (type: AlertType | "default") => void;
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
            ? "Time from the event's notification."
            : `No notification in the feed: ${count(defaultLead, "minute")} before, the default lead.`}
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {typeLine(alert)}
        </Typography>
        {alert.acknowledged && alert.acknowledgedAt && (
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            Acknowledged{" "}
            {alert.acknowledgedVia === "phone" ? "on phone" : "in a browser"} at{" "}
            {clock(alert.acknowledgedAt)}
          </Typography>
        )}
        <Stack
          direction="row"
          spacing={1}
          useFlexGap
          role="group"
          aria-label={`Type of ${at}`}
          sx={{ mt: 1, alignItems: "center", flexWrap: "wrap" }}
        >
          {(["none", "notification", "alarm"] as const).map((type) => (
            <Chip
              key={type}
              size="small"
              label={typeLabels[type]}
              aria-label={`Set ${at} to ${typeLabels[type]}`}
              aria-pressed={alert.type === type}
              color={alert.type === type ? "primary" : "default"}
              variant={alert.type === type ? "filled" : "outlined"}
              disabled={busy}
              onClick={() => onType(type)}
            />
          ))}
          {alert.typeFrom === "set" && (
            <Button
              size="small"
              disabled={busy}
              onClick={() => onType("default")}
              aria-label={`Use the default type for ${at}`}
            >
              Use default
            </Button>
          )}
        </Stack>
      </Box>
      {alert.critical && <Chip size="small" color="error" label="Critical" />}
      {alert.skipped && <Chip size="small" label="Skipped" />}
      {alert.muted && !alert.skipped && (
        <Chip size="small" color="warning" label="Muted" />
      )}
      <Button
        size="small"
        variant="outlined"
        disabled={busy || alert.type === "none"}
        onClick={onTest}
        aria-label={`Send test of ${at}`}
      >
        Send test
      </Button>
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
