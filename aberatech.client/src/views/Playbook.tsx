import PageShell from "../components/PageShell";
import PlaybookPanel from "../features/playbook/components/PlaybookPanel";

/**
 * The owner's Notion playbook, read live for a computer that cannot reach
 * Notion. Notion stays the record. Nothing is copied here.
 */
export default function Playbook(props: { disableCustomTheme?: boolean }) {
  return (
    <PageShell
      {...props}
      maxWidth="lg"
      title="Playbook"
      intro="My Notion playbook, read live. Notion holds it. This site keeps no copy."
    >
      <PlaybookPanel />
    </PageShell>
  );
}
