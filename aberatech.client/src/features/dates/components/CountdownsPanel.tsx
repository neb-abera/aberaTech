import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import CircularProgress from "@mui/material/CircularProgress";
import Stack from "@mui/material/Stack";
import * as React from "react";
import { accountSignedIn } from "../../../hooks/useAccount";
import { type AlertsView, fetchAlerts } from "../../alerts/core/api";
import SignInToSee from "../../progress/components/SignInToSee";
import CountdownsSection, { type CountdownsApi } from "./CountdownsSection";

type View = AlertsView | { status: "loading" };

/**
 * The owner's countdowns under the calculator. A visitor's browser never
 * asks for them: who is signed in is asked first, and a visitor gets the
 * sign-in button.
 */
export default function CountdownsPanel({
  load = fetchAlerts,
  signedIn = accountSignedIn,
  api,
  now,
}: {
  load?: () => Promise<AlertsView>;
  signedIn?: () => Promise<boolean>;
  api?: CountdownsApi;
  now?: number;
}) {
  const [view, setView] = React.useState<View>({ status: "loading" });

  const refresh = React.useCallback(async () => {
    setView((await signedIn()) ? await load() : { status: "visitor" });
  }, [load, signedIn]);

  React.useEffect(() => {
    void refresh();
  }, [refresh]);

  switch (view.status) {
    case "loading":
      return <CircularProgress size={28} aria-label="Loading countdowns" />;
    case "visitor":
      return (
        <SignInToSee
          message="Countdowns are the owner's. Sign in to see them."
          returnUrl="/dates"
        />
      );
    case "unconfigured":
      return (
        <Alert severity="info">
          Countdowns are kept with the alerts, which this deployment has not set
          up.
        </Alert>
      );
    case "error":
      return (
        <Stack spacing={2} sx={{ alignItems: "flex-start" }}>
          <Alert severity="error">The server did not answer.</Alert>
          <Button variant="outlined" onClick={() => void refresh()}>
            Refresh
          </Button>
        </Stack>
      );
    default:
      return (
        <CountdownsSection
          countdowns={view.state.countdowns ?? []}
          onState={(state) => setView({ status: "owner", state })}
          api={api}
          now={now}
        />
      );
  }
}
