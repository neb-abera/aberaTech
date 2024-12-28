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
        <strong>Terminal leave and ETS</strong>
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
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>Review your DD214 for accuracy.</Typography>
              <Typography sx={{ marginBottom: 2 }}>
                Make sure your ETS address is correct. Bring copies of awards, school records, and an SRB/ORB. Correct
                everything on the spot and do not leave until it is correct. The civilians are generally helpful. There
                are benefits tied to how long you served, the character of your service, and what you did while you were
                in. If you claim you did something, and it isn't on the DD214, then that's tough luck for you.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                If you leave, and your DD214 requires corrections, you will have to{' '}
                <Link
                  to="https://www.hrc.army.mil/content/Correction%20to%20Veterans%20Military%20Records"
                  target="_blank"
                  rel="noopener"
                >
                  go through HRC to make corrections
                </Link>
                , and it will be much more painful.
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
                Store your DD214, and all other important documents, in a safe location.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                Optimally, in a fire and water-resistant place. Make digital copies and email them to yourself.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                <Link
                  to="https://dd214direct.com/whats-the-difference-between-a-member-4-copy-and-service-2-copy-dd214/"
                  target="_blank"
                  rel="noopener"
                >
                  The member 4 is used at government agencies when applying for benefits. Member 1 is for civilian
                  organizations.
                </Link>
              </Typography>
              <Typography>
                <strong>Upload your DD214 to the VA.</strong> It will go in as a supporting document to your VA claim.
              </Typography>
            </Box>
          </TimelineContent>
        </TimelineItem>
      </Timeline>
    </Box>
  );
}
