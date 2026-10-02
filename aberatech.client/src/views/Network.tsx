import PageShell from "../components/PageShell";
import NetworkPanel from "../features/network/components/NetworkPanel";

/**
 * The owner's network, drawn: people around the organizations they belong
 * to, by sector. The document lives on the server behind the owner's
 * sign-in and is edited on the page, so a new version needs no deploy.
 */
export default function Network(props: { disableCustomTheme?: boolean }) {
  return (
    <PageShell
      {...props}
      maxWidth="xl"
      title="Network"
      intro="People and organizations, drawn by sector. Colour is the sector, size is how soon to act, the ring is where things stand."
    >
      <NetworkPanel />
    </PageShell>
  );
}
