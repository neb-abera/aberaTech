import PageShell from "../components/PageShell";
import TrainingPanel from "../features/training/components/TrainingPanel";

/**
 * Competitive math and programming, as a course player. The page keeps the
 * order and the record. The material lives in the systems it points to.
 */
export default function Training(props: { disableCustomTheme?: boolean }) {
  return (
    <PageShell
      {...props}
      maxWidth="md"
      title="Training"
      intro="My path in competitive math and programming, from the basics to olympiad level. A rating is a milestone of mastery. The goal is the material itself, for life."
      note="The material lives in USACO Guide, AoPS, Codeforces, AtCoder, and CSES. This page keeps the order, what is due today, and my record."
    >
      <TrainingPanel />
    </PageShell>
  );
}
