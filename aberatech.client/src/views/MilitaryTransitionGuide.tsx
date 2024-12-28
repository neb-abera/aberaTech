import AppAppBar from '../components/AppAppBar';
import Hero from '../components/Hero';
import Divider from '@mui/material/Divider';
import Typography from '@mui/material/Typography';
import CssBaseline from '@mui/material/CssBaseline';
import AppTheme from '../theme/AppTheme';
import Container from '@mui/material/Container';
import ZeroToThirtyDaysPostETS from '../components/ZeroToThirtyDays.tsx';
import TerminalLeave from '../components/TerminalLeave.tsx';
import ZeroToTenDays from '../components/ZeroToTenDays.tsx';
import ThirtyDays from '../components/ThirtyDays.tsx';
import ThirtyToNinetyDays from '../components/ThirtyToNinetyDays.tsx';
import NinetyToOneEightyDays from '../components/NinetyToOneEightyDays.tsx';
import SixToNineMonths from '../components/SixToNineMonths.tsx';
import NineToTwelveMonths from '../components/NineToTwelveMonths.tsx';
import TwelveToEighteenMonths from '../components/TwelveToEighteenMonths.tsx';
import TwentyFourToEighteenMonths from '../components/TwentyFourToEighteenMonths.tsx';
import Box from '@mui/material/Box';
import LongAfterETS from '../components/LongAfterETS.tsx';

export default function MilitaryTransitionGuide(props: { disableCustomTheme?: boolean }) {
  return (
    <AppTheme {...props}>
      <CssBaseline enableColorScheme />
      <AppAppBar />
      <Hero />
      <Divider />
      <Container
        sx={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: { xs: 4, sm: 8 },
          py: { xs: 8, sm: 10 },
          textAlign: { sm: 'center', md: 'left' }
        }}
      >
        <Typography
          variant="h1"
          component="h1"
          sx={{
            textAlign: 'center',
            color: 'text.primary'
          }}
        >
          The Military Transition Guide I Wish I Had
        </Typography>
        <Box
          sx={(theme) => ({
            p: 2,
            border: `1px solid ${theme.palette.divider}`,
            backgroundColor: theme.palette.background.paper,
            color: theme.palette.text.primary,
            boxShadow: theme.shadows[1]
          })}
        >
          <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
            I made this guide to share the lessons I learned during my transition. I embedded links, documents, and
            videos. Please click on those to ensure that you understand the topic in its breadth and depth. This is a
            work in progress so feel free to send me questions, comments, and recommendations.
          </Typography>

          <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
            I included some personal stories in this guide, that you can skip over if you're not interested, to
            illustrate the importance of some of these lessons. Please don't think that I did it to attack individual
            people in this guide. Most people do the best they can with the information and tools they have. But, you
            can have good people stuck in a bad bureaucratic system that causes unintended consequences for
            transitioning servicemembers. You will need to get the bureaucracy to work for you, not against you. Do not
            get discouraged by it. You need to advocate for yourself, your transition is worth fighting for.
          </Typography>
        </Box>
        <TwentyFourToEighteenMonths />
        <TwelveToEighteenMonths />
        <NineToTwelveMonths />
        <SixToNineMonths />
        <NinetyToOneEightyDays />
        <ThirtyToNinetyDays />
        <ThirtyDays />
        <ZeroToTenDays />
        <TerminalLeave />
        <ZeroToThirtyDaysPostETS />
        <LongAfterETS />
      </Container>
    </AppTheme>
  );
}
