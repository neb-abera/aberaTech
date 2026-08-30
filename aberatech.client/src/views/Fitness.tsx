import Box from "@mui/material/Box";
import Container from "@mui/material/Container";
import CssBaseline from "@mui/material/CssBaseline";
import Divider from "@mui/material/Divider";
import Typography from "@mui/material/Typography";
import AppAppBar from "../components/AppAppBar";
import FitnessPanel from "../features/fitness/components/FitnessPanel";
import AppTheme from "../theme/AppTheme";

export default function Fitness(props: { disableCustomTheme?: boolean }) {
  return (
    <AppTheme {...props}>
      <CssBaseline enableColorScheme />
      <AppAppBar />
      <Container maxWidth="lg" sx={{ pt: { xs: 14, sm: 16 }, pb: 8 }}>
        <Box sx={{ mb: 3 }}>
          <Typography variant="h4" component="h1" sx={{ fontWeight: 600 }}>
            Military athlete console
          </Typography>
          <Typography
            variant="body1"
            sx={{ color: "text.secondary", mt: 1, maxWidth: 760 }}
          >
            Verified training data in, sourced predictions out. Adjust the dose,
            the compliance and the bodyweight to see where the fitness lands —
            or name the goal and the date, and see what it costs.
          </Typography>
        </Box>
        <Divider sx={{ mb: 3 }} />
        <FitnessPanel />
      </Container>
    </AppTheme>
  );
}
