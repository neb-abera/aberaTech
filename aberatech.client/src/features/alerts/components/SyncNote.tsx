import CheckCircleOutlined from "@mui/icons-material/CheckCircleOutlined";
import CloudOffOutlined from "@mui/icons-material/CloudOffOutlined";
import Box from "@mui/material/Box";
import CircularProgress from "@mui/material/CircularProgress";
import Fade from "@mui/material/Fade";
import Typography from "@mui/material/Typography";
import * as React from "react";
import type { SyncStatus } from "./usePendingChanges";

const words: Record<SyncStatus, string> = {
  sending: "Sending…",
  saved: "Saved",
  waiting: "Waiting for a connection. It will send when you are back online.",
};

/**
 * One line beside a control that already moved: Sending, then Saved, which
 * fades, or Waiting for a connection. A polite live region, so a screen
 * reader hears each step without losing its place.
 */
export default function SyncNote({ status }: { status?: SyncStatus }) {
  // The last status stays in place while "Saved" fades out.
  const [shown, setShown] = React.useState(status);
  if (status && status !== shown) setShown(status);

  return (
    <Box role="status" aria-live="polite" sx={{ minHeight: "1.25rem" }}>
      <Fade in={Boolean(status)} timeout={{ enter: 150, exit: 600 }}>
        <Box
          component="span"
          sx={{ display: "inline-flex", alignItems: "center", gap: 0.5 }}
        >
          {shown === "sending" && (
            <CircularProgress size={12} thickness={5} aria-hidden="true" />
          )}
          {shown === "saved" && (
            <CheckCircleOutlined
              color="success"
              sx={{ fontSize: "1rem" }}
              aria-hidden="true"
            />
          )}
          {shown === "waiting" && (
            <CloudOffOutlined
              color="warning"
              sx={{ fontSize: "1rem" }}
              aria-hidden="true"
            />
          )}
          {shown && (
            <Typography
              variant="caption"
              sx={{
                color: shown === "waiting" ? "text.primary" : "text.secondary",
              }}
            >
              {words[shown]}
            </Typography>
          )}
        </Box>
      </Fade>
    </Box>
  );
}
