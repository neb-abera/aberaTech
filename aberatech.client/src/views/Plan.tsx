import PageShell from "../components/PageShell";
import PlanPanel from "../features/plan/components/PlanPanel";

/**
 * The owner's plan, rendered from a document kept on the server. Reached
 * from the app bar once signed in; a visitor gets a sign-in button.
 */
export default function Plan(props: { disableCustomTheme?: boolean }) {
  return (
    <PageShell {...props} maxWidth="md" title="Plan">
      <PlanPanel />
    </PageShell>
  );
}
