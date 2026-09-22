import PageShell from "../components/PageShell";
import DevBoxPanel from "../features/devbox/components/DevBoxPanel";

/**
 * The owner's dev box: its state, a Start button, and the way back to a
 * session from a phone. Reached from the app bar once signed in; a visitor
 * gets a sign-in button.
 */
export default function DevBox(props: { disableCustomTheme?: boolean }) {
  return (
    <PageShell {...props} maxWidth="md" title="Dev box">
      <DevBoxPanel />
    </PageShell>
  );
}
