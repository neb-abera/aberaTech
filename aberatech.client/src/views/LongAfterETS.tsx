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

interface LongAfterETSProps {
  disableCustomTheme?: boolean; // Existing prop
  sx?: SxProps<Theme>; // New prop for Box styling
  sx1?: SxProps<Theme>; // If you're using sx1 for inner Boxes
}

export default function LongAfterETS(props: LongAfterETSProps) {
  return (
    <Box sx={props.sx}>
      <Typography variant="h2" component="h2" sx={{ marginBottom: 2, textAlign: 'center' }}>
        <strong>Long after you ETS</strong>
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
            <Box sx={props.sx1}>
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                Your first post-transition job might not be what you wanted, or how you thought it was going to be. It's
                okay to job-hop.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                Companies generally are more likely to pay higher compensation packages to new hires than they are to
                give internal employees raises. Therefore,{' '}
                <strong>job hopping is the easiest way to get a substantial raise</strong>.
              </Typography>
              <Typography>
                If you can't meet the initial requirements to get hired into a company for your target job, it is likely
                easier to transfer to that role as an internal employee. For example, getting a software engineer role
                as an outside hire at Amazon is rigorous, but moving to another team as an employee is easier.
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
            <Box sx={props.sx1}>
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                Attend conferences for your chosen profession.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                These are great places to network and learn about industry trends.
              </Typography>
              <Typography>
                <Link
                  to="https://www.aventri.com/strategy/top-conferences-to-attend-this-year"
                  target="_blank"
                  rel="noopener"
                >
                  Top Conferences to Attend in 2022 by Industries | Aventri
                </Link>
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
            <Box sx={props.sx1}>
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                You have benefits that last long after you ETS.
              </Typography>
              <Typography>
                You learn about them during TAP. For example, you can use VET TEC a second time if you need help
                transitioning to another job again as long as it's 18 months between courses.
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
            <Box sx={props.sx1}>
              <Box
                component="a"
                href="https://nvf.org/veteran-service-officers/"
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
                    src="https://nvf.org/wp-content/uploads/2015/03/veteran-service-officers.jpg"
                    alt="Veteran Service Officers - List of Where to Find Help"
                    sx={{
                      maxWidth: '450px',
                      width: '100%',
                      height: 'auto',
                      borderRadius: 2,
                      boxShadow: 3
                    }}
                  />
                </Box>
                <Typography variant="h4" sx={{ fontWeight: 'bold', marginBottom: 1 }}>
                  Veteran Service Officers - List of Where to Find Help
                </Typography>
                <Typography sx={{ marginBottom: 1 }}>
                  Veteran Service Officers help you navigate the VA. They help with gathering information necessary to
                  support a claim through the VA.
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  nvf.org
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
            <Box sx={props.sx1}>
              <Box
                component="a"
                href="https://www.va.gov/disability/get-help-filing-claim/"
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
                    src="https://www.va.gov/img/design/logo/va-og-image.png"
                    alt="Get help from a VA accredited representative or VSO | Veterans Affairs"
                    sx={{
                      maxWidth: '450px',
                      width: '100%',
                      height: 'auto',
                      borderRadius: 2,
                      boxShadow: 3
                    }}
                  />
                </Box>
                <Typography variant="h4" sx={{ fontWeight: 'bold', marginBottom: 1 }}>
                  Get help from a VA accredited representative or VSO | Veterans Affairs
                </Typography>
                <Typography sx={{ marginBottom: 1 }}>
                  An accredited attorney, claims agent, or Veterans Service Organization (VSO) representative can help
                  you file a claim or request a decision review. Learn how to find and appoint one of these types of
                  accredited representatives to help you.
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  www.va.gov
                </Typography>
              </Box>
              <Typography sx={{ marginTop: 2 }}>
                You may need to fight for benefits. Don't hesitate to put up a fight to get the benefits you deserve.
                The VA is a very bureaucratic system, but you have government and non-governmental support. You can
                reach out to organizations like{' '}
                <Link to="https://www.dav.org/veterans/" target="_blank" rel="noopener">
                  DAV
                </Link>{' '}
                for help while working on your disability claim with the VA.
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
            <Box sx={props.sx1}>
              <Typography>
                If you lose your DD214, you can request a copy from{' '}
                <Link to="https://www.va.gov/records/get-military-service-records/" target="_blank" rel="noopener">
                  here
                </Link>
                .
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
            <Box sx={props.sx1}>
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>Give back to veterans.</Typography>
              <Typography>You got a lot of help along the way. Do what you can to give back.</Typography>
            </Box>
          </TimelineContent>
        </TimelineItem>
      </Timeline>
    </Box>
  );
}
