import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import CssBaseline from '@mui/material/CssBaseline';
import Divider from '@mui/material/Divider';
import Link from '@mui/material/Link';
import Typography from '@mui/material/Typography';
import AppAppBar from '../components/AppAppBar';
import AppTheme from '../theme/AppTheme';
import PlannerBoard from '../features/planner/components/PlannerBoard';

export default function CoursePlanner(props: { disableCustomTheme?: boolean }) {
  return (
    <AppTheme {...props}>
      <CssBaseline enableColorScheme />
      <AppAppBar />
      <Container maxWidth="xl" sx={{ pt: { xs: 14, sm: 16 }, pb: 8 }}>
        <Box sx={{ mb: 3 }}>
          <Typography variant="h4" component="h1" sx={{ fontWeight: 600 }}>
            Learning RF and Signal Processing
          </Typography>
          <Typography variant="body1" sx={{ color: 'text.secondary', mt: 1, maxWidth: 760 }}>
            Plan a Johns Hopkins Engineering for Professionals master&apos;s in electrical and computer engineering. All
            138 courses, with prerequisites and the degree rules checked as you go.
          </Typography>
          <Typography variant="body1" sx={{ color: 'text.secondary', mt: 1, maxWidth: 760 }}>
            Start from a track or browse by focus area, then drag courses between terms. A term will not take a course
            before its prerequisites.
          </Typography>
          <Typography variant="caption" sx={{ color: 'text.disabled', display: 'block', mt: 1.5 }}>
            Course data is from the{' '}
            <Link
              href="https://e-catalogue.jhu.edu/engineering/engineering-professionals/electrical-computer-engineering/"
              target="_blank"
              rel="noopener"
            >
              JHU e-catalogue
            </Link>
            . Check anything that matters with your adviser before you register.
          </Typography>
        </Box>
        <Divider sx={{ mb: 3 }} />
        <PlannerBoard />
      </Container>
    </AppTheme>
  );
}
