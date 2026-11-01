import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import type { AgentReport } from "../core/api";

export interface AgentPanelProps {
  agent: AgentReport | null;
  onHold: (minutes: number) => void;
  onPark: () => void;
  onLogin: () => void;
  onLoginCode: (code: string) => void;
}

/** A report older than this is stale: the agent ticks once a minute. */
const staleAfterSeconds = 180;

/** Warn this many days before the box's Claude sign-in ends. */
const warnLoginDays = 3;

/**
 * What the box says about itself, and the two things the owner can tell it.
 *
 * The box reports once a minute over an outbound call and takes its orders
 * from the reply, so there is no port to open and nothing on the internet
 * to guard. The one thing worth a tap here is the link: the Remote Control
 * environment changes every boot, and the agent reads the current one from
 * the service's own log.
 */
export default function AgentPanel({
  agent,
  onHold,
  onPark,
  onLogin,
  onLoginCode,
}: AgentPanelProps) {
  if (agent === null) {
    return (
      <Alert severity="success">
        Running. Open the Claude app, Code, Remote Control, and pick{" "}
        <b>devbox</b>. It registers about a minute after the box comes up.
      </Alert>
    );
  }

  if (!agent.seen || agent.seenSecondsAgo > staleAfterSeconds) {
    return (
      <Alert severity="warning">
        Running, but the box has not reported{" "}
        {agent.seen
          ? `for ${Math.round(agent.seenSecondsAgo / 60)} minutes`
          : "yet"}
        . It reports within a minute of coming up. Open the Claude app, Code,
        Remote Control, and look for <b>devbox</b>.
      </Alert>
    );
  }

  const holdLeft = agent.holdUntil
    ? Math.max(
        0,
        Math.round((Date.parse(agent.holdUntil) - Date.now()) / 60000),
      )
    : 0;
  // "active" alone is what a failing service reads for a second every 10 s.
  // Up means the service's own log shows it connected.
  const expired = agent.claudeLogin === "expired";
  const registered =
    agent.remoteControl === "active" && agent.claudeLogin === "ok";
  const loginDaysLeft = agent.loginExpiresAt
    ? Math.ceil((Date.parse(agent.loginExpiresAt) - Date.now()) / 86400000)
    : null;
  const endingSoon =
    !expired && loginDaysLeft !== null && loginDaysLeft <= warnLoginDays;

  return (
    <Stack spacing={2}>
      <Alert severity={expired ? "error" : registered ? "success" : "warning"}>
        {expired ? (
          <>
            Claude login expired. Remote Control cannot start, so <b>devbox</b>{" "}
            is not in the Claude app. Sign in again below.
          </>
        ) : registered ? (
          <>
            Remote Control is up with {agent.sessions}{" "}
            {agent.sessions === 1 ? "session" : "sessions"}.{" "}
            {agent.environmentUrl ? (
              <Button
                href={agent.environmentUrl}
                variant="contained"
                size="small"
                sx={{ ml: 1 }}
              >
                Open devbox in Claude
              </Button>
            ) : (
              "Open the Claude app, Code, Remote Control, and pick devbox."
            )}
          </>
        ) : (
          <>
            The box is up but Remote Control has not connected. The service is{" "}
            {agent.remoteControl ?? "not reporting"}. Give it a minute, then
            Refresh.
          </>
        )}
      </Alert>

      {endingSoon && (
        <Alert severity="warning">
          Claude sign-in ends in {loginDaysLeft}{" "}
          {loginDaysLeft === 1 ? "day" : "days"}. Sign in again now and open
          sessions keep running.
        </Alert>
      )}

      {agent.googleLogin === "signed-out" && (
        <Alert severity="warning">
          The box's Chrome lost its Google sign-in. Sign in on the box's
          desktop, below.
        </Alert>
      )}

      {(expired || endingSoon || agent.loginUrl) && (
        <SignIn
          url={agent.loginUrl}
          waiting={agent.pending.login}
          onLogin={onLogin}
          onLoginCode={onLoginCode}
        />
      )}

      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        Reported {Math.round(agent.seenSecondsAgo)} s ago. Up{" "}
        {formatUptime(agent.uptimeSeconds)}, load {agent.load.toFixed(2)}.
        {holdLeft > 0
          ? ` Held for another ${holdLeft} minutes.`
          : " Parks after 30 idle minutes."}
        {agent.pending.holdMinutes || agent.pending.park || agent.pending.login
          ? " An order is waiting for the next report."
          : ""}
      </Typography>

      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Keep it up, or park it
        </Typography>
        <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap" }}>
          <Button variant="outlined" size="small" onClick={() => onHold(120)}>
            Hold 2 h
          </Button>
          <Button variant="outlined" size="small" onClick={() => onHold(480)}>
            Hold 8 h
          </Button>
          <Button
            variant="outlined"
            color="warning"
            size="small"
            onClick={onPark}
          >
            Park now
          </Button>
        </Stack>
        <Typography
          variant="caption"
          sx={{ color: "text.secondary", display: "block", mt: 1 }}
        >
          A hold keeps the box up through the idle watchdog and the nightly
          check, for a long unattended job. Park ends every session on it.
        </Typography>
      </Box>
    </Stack>
  );
}

/**
 * The Claude sign-in, for a thumb. The box runs `claude auth login` and sends
 * the link here. The code Claude shows goes back to the box, where the sign-in
 * is waiting for it. A sign-in nobody finishes ends after 10 minutes.
 */
function SignIn({
  url,
  waiting,
  onLogin,
  onLoginCode,
}: {
  url: string | null;
  waiting: boolean;
  onLogin: () => void;
  onLoginCode: (code: string) => void;
}) {
  const [code, setCode] = React.useState("");

  if (!url) {
    return (
      <Box>
        <Button variant="contained" onClick={onLogin} disabled={waiting}>
          Sign in again
        </Button>
        {waiting && (
          <Typography variant="body2" sx={{ color: "text.secondary", mt: 1 }}>
            Asking the box. The link appears here within a minute.
          </Typography>
        )}
      </Box>
    );
  }

  return (
    <Stack spacing={1.5}>
      <Box>
        <Button
          variant="contained"
          href={url}
          target="_blank"
          rel="noopener noreferrer"
        >
          Open the Claude sign-in
        </Button>
      </Box>
      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        Approve it, copy the code Claude shows, and paste it here.
      </Typography>
      <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
        <TextField
          label="Code from Claude"
          size="small"
          value={code}
          onChange={(event) => setCode(event.target.value)}
          autoComplete="off"
          fullWidth
        />
        <Button
          variant="contained"
          disabled={code.trim().length === 0}
          onClick={() => {
            onLoginCode(code.trim());
            setCode("");
          }}
        >
          Send code
        </Button>
      </Stack>
    </Stack>
  );
}

function formatUptime(seconds: number): string {
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  return hours > 0 ? `${hours} h ${minutes} min` : `${minutes} min`;
}
