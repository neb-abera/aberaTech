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
            Signals course planner
          </Typography>
          <Typography variant="body1" sx={{ color: 'text.secondary', mt: 1, maxWidth: 760 }}>
            The whole Johns Hopkins Engineering for Professionals electrical and computer engineering catalog, with the
            prerequisites enforced, the degree rules checked, and the five year clock counted from the first course you
            apply rather than the first course you take. Pick a curated track or browse by focus area, then drag courses
            between terms. A term refuses a course whose prerequisites are not behind it.
          </Typography>
          <Typography variant="caption" sx={{ color: 'text.disabled', display: 'block', mt: 1.5 }}>
            Course data parsed from the{' '}
            <Link
              href="https://e-catalogue.jhu.edu/engineering/engineering-professionals/electrical-computer-engineering/"
              target="_blank"
              rel="noopener"
            >
              JHU e-catalogue
            </Link>
            . Verify anything that matters against the catalogue and your adviser before you register.
          </Typography>
        </Box>
        <Divider sx={{ mb: 3 }} />
        <PlannerBoard />
      </Container>
    </AppTheme>
  );
}
