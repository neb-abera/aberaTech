import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import { Link } from "react-router";
import PageShell from "../components/PageShell";

/**
 * The page for an address this site does not have.
 *
 * Every unknown path used to render the empty shell: a typo, a dead link and a
 * page that had been deleted all looked identical to a reader, and the server
 * answered 200 to all three. The server now answers 404 for anything outside
 * site/routes.ts and serves this.
 */
export default function NotFound(props: { disableCustomTheme?: boolean }) {
  return (
    <PageShell
      {...props}
      maxWidth="md"
      title="No page at that address"
      intro="The link may be old, or the address mistyped. Everything on this site is one of these two lists."
    >
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={2}
        sx={{ justifyContent: "center" }}
      >
        <Button variant="contained" component={Link} to="/">
          Home
        </Button>
        <Button variant="outlined" component={Link} to="/guides">
          Guides
        </Button>
        <Button variant="outlined" component={Link} to="/projects">
          Projects
        </Button>
      </Stack>
    </PageShell>
  );
}
