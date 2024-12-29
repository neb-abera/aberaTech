import React, { Suspense, useState } from 'react';
import AppAppBar from '../components/AppAppBar';
import Hero from '../components/Hero';
import Divider from '@mui/material/Divider';
import Typography from '@mui/material/Typography';
import CssBaseline from '@mui/material/CssBaseline';
import AppTheme from '../theme/AppTheme';
import Container from '@mui/material/Container';
import Box from '@mui/material/Box';
import Accordion from '@mui/material/Accordion';
import AccordionSummary from '@mui/material/AccordionSummary';
import AccordionDetails from '@mui/material/AccordionDetails';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';

// Lazy-loaded components
const ZeroToThirtyDaysPostETS = React.lazy(() => import('../components/ZeroToThirtyDays'));
const TerminalLeave = React.lazy(() => import('../components/TerminalLeave'));
const ZeroToTenDays = React.lazy(() => import('../components/ZeroToTenDays'));
const ThirtyDays = React.lazy(() => import('../components/ThirtyDays'));
const ThirtyToNinetyDays = React.lazy(() => import('../components/ThirtyToNinetyDays'));
const NinetyToOneEightyDays = React.lazy(() => import('../components/NinetyToOneEightyDays'));
const SixToNineMonths = React.lazy(() => import('../components/SixToNineMonths'));
const NineToTwelveMonths = React.lazy(() => import('../components/NineToTwelveMonths'));
const TwelveToEighteenMonths = React.lazy(() => import('../components/TwelveToEighteenMonths'));
const EighteenToTwentyFourMonths = React.lazy(() => import('../components/EighteenToTwentyFourMonths.tsx'));
const LongAfterETS = React.lazy(() => import('../components/LongAfterETS'));

// Fallback loading spinner or placeholder
const LoadingFallback = () => <div>Loading...</div>;

export default function MilitaryTransitionGuide(props: { disableCustomTheme?: boolean }) {
  // Maintain open state for multiple accordions (optional)
  const [expanded, setExpanded] = useState<string | false>(false);

  const handleAccordionChange = (panel: string) => (_: React.SyntheticEvent, isExpanded: boolean) => {
    setExpanded(isExpanded ? panel : false);
  };
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

        {/* Suspense-wrapped lazy-loaded components */}
        <Accordion expanded={expanded === 'panel1'} onChange={handleAccordionChange('panel1')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>18 to 24 Months before ETS</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <EighteenToTwentyFourMonths />
            </Suspense>
          </AccordionDetails>
        </Accordion>

        <Accordion expanded={expanded === 'panel2'} onChange={handleAccordionChange('panel2')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>12 to 18 Months before ETS</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <TwelveToEighteenMonths />
            </Suspense>
          </AccordionDetails>
        </Accordion>

        <Accordion expanded={expanded === 'panel3'} onChange={handleAccordionChange('panel3')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>12 to 9 Months before ETS</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <NineToTwelveMonths />
            </Suspense>
          </AccordionDetails>
        </Accordion>

        <Accordion expanded={expanded === 'panel4'} onChange={handleAccordionChange('panel4')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>6 to 9 Months before ETS</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <SixToNineMonths />
            </Suspense>
          </AccordionDetails>
        </Accordion>

        <Accordion expanded={expanded === 'panel5'} onChange={handleAccordionChange('panel5')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>90 to 180 days before ETS</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <NinetyToOneEightyDays />
            </Suspense>
          </AccordionDetails>
        </Accordion>

        <Accordion expanded={expanded === 'panel6'} onChange={handleAccordionChange('panel6')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>30 to 90 days before ETS</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <ThirtyToNinetyDays />
            </Suspense>
          </AccordionDetails>
        </Accordion>

        <Accordion
          expanded={expanded === 'panel7'}
          onChange={handleAccordionChange('panel7')}
          sx={{ width: '100%' }} // Full width style applied
        >
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>10 to 30 days before ETS</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <ThirtyDays />
            </Suspense>
          </AccordionDetails>
        </Accordion>

        <Accordion expanded={expanded === 'panel8'} onChange={handleAccordionChange('panel8')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>0 to 10 days before ETS</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <ZeroToTenDays />
            </Suspense>
          </AccordionDetails>
        </Accordion>

        <Accordion expanded={expanded === 'panel9'} onChange={handleAccordionChange('panel9')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>Terminal leve</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <TerminalLeave />
            </Suspense>
          </AccordionDetails>
        </Accordion>

        <Accordion expanded={expanded === 'panel10'} onChange={handleAccordionChange('panel10')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>0 to 30 days after ETS</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <ZeroToThirtyDaysPostETS />
            </Suspense>
          </AccordionDetails>
        </Accordion>

        <Accordion expanded={expanded === 'panel11'} onChange={handleAccordionChange('panel11')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>Long after ETS</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <LongAfterETS />
            </Suspense>{' '}
          </AccordionDetails>
        </Accordion>
      </Container>
    </AppTheme>
  );
}
