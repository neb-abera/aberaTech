import Alert from "@mui/material/Alert";
import AlertTitle from "@mui/material/AlertTitle";
import * as React from "react";
import { fetchPushoverDevices, type PushoverDevicesView } from "../core/api";

/**
 * A red warning when Pushover has no device to deliver to. Pushover answers
 * every send with success then, so the page's "sent" says nothing about the
 * phone.
 */
export default function PushoverWarning({
  read = fetchPushoverDevices,
}: {
  read?: () => Promise<PushoverDevicesView | null>;
}) {
  const [view, setView] = React.useState<PushoverDevicesView | null>(null);
  React.useEffect(() => {
    let live = true;
    void read().then((answer) => {
      if (live) setView(answer);
    });
    return () => {
      live = false;
    };
  }, [read]);

  if (!view?.known || view.devices.length > 0) return null;
  return (
    <Alert severity="error" sx={{ mb: 2 }}>
      <AlertTitle>Pushover has no device</AlertTitle>
      Pushover takes every message and delivers none, so the backup and the test
      buttons reach no phone. Open the Pushover app on the phone, sign in, and
      buy its licence if the trial has ended.
    </Alert>
  );
}
