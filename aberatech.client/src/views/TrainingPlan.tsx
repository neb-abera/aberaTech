import PageShell from "../components/PageShell";
import TrainingPlanPanel from "../features/training-plan/components/TrainingPlanPanel";

/**
 * The owner's fitness plan, read live from Notion one week at a time.
 * Notion holds the plan and every edit to it. Nothing is copied here.
 */
export default function TrainingPlan(props: { disableCustomTheme?: boolean }) {
  return (
    <PageShell
      {...props}
      maxWidth="lg"
      title="Fitness plan"
      intro="My fitness plan, read live from Notion. I edit it there."
    >
      <TrainingPlanPanel />
    </PageShell>
  );
}
