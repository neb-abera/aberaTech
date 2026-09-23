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
  holdDevBox,
  isInTransit,
  isParked,
  isRunning,
  parkDevBox,
  startDevBox,
} from "../core/api";
import AgentPanel from "./AgentPanel";
import BrowserPanel from "./BrowserPanel";
import Runbook from "./Runbook";

/** How often to ask again while the box is between states. */
const pollEvery = 5000;

/** How often to refresh the agent's report while the box runs. */
const refreshEvery = 30000;

type View = { status: "loading" } | DevBoxStatus;

/**
 * The owner's dev box: what state it is in, the buttons, what its agent
 * reports, and the runbook for when this page is not enough.
 *
 * The box parks itself after 30 idle minutes, and at 03:00 UTC when nobody
 * is attached, and nothing wakes it. On 2026-09-21 every phone session died
 * with it and there was no way back that did not need a laptop. This page
 * is the way back: sign in, press Start, wait for "running", open the link
 * the agent reports.
 */
export default function DevBoxPanel() {
  const [view, setView] = React.useState<View>({ status: "loading" });
  const [starting, setStarting] = React.useState(false);
  const [problem, setProblem] = React.useState<string | null>(null);
  const [notice, setNotice] = React.useState<string | null>(null);
  // True after a status check got no answer while an earlier one had. The
  // page keeps the earlier answer and says so, instead of dropping to the
  // error view. On 2026-09-22 one missed poll during a start did exactly
  // that: the box was running, the page said "Azure did not answer" and
  // stopped asking.
  const [stale, setStale] = React.useState(false);
  const alive = React.useRef(true);

  const refresh = React.useCallback(async () => {
    const next = await fetchDevBoxStatus();
    if (!alive.current) return next;
    if (next.status === "error") {
      setView((current) => (current.status === "owner" ? current : next));
      setStale(true);
    } else {
      setView(next);
      setStale(false);
    }
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
  // answer never overlaps the next question. A running box is asked every
  // half minute, so the agent's report stays current.
  const power = view.status === "owner" ? view.power : null;
  const waiting = starting || (power !== null && isInTransit(power));
  const running = power !== null && isRunning(power);
  React.useEffect(() => {
    if (!waiting && !running) return;
    let cancelled = false;
    let timer = 0;
    const tick = async () => {
      const next = await refresh();
      if (cancelled) return;
      if (next.status === "error") {
        // One missed answer does not end the wait. Ask again.
        timer = window.setTimeout(tick, waiting ? pollEvery : refreshEvery);
        return;
      }
      if (next.status !== "owner") return;
      if (isRunning(next.power)) {
        setStarting(false);
        timer = window.setTimeout(tick, refreshEvery);
        return;
      }
      if (starting || isInTransit(next.power)) {
        timer = window.setTimeout(tick, pollEvery);
      }
    };
    timer = window.setTimeout(tick, waiting ? pollEvery : refreshEvery);
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [waiting, running, starting, refresh]);

  const explain = (reason: "visitor" | "throttled" | "azure" | "network") =>
    reason === "throttled"
      ? "Too many presses. Wait a minute."
      : reason === "visitor"
        ? "The session expired. Reload and sign in again."
        : reason === "azure"
          ? "Azure refused. The fallbacks below still work."
          : "No answer from the server. Check the connection and try again.";

  const start = async () => {
    setProblem(null);
    setNotice(null);
    setStarting(true);
    const result = await startDevBox();
    if (!alive.current) return;
    if (result.ok) {
      setView((current) =>
        current.status === "owner"
          ? { ...current, power: "starting" }
          : { status: "owner", power: "starting", agent: null },
      );
      return;
    }
    setStarting(false);
    setProblem(explain(result.reason));
  };

  const hold = async (minutes: number) => {
    setProblem(null);
    const result = await holdDevBox(minutes);
    if (!alive.current) return;
    if (result.ok) {
      setNotice(
        `Hold for ${minutes / 60} hours queued. The box picks it up within a minute.`,
      );
      void refresh();
    } else setProblem(explain(result.reason));
  };

  const park = async () => {
    if (!window.confirm("Park the dev box now? Open sessions on it end."))
      return;
    setProblem(null);
    const result = await parkDevBox();
    if (!alive.current) return;
    if (result.ok) {
      setNotice("Park queued. The box deallocates itself within a minute.");
      void refresh();
    } else setProblem(explain(result.reason));
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

  const parked = isParked(view.power);
  const transit = starting || isInTransit(view.power);
  const state = running
    ? { label: "Running", color: "success" as const }
    : transit
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
        {/* Outlined once it cannot be pressed: a disabled contained button in
            the dark theme is a pale slab with no legible text. */}
        <Button
          variant={parked && !starting ? "contained" : "outlined"}
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
      {notice && (
        <Alert severity="info" onClose={() => setNotice(null)}>
          {notice}
        </Alert>
      )}
      {stale && (
        <Alert severity="warning">
          Azure did not answer the last check. The state above is its previous
          answer.
        </Alert>
      )}

      {running ? (
        <>
          <AgentPanel agent={view.agent} onHold={hold} onPark={park} />
          <BrowserPanel />
        </>
      ) : transit ? (
        <Alert severity="info">
          Azure is starting the box. This page asks again every five seconds.
          About two minutes from Start to a session.
        </Alert>
      ) : (
        <Typography variant="body1" sx={{ color: "text.secondary" }}>
          The box is parked, which is where the idle watchdog and the nightly
          check leave it. It bills nothing but disks until it starts.
        </Typography>
      )}

      <Runbook />
    </Stack>
  );
}

function capitalise(word: string): string {
  return word.length === 0 ? word : word[0].toUpperCase() + word.slice(1);
}
