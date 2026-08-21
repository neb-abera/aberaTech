import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import CssBaseline from '@mui/material/CssBaseline';
import Divider from '@mui/material/Divider';
import Typography from '@mui/material/Typography';
import AppAppBar from '../components/AppAppBar';
import AppTheme from '../theme/AppTheme';
import SchedulePanel from '../features/scheduling/components/SchedulePanel';

export default function ScheduleTime(props: { disableCustomTheme?: boolean }) {
  return (
    <AppTheme {...props}>
      <CssBaseline enableColorScheme />
      <AppAppBar />
      <Container maxWidth="md" sx={{ pt: { xs: 14, sm: 16 }, pb: 8 }}>
        <Box sx={{ mb: 3 }}>
          <Typography variant="h4" component="h1" sx={{ fontWeight: 600 }}>
            Schedule time with me
          </Typography>
          <Typography variant="body1" sx={{ color: 'text.secondary', mt: 1, maxWidth: 760 }}>
            Pick a time from my calendar, or join the queue when one is open and I will work down the line. Either way
            this is the same link, and every time on this page is shown in your own time zone rather than mine.
          </Typography>
        </Box>
        <Divider sx={{ mb: 3 }} />
        <SchedulePanel />
      </Container>
    </AppTheme>
  );
}
