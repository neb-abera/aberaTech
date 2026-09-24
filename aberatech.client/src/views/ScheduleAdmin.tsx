import Box from "@mui/material/Box";
import Container from "@mui/material/Container";
import Divider from "@mui/material/Divider";
import Typography from "@mui/material/Typography";
import SiteFrame from "../components/SiteFrame";
import AdminPanel from "../features/scheduling/components/AdminPanel";

/**
 * The owner's view of the queue.
 *
 * It built its own chrome until 2026-09-24 and was the one page on the site
 * with no footer, which is how tools/render-copy.mjs found it.
 */

export default function ScheduleAdmin(props: { disableCustomTheme?: boolean }) {
  return (
    <SiteFrame {...props}>
      <Container maxWidth="md" sx={{ pt: { xs: 14, sm: 16 }, pb: 8 }}>
        <Box sx={{ mb: 3 }}>
          <Typography variant="h4" component="h1" sx={{ fontWeight: 600 }}>
            Run the queue
          </Typography>
          <Typography
            variant="body1"
            sx={{ color: "text.secondary", mt: 1, maxWidth: 760 }}
          >
            Open a session, work down the line, and close it when you are done.
            Everyone waiting is told automatically when their turn moves.
          </Typography>
        </Box>
        <Divider sx={{ mb: 3 }} />
        <AdminPanel />
      </Container>
    </SiteFrame>
  );
}
