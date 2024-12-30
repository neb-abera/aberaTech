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

export default function ThirtyToNinetyDays() {
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
              <Typography sx={{ marginBottom: 2 }}>
                If you're an Army Reservist then you should submit a PAR on IPPS-A for separation orders at least 90
                days out. If you've met your contractual obligation, but still have an IRR obligation, look at the USAR
                PAG for T-1-E-2. You should also turn-in your equipment to a Supply soldier, maintain a copy of your
                turn-in, and submit RSTs for any Battle Assemblies you want to be excused from ~45 days in advance.{' '}
                <strong>You have an obligation to attend training unless you have orders or an RST.</strong>
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                If you're Active Duty submit your request for orders through your BN S1 who will then send it up to get
                processed by your higher headquarters' G1 equivalent. At Fort Moore, you can{' '}
                <strong>request your orders 90 days</strong> out from your terminal leave or ETS date.{' '}
                <strong>You can also request your orders earlier for extenuating circumstances</strong>. Your orders are
                one of the key documents that will prevent you from moving forward with clearing and moving.
              </Typography>
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>STORY ABOUT HOW I LEARNED THIS:</Typography>
              <Typography sx={{ marginBottom: 2 }}>
                I was paying for my apartment outside of Fort Moore AND my wife's apartment in Chicago while she was in
                residency. It made financial sense to move to Chicago during my CSP so I requested my orders early. I
                had already briefed this as part of my transition plan to my leadership.{' '}
                <strong>Transportation may not pay you if you move before the publication date on your orders.</strong>{' '}
                My orders had a publication date before I moved to Chicago so I was able to get paid for the move.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                You should begin applying to jobs, even if that is not your goal or you already have something lined up.
                You will gain valuable experience during interviews. Ask for feedback after an interview. Do an AAR with
                someone you know after every interview.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                You can use sites like{' '}
                <Link to="https://www.glassdoor.com/index.htm" target="_blank" rel="noopener">
                  Glassdoor
                </Link>{' '}
                and{' '}
                <Link to="https://www.teamblind.com/" target="_blank" rel="noopener">
                  Blind
                </Link>{' '}
                to get inside info on things like the culture of a company, or what to expect for compensation.
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
                sx={{
                  display: 'flex',
                  justifyContent: 'center',
                  marginBottom: 2
                }}
              >
                <iframe
                  title="Interview Tips"
                  src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/networking/Interview_Tips.pdf&embedded=true"
                  style={{
                    width: '100%',
                    maxWidth: '800px', // Adjust width as needed
                    height: '741px', // Adjust height as needed
                    borderRadius: '8px',
                    boxShadow: '0px 4px 10px rgba(0, 0, 0, 0.1)',
                    border: 'none', // Replaces frameBorder="0"
                    overflow: 'hidden' // Replaces scrolling="no"
                  }}
                />
              </Box>
              <Typography sx={{ marginBottom: 2 }}>
                You should also <strong>begin interviewing for jobs</strong> at this time. Most companies don't want to
                entertain prospective applicants until they're 30-90 days out from their availability date.
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
                If you haven't already, apply for the GI Bill and VET TEC.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                It takes about 30 days to approve and receive your Certificate of Eligibility (COE), so it's better to
                have it and not need it than to need it and not have it.
              </Typography>
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>STORY ABOUT HOW I LEARNED THIS:</Typography>
              <Typography sx={{ marginBottom: 2 }}>
                I found out about this too late. I missed out on being able to do VET TEC while I was on terminal leave
                because I received the VA COE the weekend after the course deadline for registration. That cost me about
                a two-month delay in finding employment.
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
                If you chose to do VET TEC,{' '}
                <Link
                  to="https://www.coursereport.com/blog/which-coding-bootcamps-accept-vet-tec"
                  target="_blank"
                  rel="noopener"
                >
                  find which company offers what you’re interested in
                </Link>
                , complete the train-up, and take the entrance exam. It could take you 2 months to do the train-up and
                multiple attempts to pass the assessment, so plan accordingly.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                Look at all the different pros and cons of the companies.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                Going remote will give you less BAH than in person. But, going remote will allow you to be anywhere you
                want to be.
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
                If you haven't moved yet, 60-90 days out is the minimum amount of time you need to start planning for
                your move. Schedule a transportation brief.{' '}
                <Link
                  to="https://www.military.com/sites/default/files/2017-09/pcs-guide-2017.pdf"
                  target="_blank"
                  rel="noopener"
                >
                  Here are some things to think about.
                </Link>
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
                to break leases.
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
                Apply for{' '}
                <Link to="https://breakline.org/" target="_blank" rel="noopener">
                  BreakLine
                </Link>{' '}
                . If you're doing a bootcamp then apply after you graduate from the bootcamp. Breakline is probably the
                best way to break into a tech company, even if you don’t have a tech background. It's an incredible
                program and they teach you interview/resume skills and introduce you to tech companies. Their mission is
                to get veterans, minorities, and women into high-paying jobs at tech companies.
              </Typography>
            </Box>
          </TimelineContent>
        </TimelineItem>
      </Timeline>
    </Box>
  );
}
