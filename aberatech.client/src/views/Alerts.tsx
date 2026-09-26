import PageShell from "../components/PageShell";
import AlertsPanel from "../features/alerts/components/AlertsPanel";

/**
 * The owner's calendar alerts: on or muted, the next ones, and the buttons.
 * Reached from the app bar once signed in; a visitor gets a sign-in button.
 */
export default function Alerts(props: { disableCustomTheme?: boolean }) {
  return (
    <PageShell {...props} maxWidth="md" title="Alerts">
      <AlertsPanel />
    </PageShell>
  );
}
