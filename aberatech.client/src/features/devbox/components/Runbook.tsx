import Box from "@mui/material/Box";
import Link from "@mui/material/Link";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemText from "@mui/material/ListItemText";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

/**
 * How to get a session back when the button above is not enough, and what
 * parks the box in the first place. Kept in the page rather than in a
 * document on the server: the day it is needed is the day nothing else is
 * reachable, and this text has to be here whatever the database says.
 */
export default function Runbook() {
  return (
    <Stack spacing={2}>
      <Box>
        <Typography variant="h6" component="h2" gutterBottom>
          If the button fails
        </Typography>
        <List dense disablePadding>
          <ListItem disableGutters>
            <ListItemText
              primary="Phone: the Azure app"
              secondary="Virtual machines, devbox, Start. Two minutes later devbox is back in the Remote Control picker."
            />
          </ListItem>
          <ListItem disableGutters>
            <ListItemText
              primary="Any browser: the Azure portal"
              secondary={
                <>
                  <Link href="https://portal.azure.com" rel="noopener">
                    portal.azure.com
                  </Link>
                  , devbox, Start.
                </>
              }
            />
          </ListItem>
          <ListItem disableGutters>
            <ListItemText
              primary="Laptop: one command"
              secondary={
                <Box component="code" sx={{ fontFamily: "monospace" }}>
                  ~/repos/devbox/devbox up
                </Box>
              }
            />
          </ListItem>
        </List>
      </Box>

      <Box>
        <Typography variant="h6" component="h2" gutterBottom>
          Then
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Claude app, Code, Remote Control, pick devbox in the environment
          picker (the one that says Default). Same repos, same rules, same
          signed commits. The environment link changes every boot, so pick it by
          name.
        </Typography>
      </Box>

      <Box>
        <Typography variant="h6" component="h2" gutterBottom>
          What parks it
        </Typography>
        <List dense disablePadding>
          <ListItem disableGutters>
            <ListItemText
              primary="30 idle minutes"
              secondary="No SSH session, no running container, load under 1.0. A Claude session waiting on you counts as idle."
            />
          </ListItem>
          <ListItem disableGutters>
            <ListItemText
              primary="03:00 UTC every day, unless somebody is attached"
              secondary="23:00 in Washington. An SSH session or a Claude session still working keeps it up. A leaked container does not."
            />
          </ListItem>
          <ListItem disableGutters>
            <ListItemText
              primary="Cost"
              secondary="$2.74 an hour running (48 cores). About $19 a month parked."
            />
          </ListItem>
        </List>
      </Box>
    </Stack>
  );
}
