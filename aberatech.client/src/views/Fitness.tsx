import PageShell from "../components/PageShell";
import FitnessPanel from "../features/fitness/components/FitnessPanel";

/**
 * The one page layout, as every other content page uses. This page used to
 * assemble its own chrome, which left it the only page on the site with no
 * footer — so once a visitor arrived there was nothing to click but Back.
 */
export default function Fitness(props: { disableCustomTheme?: boolean }) {
  return (
    <PageShell
      {...props}
      title="Military athlete console"
      intro="Verified training data in, sourced predictions out. Adjust the dose, the compliance and the bodyweight to see where the fitness lands — or name the goal and the date, and see what it costs."
    >
      <FitnessPanel />
    </PageShell>
  );
}
