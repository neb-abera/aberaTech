import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import * as React from "react";
import SignInToSee from "../../progress/components/SignInToSee";
import {
  type DevBoxStatus,
  fetchDevBoxStatus,
  isInTransit,
  isParked,
  isRunning,
  startDevBox,
} from "../core/api";
import Runbook from "./Runbook";

/** How often to ask Azure again while the box is between states. */
const pollEvery = 5000;

type View = { status: "loading" } | DevBoxStatus;

/**
 * The owner's dev box: what state it is in, a button that starts it, and
 * the runbook for getting a session back when this page is not enough.
 *
 * The box parks itself after 30 idle minutes and at 03:00 UTC, and nothing
 * wakes it. On 2026-09-21 every phone session died with it and there was no
 * way back that did not need a laptop. This page is the way back: sign in,
 * press Start, wait for "running", open Remote Control.
 */
export default function DevBoxPanel() {
  const [view, setView] = React.useState<View>({ status: "loading" });
  const [starting, setStarting] = React.useState(false);
  const [problem, setProblem] = React.useState<string | null>(null);
  const alive = React.useRef(true);

  const refresh = React.useCallback(async () => {
    const next = await fetchDevBoxStatus();
    if (alive.current) setView(next);
    return next;
  }, []);

  React.useEffect(() => {
    alive.current = true;
    void refresh();
    return () => {
      alive.current = false;
    };
  }, [refresh]);

  // Keep asking while Azure is between states, and after a Start until the
  // answer is "running". A timeout chain rather than an interval, so a slow
  // answer never overlaps the next question.
  const power = view.status === "owner" ? view.power : null;
  const waiting = starting || (power !== null && isInTransit(power));
  React.useEffect(() => {
    if (!waiting) return;
    let cancelled = false;
    let timer = 0;
    const tick = async () => {
      const next = await refresh();
      if (cancelled || next.status !== "owner") return;
      if (isRunning(next.power)) {
        setStarting(false);
        return;
      }
      if (starting || isInTransit(next.power)) {
        timer = window.setTimeout(tick, pollEvery);
      }
    };
    timer = window.setTimeout(tick, pollEvery);
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [waiting, starting, refresh]);

  const start = async () => {
    setProblem(null);
    setStarting(true);
    const result = await startDevBox();
    if (!alive.current) return;
    if (result.ok) {
      setView({ status: "owner", power: "starting" });
      return;
    }
    setStarting(false);
    setProblem(
      result.reason === "throttled"
        ? "Too many presses. Wait a minute."
        : result.reason === "visitor"
          ? "The session expired. Reload and sign in again."
          : result.reason === "azure"
            ? "Azure refused the start. The fallbacks below still work."
            : "No answer from the server. Check the connection and try again.",
    );
  };

  if (view.status === "loading") {
    return <CircularProgress size={28} aria-label="Loading" />;
  }

  if (view.status === "visitor") {
    return (
      <SignInToSee
        message="This page is the owner's. Sign in to see it."
        returnUrl="/devbox"
      />
    );
  }

  if (view.status === "unconfigured") {
    return (
      <Alert severity="info">
        This deployment has no dev box configured. Set DevBox__SubscriptionId on
        the container app.
      </Alert>
    );
  }

  if (view.status === "error") {
    return (
      <Stack spacing={3}>
        <Alert severity="error">
          Azure did not answer. The fallbacks below still work.
        </Alert>
        <Runbook />
      </Stack>
    );
  }

  const running = isRunning(view.power);
  const parked = isParked(view.power);
  const state = running
    ? { label: "Running", color: "success" as const }
    : starting || isInTransit(view.power)
      ? { label: `${capitalise(view.power)}…`, color: "warning" as const }
      : { label: capitalise(view.power), color: "default" as const };

  return (
    <Stack spacing={3}>
      <Stack
        direction="row"
        spacing={2}
        sx={{ alignItems: "center", flexWrap: "wrap" }}
      >
        <Chip
          label={state.label}
          color={state.color}
          variant={running ? "filled" : "outlined"}
          aria-label={`Power state: ${view.power}`}
        />
        <Button
          variant="contained"
          size="large"
          onClick={start}
          disabled={!parked || starting}
        >
          Start dev box
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

      {running ? (
        <Alert severity="success">
          Open the Claude app, Code, Remote Control, and pick <b>devbox</b>. It
          registers about a minute after the box comes up. If it is not in the
          list yet, look again in a minute. Pick it by name, the link changes
          every boot.
        </Alert>
      ) : starting || isInTransit(view.power) ? (
        <Alert severity="info">
          Azure is starting the box. This page asks again every five seconds.
          About two minutes from Start to a session.
        </Alert>
      ) : (
        <Typography variant="body1" sx={{ color: "text.secondary" }}>
          The box is parked, which is where the idle watchdog and the 03:00 UTC
          shutdown leave it. It bills nothing but disks until it starts.
        </Typography>
      )}

      <Runbook />
    </Stack>
  );
}

function capitalise(word: string): string {
  return word.length === 0 ? word : word[0].toUpperCase() + word.slice(1);
}
