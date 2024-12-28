import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Timeline from '@mui/lab/Timeline';
import TimelineItem, { timelineItemClasses } from '@mui/lab/TimelineItem';
import TimelineSeparator from '@mui/lab/TimelineSeparator';
import TimelineDot from '@mui/lab/TimelineDot';
import CircleIcon from '@mui/icons-material/Circle';
import TimelineConnector from '@mui/lab/TimelineConnector';
import TimelineContent from '@mui/lab/TimelineContent';

export default function TerminalLeave() {
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
        <strong>30-90 days</strong>
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
                At 30 days out apply for your installation clearance papers through your BN S1.
              </Typography>
              <Typography>
                Fort Moore G1 won't process installation clearance paper requests until you've completed CSP.
              </Typography>
            </Box>
          </TimelineContent>
        </TimelineItem>
      </Timeline>
    </Box>
  );
}
