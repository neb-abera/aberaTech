import PageShell from "../components/PageShell";
import TrainingPlan from "../features/rf-training/components/TrainingPlan";
import { copy } from "../features/rf-training/core/plan";
import { type Entry, guides } from "../site/sections";

// Found by path, as sections.ts finds the bar's action: an index would point
// at a different guide the first time one was inserted above this one.
const found = guides.find((guide) => guide.to === "/rf-training");
if (!found) throw new Error("sections: /rf-training is missing from guides");
const entry: Entry = found;

/**
 * A reading page at md width: it is a checklist and prose, not a board.
 */
export default function RfTraining(props: { disableCustomTheme?: boolean }) {
  return (
    <PageShell
      {...props}
      title={entry.title}
      intro={copy.intro}
      note={copy.note}
      maxWidth="md"
    >
      <TrainingPlan />
    </PageShell>
  );
}
