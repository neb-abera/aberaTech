import PageShell from "../components/PageShell";
import LinksPanel from "../features/links/components/LinksPanel";

/**
 * The owner's bookmarks. Reached by address, not from the navigation: a
 * visitor gets a sign-in button and nothing else, and the list itself lives
 * on the server behind the same owner policy as the fitness console.
 */
export default function Links(props: { disableCustomTheme?: boolean }) {
  return (
    <PageShell
      {...props}
      maxWidth="md"
      title="Links"
      intro="One list, kept on the server, so it is the same on every device."
    >
      <LinksPanel />
    </PageShell>
  );
}
