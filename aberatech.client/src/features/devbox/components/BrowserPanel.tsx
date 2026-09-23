import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

/** The two names on abera.tech that reach the box from a browser tab. */
export const browserTerminalUrl = "https://devbox.abera.tech";
export const browserDesktopUrl = "https://devbox-desktop.abera.tech";

/**
 * The way in from a machine where nothing can be installed: a work computer
 * with no admin rights, an inspecting proxy and a filter that drops anything
 * but HTTPS on a categorised host. Chrome Remote Desktop fails there (WebRTC
 * and Google's relay), and on 2026-09-22 this page was the only thing on the
 * box that opened. Both names go through a Cloudflare Tunnel behind
 * Cloudflare Access: a one-time PIN to an allowed address, then the terminal
 * or the desktop, rendered by Cloudflare in the tab. No port on the box is
 * open to the internet.
 */
export default function BrowserPanel() {
  return (
    <Box>
      <Typography variant="subtitle2" gutterBottom>
        From any browser
      </Typography>
      <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap" }}>
        <Button
          href={browserTerminalUrl}
          target="_blank"
          rel="noopener"
          variant="outlined"
          size="small"
        >
          Open terminal
        </Button>
        <Button
          href={browserDesktopUrl}
          target="_blank"
          rel="noopener"
          variant="outlined"
          size="small"
        >
          Open desktop
        </Button>
      </Stack>
      <Typography
        variant="caption"
        sx={{ color: "text.secondary", display: "block", mt: 1 }}
      >
        HTTPS only, nothing to install. Type an allowed address and enter the
        PIN it receives. The desktop then asks for the VNC password. One sign-in
        covers both for 24 hours, and a desktop session keeps the box from
        parking.
      </Typography>
    </Box>
  );
}
