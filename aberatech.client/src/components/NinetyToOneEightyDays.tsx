import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Timeline from '@mui/lab/Timeline';
import TimelineItem, { timelineItemClasses } from '@mui/lab/TimelineItem';
import TimelineSeparator from '@mui/lab/TimelineSeparator';
import TimelineDot from '@mui/lab/TimelineDot';
import CircleIcon from '@mui/icons-material/Circle';
import TimelineConnector from '@mui/lab/TimelineConnector';
import TimelineContent from '@mui/lab/TimelineContent';
import { CardMedia } from '@mui/material';
import ETSScheduleImage from '../assets/ETS_briefing_schedule.jpeg';
import G1Schedule from '../assets/g1_contact_info.png';
import { Link } from 'react-router';

export default function NinetyToOneEightyDays() {
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
        <strong>90-180 days</strong>
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
              <Box
                sx={{
                  display: 'flex',
                  justifyContent: 'center', // Center the image horizontally
                  marginBottom: 2 // Add spacing below the image
                }}
              >
                <CardMedia
                  component="img"
                  image={ETSScheduleImage}
                  alt="ETS Briefing Schedule"
                  sx={{
                    width: '100%',
                    maxWidth: 500, // Adjust width as needed
                    height: 'auto',
                    borderRadius: 2,
                    boxShadow: 3
                  }}
                />
              </Box>

              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                Attend your installation's ETS brief, if it is a requirement by your higher headquarters' G1, as soon as
                possible.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                Sometimes these are done online, and sometimes they are done in person. Once complete, you can send a
                request for orders packet with your ETS briefing certificate to G1 through your BN S1. It will take G1
                about 2 weeks to produce orders for you.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                At Fort Moore, you fall under the DHR, so look at the box to get info on speaking to G1.
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
                  width: '100%',
                  justifyContent: 'center',
                  marginBottom: 2
                }}
              >
                <CardMedia
                  component="img"
                  image={G1Schedule}
                  alt="Fort Moore G1 Contact Information"
                  sx={{
                    width: '100%',
                    maxWidth: 800,
                    height: 'auto',
                    borderRadius: 2,
                    boxShadow: 3
                  }}
                />
              </Box>
              <Typography sx={{ marginBottom: 2 }}>
                Reach out to your G1 separations sections to get the most up-to-date information on separation
                requirements. For Fort Moore, you can find their contact info{' '}
                <Link to="https://www.moore.army.mil/Garrison/DHR/Contacts.html" target="_blank" rel="noopener">
                  here
                </Link>
                .
              </Typography>
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>STORY ABOUT HOW I LEARNED THIS:</Typography>
              <Typography sx={{ marginBottom: 2 }}>
                I was "waiting" for my orders for months without any believable updates from S1. I went to my unit's G1,
                DHR at Fort Moore, and{' '}
                <strong>
                  I found out that my packet was never submitted correctly by my BN S1 so they weren't going to process
                  it
                </strong>
                . That's because <strong>they started requiring in-person ETS briefs THAT MONTH</strong>, after a
                two-year break due to COVID, and I wouldn't get my orders unless I attended their brief and received a
                certificate to put in my request for orders packet. This info had not been communicated down to the
                soldier level by BN S1s.{' '}
                <strong>
                  I had to drive 4 hours from a field training exercise to do the ETS brief at Fort Moore, and then
                  drive another 4 hours back to the field training exercise.
                </strong>
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
                Set up a{' '}
                <Link
                  to="https://www.va.gov/resources/how-to-get-a-premium-ds-logon-account-online/"
                  target="_blank"
                  rel="noopener"
                >
                  DS logon
                </Link>{' '}
                and ensure you can access the VA and{' '}
                <Link to="https://mypay.dfas.mil/#/" target="_blank" rel="noopener">
                  myPay
                </Link>{' '}
                websites.
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
                Identify any vocational training you're interested in and apply for acceptance in the program. Remember,
                you need to apply for VA benefits <strong>AND</strong> apply for acceptance from the company.
              </Typography>
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                For VET TEC, find which company offers what you’re interested in, complete the train-up programs, then
                apply for acceptance.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                These are the{' '}
                <Link
                  to="https://www.benefits.va.gov/GIBILL/FGIB/VetTecTrainingProviders.asp"
                  target="_blank"
                  rel="noopener"
                >
                  preferred VET TEC training providers
                </Link>
                .
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                Look at the benefits of an in-person course vs remote. You will get less BAH for remote vs in-person
                attendance. Let your social network know you'll be unavailable during the BootCamp.{' '}
                <strong>To start the program, you need to be on terminal leave, DOD Skillbridge, or have ETSed.</strong>
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
                Apply for the{' '}
                <Link
                  to="https://www.va.gov/disability/how-to-file-claim/when-to-file/pre-discharge-claim/"
                  target="_blank"
                  rel="noopener"
                >
                  VA's BDD
                </Link>
                .
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                Upload your medical record to your claim when you get it.
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
                Buy a computer if you don't have one yet.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                You'll need it to access VA resources, network, work from home, etc. There are many resources out there
                on desktop vs laptop and{' '}
                <Link to="https://www.nytimes.com/wirecutter/reviews/best-laptops/" target="_blank" rel="noopener">
                  PC vs mac
                </Link>
                .
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                A desktop is more powerful. You can save even more money by{' '}
                <Link to="https://www.reddit.com/r/buildapc/wiki/index" target="_blank" rel="noopener">
                  building your own desktop
                </Link>
                . A laptop is less powerful, and usually more expensive per unit of computing power, but is more
                portable. I recommend Apple MacBooks, especially a MacBook Pro, if you want a laptop. Stay away from
                MacBook Airs if you have any plans to use it for more than typical web browsing and streaming. MacBooks
                are more expensive than PC laptops but include better integration with cell phones, a more secure
                environment, and are a requirement if you want to do software development.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                You can also start building a{' '}
                <Link
                  to="https://www.nytimes.com/wirecutter/reviews/the-best-home-office-furniture-and-supplies/"
                  target="_blank"
                  rel="noopener"
                >
                  home office
                </Link>
                . This is especially important if you want to pursue a work-from-home or remote position.
              </Typography>
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                If you plan to do a VET TEC here are the usual computer requirements:
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                Hack Reactor's computer requirements from their{' '}
                <Link to="https://www.hackreactor.com/faq" target="_blank" rel="noopener">
                  FAQ
                </Link>
                :
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                Our curriculum, like the rest of the software engineering industry, is heavily dependent on open-source
                software, which traditionally has not worked very well on Windows platforms. For that reason, a computer
                running macOS or Linux is greatly preferred. Windows users are required to either switch to a Mac or
                Linux computer (preferred), or install Windows Subsystem for Linux 2 (WSL2). Windows users should expect
                to spend extra time setting up WSL, installing an Ubuntu dual boot configuration, and/or running Linux
                via a virtual machine (via Virtual Box). Please note that the minimum specs below may not be suitable
                for running a virtual machine.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                You should not use a computer that is actively shared with another person (especially with another
                software developer) since you will be making system-level configuration changes, and possibly deleting
                information that will affect those users.
              </Typography>
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                Macs with Apple Silicon M1 are permitted in the immersive.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                <strong>Processor:</strong> Intel Dual-Core i5 or equivalent (minimum)
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                <strong>Memory:</strong> 8 GB RAM (minimum), 16 GB RAM (recommended)
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                <strong>Storage:</strong> 50 GB available space (minimum)
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                <strong>Peripherals:</strong> Working Webcam
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                <strong>Operating System:</strong>
              </Typography>
              <Typography component="ul" sx={{ marginBottom: 2, paddingLeft: 3 }}>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  <strong>Highly Recommended:</strong> Mac OS X (v10.14 minimum,{' '}
                  <Link to="https://support.apple.com/en-us/HT211683" target="_blank" rel="noopener">
                    LTS
                  </Link>{' '}
                  recommended)
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  <strong>Acceptable:</strong>{' '}
                  <Link
                    to="https://docs.microsoft.com/en-us/windows/wsl/install-win10#step-2---check-requirements-for-running-wsl-2"
                    target="_blank"
                    rel="noopener"
                  >
                    Windows 10 compatible with WSL 2
                  </Link>
                  - We do not provide full instructional support for Windows users. Our staff can assist with
                  WSL2/Ubuntu-related issues but may be unable to troubleshoot Windows-specific issues.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  <strong>Acceptable Alternative:</strong> Ubuntu Linux (LTS minimum)
                </Typography>
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                Note that Zoom and other communication apps, webcams, and microphones may be buggy on Ubuntu. This is
                outside of the control of staff as they cannot support debugging these issues.
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
                Look up job conferences to attend in the upcoming months. Don't forget that you can go to professional
                conventions or company-hosted conventions to network and find employment (e.g., DEFCON, Blue Team CON).
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
                href="https://success.recruitmilitary.com/events/schedule"
                target="_blank"
                rel="noopener noreferrer ugc nofollow"
                sx={{
                  textDecoration: 'none',
                  display: 'block',
                  color: 'inherit'
                }}
              >
                <Box
                  sx={{
                    display: 'flex',
                    justifyContent: 'center', // Only center the image horizontally
                    marginBottom: 2
                  }}
                >
                  <Box
                    component="img"
                    src="https://assets.recruitmilitary.com/images/rm_logo_new_large.png"
                    alt="Veteran Job Fairs"
                    sx={{
                      maxWidth: 400, // Adjust as needed
                      width: '100%',
                      height: 'auto',
                      borderRadius: 2,
                      boxShadow: 3
                    }}
                  />
                </Box>
                <Typography variant="h4" sx={{ marginBottom: 1, fontWeight: 'bold' }}>
                  Veteran Job Fairs
                </Typography>
                <Typography sx={{ marginBottom: 1 }}>
                  Network with military-friendly companies interested in hiring you.
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  success.recruitmilitary.com
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
              <Box
                component="a"
                href="https://www.hiringourheroes.org/career-services/hiring-events/"
                target="_blank"
                rel="noopener noreferrer ugc nofollow"
                sx={{
                  textDecoration: 'none',
                  display: 'block',
                  color: 'inherit'
                }}
              >
                <Box
                  sx={{
                    display: 'flex',
                    justifyContent: 'center', // Center the image
                    marginBottom: 2
                  }}
                >
                  <Box
                    component="img"
                    src="https://www.hiringourheroes.org/wp-content/uploads/2022/12/homepage-featured-img-december-2022-1200x800-1.jpg"
                    alt="Hiring Events"
                    sx={{
                      maxWidth: 800, // Adjust as needed
                      width: '100%',
                      height: 'auto',
                      borderRadius: 2,
                      boxShadow: 3
                    }}
                  />
                </Box>
                <Typography variant="h4" sx={{ marginBottom: 1, fontWeight: 'bold' }}>
                  Hiring Events
                </Typography>
                <Typography sx={{ marginBottom: 1 }}>
                  Our hiring events connect military community job seekers from around the globe with American
                  businesses hiring for local, national, and remote opportunities.
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  www.hiringourheroes.org
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
                If you're interested in the Reserve/Guard, make sure to explore available MOS, benefits, bonuses, and
                locations you're interested in. The reserve career counselors can send you a list to look through. You
                can also find the list of open positions on{' '}
                <Link to="https://www.milsuite.mil/book/community/spaces/apf/s1net" target="_blank" rel="noopener">
                  milsuite in the S1 Net
                </Link>
                .
              </Typography>
            </Box>
          </TimelineContent>
        </TimelineItem>
      </Timeline>
    </Box>
  );
}
