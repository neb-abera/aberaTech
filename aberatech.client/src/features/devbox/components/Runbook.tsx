import Box from "@mui/material/Box";
import Link from "@mui/material/Link";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemText from "@mui/material/ListItemText";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type * as React from "react";

/**
 * How to get a session back when the button above is not enough, and what
 * parks the box in the first place. Kept in the page rather than in a
 * document on the server: the day it is needed is the day nothing else is
 * reachable, and this text has to be here whatever the database says.
 */
export default function Runbook() {
  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h6" component="h2" gutterBottom>
          From the phone, start to session
        </Typography>
        <List dense disablePadding>
          <Step
            n={1}
            primary="Open abera.tech/devbox and sign in with Google"
            secondary="Bookmark it. The sign-in lasts twelve hours."
          />
          <Step
            n={2}
            primary="Press Start dev box"
            secondary="The chip goes Starting, then Running. About 90 seconds."
          />
          <Step
            n={3}
            primary="Wait for the green box to say Remote Control is up"
            secondary="The agent reports within a minute of the box coming up. Refresh if it is slow."
          />
          <Step
            n={4}
            primary="Tap Open devbox in Claude"
            secondary="It lands in the Claude app on the Remote Control picker with devbox selected. Without the link: Claude app, Code tab, the environment picker that says Default, Remote Control, devbox."
          />
        </List>
      </Box>

      <Box>
        <Typography variant="h6" component="h2" gutterBottom>
          If this page is down
        </Typography>
        <List dense disablePadding>
          <Step
            n={1}
            primary="Phone: the Microsoft Azure app"
            secondary="Install it from the App Store, sign in as neb.abera@outlook.com. Home, Resources, tap devbox (the Virtual machine, not the disk or the IP), Start at the top. Confirm. Allow two minutes, then step 4 above."
          />
          <Step
            n={2}
            primary="Any browser: portal.azure.com"
            secondary={
              <>
                Sign in as neb.abera@outlook.com. Type devbox in the search bar
                at the top, choose the Virtual machine result, Start in the
                toolbar. Direct link:{" "}
                <Link
                  href="https://portal.azure.com/#@/resource/subscriptions/cbc1bfa3-2730-4c9e-b56e-de58df35f671/resourceGroups/devbox-rg/providers/Microsoft.Compute/virtualMachines/devbox/overview"
                  rel="noopener"
                >
                  the devbox VM
                </Link>
                .
              </>
            }
          />
          <Step
            n={3}
            primary="Laptop: one command in Terminal"
            secondary={
              <Box component="code" sx={{ fontFamily: "monospace" }}>
                ~/repos/devbox/devbox up
              </Box>
            }
          />
        </List>
      </Box>

      <Box>
        <Typography variant="h6" component="h2" gutterBottom>
          What parks it
        </Typography>
        <List dense disablePadding>
          <ListItem disableGutters>
            <ListItemText
              primary="30 idle minutes"
              secondary="No SSH session, no running container, load under 1.0, no Claude session doing work. A session waiting on you counts as idle. Hold above overrides this."
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

function Step({
  n,
  primary,
  secondary,
}: {
  n: number;
  primary: string;
  secondary: React.ReactNode;
}) {
  return (
    <ListItem disableGutters sx={{ alignItems: "flex-start" }}>
      <Typography
        variant="body1"
        sx={{ minWidth: 28, color: "text.secondary", pt: "2px" }}
      >
        {n}.
      </Typography>
      <ListItemText primary={primary} secondary={secondary} />
    </ListItem>
  );
}
