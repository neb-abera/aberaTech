import PageShell from "../components/PageShell";
import MarksmanshipPanel from "../features/marksmanship/components/MarksmanshipPanel";

/**
 * The owner's President's Hundred plan and training log, read live from
 * Notion. Notion stays the record. Nothing is copied here.
 */
export default function Marksmanship(props: { disableCustomTheme?: boolean }) {
  return (
    <PageShell
      {...props}
      maxWidth="lg"
      title="Marksmanship"
      intro="My President's Hundred plan and training log, read live from Notion. This site keeps no copy."
    >
      <MarksmanshipPanel />
    </PageShell>
  );
}
