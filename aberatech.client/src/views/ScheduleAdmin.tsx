import Box from "@mui/material/Box";
import Container from "@mui/material/Container";
import CssBaseline from "@mui/material/CssBaseline";
import Divider from "@mui/material/Divider";
import Typography from "@mui/material/Typography";
import AppAppBar from "../components/AppAppBar";
import AdminPanel from "../features/scheduling/components/AdminPanel";
import AppTheme from "../theme/AppTheme";

export default function ScheduleAdmin(props: { disableCustomTheme?: boolean }) {
  return (
    <AppTheme {...props}>
      <CssBaseline enableColorScheme />
      <AppAppBar />
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
    </AppTheme>
  );
}
