import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { AgentReport } from "../core/api";

export interface AgentPanelProps {
  agent: AgentReport | null;
  onHold: (minutes: number) => void;
  onPark: () => void;
}

/** A report older than this is stale: the agent ticks once a minute. */
const staleAfterSeconds = 180;

/**
 * What the box says about itself, and the two things the owner can tell it.
 *
 * The box reports once a minute over an outbound call and takes its orders
 * from the reply, so there is no port to open and nothing on the internet
 * to guard. The one thing worth a tap here is the link: the Remote Control
 * environment changes every boot, and the agent reads the current one from
 * the service's own log.
 */
export default function AgentPanel({ agent, onHold, onPark }: AgentPanelProps) {
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
  const registered = agent.remoteControl === "active";

  return (
    <Stack spacing={2}>
      <Alert severity={registered ? "success" : "warning"}>
        {registered ? (
          <>
            Remote Control is up with {agent.sessions}{" "}
            {agent.sessions === 1 ? "session" : "sessions"}.{" "}
            {agent.environmentUrl ? (
              <>
                <Button
                  href={agent.environmentUrl}
                  variant="contained"
                  size="small"
                  sx={{ ml: 1 }}
                >
                  Open devbox in Claude
                </Button>
              </>
            ) : (
              "Open the Claude app, Code, Remote Control, and pick devbox."
            )}
          </>
        ) : (
          <>
            The box is up but the Remote Control service is{" "}
            {agent.remoteControl ?? "not reporting"}. Give it a minute, then
            Refresh.
          </>
        )}
      </Alert>

      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        Reported {Math.round(agent.seenSecondsAgo)} s ago. Up{" "}
        {formatUptime(agent.uptimeSeconds)}, load {agent.load.toFixed(2)}.
        {holdLeft > 0
          ? ` Held for another ${holdLeft} minutes.`
          : " Parks after 30 idle minutes."}
        {agent.pending.holdMinutes || agent.pending.park
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

function formatUptime(seconds: number): string {
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  return hours > 0 ? `${hours} h ${minutes} min` : `${minutes} min`;
}
