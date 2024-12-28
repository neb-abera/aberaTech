import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Timeline from '@mui/lab/Timeline';
import TimelineItem, { timelineItemClasses } from '@mui/lab/TimelineItem';
import TimelineSeparator from '@mui/lab/TimelineSeparator';
import TimelineDot from '@mui/lab/TimelineDot';
import CircleIcon from '@mui/icons-material/Circle';
import TimelineConnector from '@mui/lab/TimelineConnector';
import TimelineContent from '@mui/lab/TimelineContent';
import { Link } from 'react-router';
import { SxProps, Theme } from '@mui/system';

interface TerminalLeave {
  disableCustomTheme?: boolean; // Existing prop
  sx?: SxProps<Theme>; // New prop for Box styling
  sx1?: SxProps<Theme>; // If you're using sx1 for inner Boxes
}

export default function TerminalLeave(props: TerminalLeave) {
  return (
    <Box sx={props.sx}>
      <Typography variant="h2" component="h2" sx={{ marginBottom: 2, textAlign: 'center' }}>
        <strong>0-10 days</strong>
      </Typography>
      <Timeline
        sx={{
          [`& .${timelineItemClasses.root}:before`]: {
            flex: 0,
            padding: 0
          }
        }}
      >
        <TimelineItem>
          <TimelineSeparator>
            <TimelineDot color="primary">
              <CircleIcon />
            </TimelineDot>
            <TimelineConnector />
          </TimelineSeparator>
          <TimelineContent>
            <Box
              sx={(theme) => ({
                p: 2,
                border: `1px solid ${theme.palette.divider}`,
                backgroundColor: theme.palette.background.paper,
                color: theme.palette.text.primary,
                boxShadow: theme.shadows[1],
                width: '100%',
                marginBottom: 2,
                textAlign: 'left'
              })}
            >
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                Upload your Personally Procured Move (PPM) to{' '}
                <Link to="https://dps.move.mil/cust" target="_blank" rel="noopener">
                  DPS
                </Link>{' '}
                and submit your move packets to transportation.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                You can do this in person or via mail. <strong>Submit per diem request to finance.</strong> The
                instructions are on the packets you get when you clear those offices.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                You can calculate how much to expect from your PPM using calculators like the one next to this tile.{' '}
                <strong>You will get 95% of the cost to the government</strong> to pay professionals to move you. The
                Army will pay you what it'd cost to get you from your current duty station to your home of record at
                entry or the location you entered service.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                <strong>You need to get empty-weight and full-weight tickets to get paid for your move.</strong> They'll
                give you the finer details in your transportation brief. For example, do not include your personally
                owned vehicle as part of the net difference in weight.{' '}
                <strong>Do not try to defraud the system. If you weigh it, then you must move it.</strong>
              </Typography>
            </Box>
          </TimelineContent>
        </TimelineItem>
        <TimelineItem>
          <TimelineSeparator>
            <TimelineDot color="primary">
              <CircleIcon />
            </TimelineDot>
            <TimelineConnector />
          </TimelineSeparator>
          <TimelineContent>
            <Box
              sx={(theme) => ({
                p: 2,
                border: `1px solid ${theme.palette.divider}`,
                backgroundColor: theme.palette.background.paper,
                color: theme.palette.text.primary,
                boxShadow: theme.shadows[1],
                width: '100%',
                marginBottom: 2,
                textAlign: 'left'
              })}
            >
              <Box
                component="a"
                href="https://ditymovecalculator.net/"
                target="_blank"
                rel="noopener noreferrer"
                sx={{
                  textDecoration: 'none',
                  color: 'inherit',
                  display: 'block'
                }}
              >
                <Box
                  sx={{
                    display: 'flex',
                    justifyContent: 'center',
                    marginBottom: 2
                  }}
                >
                  <Box
                    component="img"
                    src="https://www.lduhtrp.net/image-8389227-12188911"
                    alt="DITY/Personally Procured Move Calculator - UPDATED FOR 2021"
                    sx={{
                      maxWidth: '300px',
                      width: '100%',
                      height: 'auto',
                      borderRadius: 2,
                      boxShadow: 3
                    }}
                  />
                </Box>
                <Typography variant="h4" sx={{ fontWeight: 'bold', marginBottom: 1 }}>
                  DITY/Personally Procured Move Calculator - UPDATED FOR 2021
                </Typography>
                <Typography sx={{ marginBottom: 1 }}>
                  A comprehensive DITY move calculator for PCS moves, allowing the user to view eligible allowances and
                  estimate expenses and profit.
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  ditymovecalculator.net
                </Typography>
              </Box>
            </Box>
          </TimelineContent>
        </TimelineItem>
        <TimelineItem>
          <TimelineSeparator>
            <TimelineDot color="primary">
              <CircleIcon />
            </TimelineDot>
            <TimelineConnector />
          </TimelineSeparator>
          <TimelineContent>
            <Box
              sx={(theme) => ({
                p: 2,
                border: `1px solid ${theme.palette.divider}`,
                backgroundColor: theme.palette.background.paper,
                color: theme.palette.text.primary,
                boxShadow: theme.shadows[1],
                width: '100%',
                marginBottom: 2,
                textAlign: 'left'
              })}
            >
              <Typography sx={{ marginBottom: 2 }}>
                Clear your installation and out-process the Army.{' '}
                <strong>
                  If you haven't done it already, you need to do your separation physical during this period.
                </strong>{' '}
                If it's been more than 30 days since your Phase 1 physical, you will need to go into your MEDPROS and
                add an update to your medical status as per the instructions on your SHPE packet.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                Go to{' '}
                <Link to="https://medpros.mods.army.mil/portal/" target="_blank" rel="noopener">
                  medpros
                </Link>
                , click on "periodic health assessments," go to the "SHPE" tab, and fill out a medical update (I believe
                it's the DD Form 2807-1).
              </Typography>
            </Box>
          </TimelineContent>
        </TimelineItem>
        <TimelineItem>
          <TimelineSeparator>
            <TimelineDot color="primary">
              <CircleIcon />
            </TimelineDot>
            <TimelineConnector />
          </TimelineSeparator>
          <TimelineContent>
            <Box
              sx={(theme) => ({
                p: 2,
                border: `1px solid ${theme.palette.divider}`,
                backgroundColor: theme.palette.background.paper,
                color: theme.palette.text.primary,
                boxShadow: theme.shadows[1],
                width: '100%',
                marginBottom: 2,
                textAlign: 'left'
              })}
            >
              <Typography sx={{ marginBottom: 2 }}>
                Make sure your final pay sheet at finance is accurate before you sign. For example, make sure that
                you're{' '}
                <Link
                  to="https://www.military.com/military-transition/personal-finances/should-you-sell-back-leave-or-take-terminal-leave.html"
                  target="_blank"
                  rel="noopener"
                >
                  selling the number of leave days
                </Link>{' '}
                you want.
              </Typography>
            </Box>
          </TimelineContent>
        </TimelineItem>
        <TimelineItem>
          <TimelineSeparator>
            <TimelineDot color="primary">
              <CircleIcon />
            </TimelineDot>
            <TimelineConnector />
          </TimelineSeparator>
          <TimelineContent>
            <Box
              sx={(theme) => ({
                p: 2,
                border: `1px solid ${theme.palette.divider}`,
                backgroundColor: theme.palette.background.paper,
                color: theme.palette.text.primary,
                boxShadow: theme.shadows[1],
                width: '100%',
                marginBottom: 2,
                textAlign: 'left'
              })}
            >
              <Typography sx={{ marginBottom: 2 }}>
                Don't forget that you'll need lodging and transportation if you've already moved away from your
                installation. Costco provides a{' '}
                <Link to="https://www.costcotravel.com/Rental-Cars" target="_blank" rel="noopener">
                  rental car discount
                </Link>
                . You can save money by staying with a friend or even staying in your car.
              </Typography>
            </Box>
          </TimelineContent>
        </TimelineItem>
      </Timeline>
    </Box>
  );
}
