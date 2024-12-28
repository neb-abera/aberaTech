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

export default function SixToNineMonths() {
  return (
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
      <Typography variant="h2" component="h2" sx={{ marginBottom: 2, textAlign: 'center' }}>
        <strong>6-9 months</strong>
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
                Submit a request for your medical records at about the 7-8 month mark.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                This is usually done at medical records at the installation hospital. It may take 4-8 weeks for them to
                process your request. This is especially true for the busy summer move cycle.
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
                You should have an idea of prospective jobs and/or companies by this point.{' '}
                <strong>
                  Start targeting specific companies and job titles to build a network where you want to land.
                </strong>
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                You can{' '}
                <strong>
                  use Edge/Chrome add-ons like{' '}
                  <Link
                    to="https://microsoftedge.microsoft.com/addons/detail/hunter-email-finder-ext/dmgcgojogkfomifjfeeafajhmgilkofk"
                    target="_blank"
                    rel="noopener"
                  >
                    Hunter
                  </Link>{' '}
                  to get the contact info for leaders on a company's webpage.
                </strong>
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                <strong>
                  Get a joint email/calendar manager like Microsoft Outlook, Google Calendar + Gmail, or a meeting
                  manager like{' '}
                  <Link to="https://calendly.com/" target="_blank" rel="noopener">
                    Calendly{' '}
                  </Link>
                  . Ensure you synch your accounts with video conferencing sites like Zoom.
                </strong>{' '}
                for a seamless virtual meeting experience.
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
                Schedule to turn in your CIF equipment prior to leaving for CSP.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                Usually, CIF will require an early turn-in memo signed by your commander to do this (they usually keep
                example memos on site). If you are missing anything during your first turn-in, you can find the item at
                a local surplus store and then do a second turn-in when you get your installation clearing papers.
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
                If it makes financial sense, you can{' '}
                <Link
                  to="https://www.military.com/sites/default/files/2017-09/pcs-guide-2017.pdf"
                  target="_blank"
                  rel="noopener"
                >
                  make coordinations to move
                </Link>{' '}
                prior to your CSP.{' '}
                <strong>
                  You need to move on or after the publication date on your orders or transportation might not pay for
                  your move.
                </strong>
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                You can use the{' '}
                <Link
                  to="https://www.militaryonesource.mil/military-life-cycle/deployment/preparing-for-deployment/military-clause-terminate-your-lease-due-to-deployment-or-pcs/#:~:text=Under%20the%20SCRA%2C%20to%20end,copy%20of%20your%20military%20orders."
                  target="_blank"
                  rel="noopener"
                >
                  Servicemembers Civil Relief Act
                </Link>{' '}
                to break leases. It is customary, but not a requirement, to give a 30-day notice.{' '}
                <Link
                  to="https://www.29palms.marines.mil/Portals/56/Docs/SJA/SCRA%20-%20Letter%20for%20Residential%20Lease.pdf"
                  target="_blank"
                  rel="noopener"
                >
                  Here is an example template.
                </Link>
              </Typography>
            </Box>
          </TimelineContent>
        </TimelineItem>
      </Timeline>
    </Box>
  );
}
