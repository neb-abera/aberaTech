import Link from "@mui/material/Link";
import PageShell from "../components/PageShell";
import PlannerBoard from "../features/planner/components/PlannerBoard";
import { projects } from "../site/sections";

// The card on /projects and this heading come from the same entry.
const found = projects.find((project) => project.to === "/planner");
if (!found) throw new Error("sections: /planner is missing from projects");
const entry = found;

export default function CoursePlanner(props: { disableCustomTheme?: boolean }) {
  return (
    <PageShell
      {...props}
      maxWidth="xl"
      title={entry.title}
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
