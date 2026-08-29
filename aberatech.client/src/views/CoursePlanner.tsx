import Link from "@mui/material/Link";
import PageShell from "../components/PageShell";
import PlannerBoard from "../features/planner/components/PlannerBoard";

export default function CoursePlanner(props: { disableCustomTheme?: boolean }) {
  return (
    <PageShell
      {...props}
      maxWidth="xl"
      title="Learning RF and Signal Processing"
      intro="Plan a Johns Hopkins Engineering for Professionals master&rsquo;s in electrical and computer engineering. All 138 courses, with prerequisites and the degree rules checked as you go. Start from a track or browse by focus area, then drag courses between terms."
      note={
        <>
          Course data is from the{" "}
          <Link
            href="https://e-catalogue.jhu.edu/engineering/engineering-professionals/electrical-computer-engineering/"
            target="_blank"
            rel="noopener"
          >
            JHU e-catalogue
          </Link>
          . Check anything that matters with your adviser before you register.
        </>
      }
    >
      <PlannerBoard />
    </PageShell>
  );
}
