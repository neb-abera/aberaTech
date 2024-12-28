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

interface ZeroToThirtyDays {
  disableCustomTheme?: boolean; // Existing prop
  sx?: SxProps<Theme>; // New prop for Box styling
  sx1?: SxProps<Theme>; // If you're using sx1 for inner Boxes
}

export default function ZeroToThirtyDays(props: ZeroToThirtyDays) {
  return (
    <Box sx={props.sx}>
      <Typography variant="h2" component="h2" sx={{ marginBottom: 2, textAlign: 'center' }}>
        <strong>0-30 days post ETS</strong>
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
              <Box
                component="a"
                href="https://myarmybenefits.us.army.mil/Benefit-Library/Federal-Benefits/Unemployment-Compensation"
                target="_blank"
                rel="noopener noreferrer"
                sx={{
                  textDecoration: 'none',
                  color: 'inherit',
                  display: 'block'
                }}
              >
                <Typography variant="h4" sx={{ fontWeight: 'bold', marginBottom: 1 }}>
                  Unemployment Compensation
                </Typography>
                <Typography sx={{ marginBottom: 1 }}>
                  If you aren't gainfully employed, apply for unemployment at your local agency as
                  soon as possible. Unemployment is administered through your state.Unemployment
                  Compensation for Ex-Service members (UCX) provides unemployment benefits to
                  eligible workers who become unemployed through no fault of their own, and meet
                  certain other eligibility requirements, such as being able to work, available for
                  suitable full-time work, and actively seeking work.
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  myarmybenefits.us.army.mil
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
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                Your last Army paycheck will be delayed about 2-3 weeks from its usual payday.
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
                If you signed up for Reserves/Guard, make sure to do the following:
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                <strong>
                  Sign into your unit within 30 days of your ETS, get a new CAC from a{' '}
                  <Link to="https://idco.dmdc.osd.mil/idco/" target="_blank" rel="noopener">
                    DEERS office near you
                  </Link>
                  , and sign up for{' '}
                  <Link to="https://www.tricare.mil/TRS" target="_blank" rel="noopener">
                    Tricare Reserve Select
                  </Link>
                  .
                </strong>{' '}
                You have to do it in that order according to the Fort Moore Reserve officer
                counselor.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                Once you "hit" your Army Reserve unit's books ensure that you inprocess, and request
                a pay action and an Army Reserve DFAS account. Once you have your pay setup through
                the USAR it'll show up in a dropdown menu on myPay.
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
                You have{' '}
                <Link
                  to="https://www.tricare.mil/LifeEvents/Separating"
                  target="_blank"
                  rel="noopener"
                >
                  transitional healthcare programs
                </Link>{' '}
                for about 180 days after ETS. The personnel at the ETS brief said you need to apply
                for these immediately after ETS. However, the website makes it seem like it is
                applied automatically. It happened "automatically" for me and I was able to verify
                it on milconnect. Do your own research and make sure you have coverage before you
                need it.
              </Typography>
              <Typography>
                Retirees and Reserve/Guard have different healthcare benefits.
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
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                You need to send your PPM and per diem voucher back to your installation
                transportation office.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                These are two separate payments you'll receive. I put it off for about 5 months, but
                I believe you have up to a year past your ETS date. I got paid around $5k for PPM
                and about $500 for per diem.
              </Typography>
            </Box>
          </TimelineContent>
        </TimelineItem>
      </Timeline>
    </Box>
  );
}
