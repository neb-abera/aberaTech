import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import TimelineSeparatorCustom from './TimelineSeparatorCustom.tsx';
import Timeline from '@mui/lab/Timeline';
import TimelineItem, { timelineItemClasses } from '@mui/lab/TimelineItem';
import TimelineSeparator from '@mui/lab/TimelineSeparator';
import TimelineDot from '@mui/lab/TimelineDot';
import CircleIcon from '@mui/icons-material/Circle';
import TimelineConnector from '@mui/lab/TimelineConnector';
import TimelineContent from '@mui/lab/TimelineContent';
import { Link } from 'react-router';
import { CardMedia } from '@mui/material';

export default function EighteenToTwentyFourMonths() {
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
        <strong>18-24 months</strong>
      </Typography>
      <TimelineSeparatorCustom />
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
                width: '100%'
              })}
            >
              <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                <strong>The earlier you can complete the Transition Assistance Program (TAP) the better</strong>. I
                recommend completing TAP as early as possible. That's because not having a completed DD Form 2648 will
                prevent you from being able to schedule a Career Skills Program (CSP) internship.
              </Typography>
              <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                <strong>
                  Retiring soldiers can begin TAP 24 months out from their ETS. Everyone else can begin TAP at 18 months
                  from their prospective ETS date
                </strong>
                . You start TAP by going to your installation's TAP center, registering for the program, and getting a
                counselor assigned.
              </Typography>
              <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                You can find the information for Fort Moore at{' '}
                <Link to="https://home.army.mil/moore/About/garrison/DHR/TAP" target="_blank" rel="noopener">
                  Fort Moore | Army Transition Assistance Program (TAP).
                </Link>
              </Typography>
              <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                <strong>
                  Ensure you have access to your TSP account, you are getting the Army matching contributions, and move
                  your retirement to a fund you like
                </strong>
                . The general rule of thumb is if you're young, or don't need access to your retirement, invest it in
                "riskier" funds like the S fund. If you're older, or more risk-averse, choose "safer" funds.
              </Typography>
              <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                <strong>STORY ABOUT HOW I LEARNED THIS:</strong> I began TAP at around the 11-month mark, told my TAP
                counselor I wanted to do a CSP in Chicago, and then my TAP counselor spread out my appointments all the
                way out until the 6-month mark. My TAP counselor didn't seem very familiar with CSP but said we would
                discuss at a future appointment. It seemed reasonable at the time since I didn't know any better. At
                around the 9-month mark, I reached out to my installation's CSP coordinator but she said she wouldn't
                give me the CSP documents until I was TAP complete. I then had to go back to my TAP counselor and tell
                her I needed her to re-schedule all of my appointments so that I could be done as quickly as possible.
                When I completed TAP at about the 8-month mark, I found out that I had to get O6 approval for my CSP in
                Chicago since it was outside of a 50-mile radius from Fort Moore. Fort Moore also didn't have any CSP
                options available for my timeline (remote or in-person). I had to personally reach out to countless
                companies to create a CSP, and then get JAG and Installation Command approval. I even flew out to a job
                conference in DC to approach a Northrop Grumman recruiter that I'd lost contact with. I got the approval
                from Northrop Grumman too late for JAG approval and I lost out on my dream CSP. That didn't end up
                mattering though because my packet had actually not been sent up to the approval authority by the
                bureaucracy. But that's another story. I had great Company, Battalion, and Brigade commanders, along
                with the support of my unit, so everything eventually worked out. You might not, so you will need to
                advocate for yourself and understand requirements and timelines in-depth.
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
              <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                <strong>
                  The most important skill to have is the ability to search for the answer to your questions
                </strong>
              </Typography>
              <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                Learn how to use google search to find what you need. I like to google to search Reddit and quora for
                the answers I'm looking for. Just add "Reddit" or "quora" to your search terms, and try to look for the
                most recent posts with the most discussion. The most upvoted answers are usually the best. The
                dissenting opinions are also important to understand.
              </Typography>
              <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                "best ______ Reddit"
                <br />
                "best ______ quora"
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
              <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                <strong>Reach out to people after you have done your homework</strong>
              </Typography>
              <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                Use content creators on YouTube, TikTok, and Reddit to help your transition. Just use what you're
                interested in as a search term and they will feed you the people that are related to that topic.
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
              <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                Use Credential Assistance (CA) to pay for relevant education. CA does not incur a service obligation for
                any soldier, including officers.{' '}
                <Link to="https://home.army.mil/moore/About/garrison/DHR/TAP" target="_blank" rel="noopener">
                  Updates to Army Credentialing Assistance Policy
                </Link>{' '}
                Paragraph 10a. "The service obligation for the CA program has been removed per reference 1a, dated 13
                October 2021."
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
                marginBottom: 2
              })}
            >
              <CardMedia
                component="iframe"
                title="test"
                src="https://www.youtube.com/embed/v1ybHS-Zmlg"
                sx={{ width: '100%', height: 'auto', aspectRatio: '16/9', marginBottom: 2 }}
              />
              <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                This is one of the intro videos they show you. You can go to this website to see an example of some of
                the classes you will take{' '}
                <Link to="https://www.armytap.army.mil/" target="_blank" rel="noopener">
                  TAP Online
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
              <Typography sx={{ marginBottom: 2 }}>
                <span style={{ fontWeight: 'bold' }}>
                  You need to ensure that all of your illnesses and injuries are appropriately recorded in your medical
                  record.
                </span>{' '}
                You also need to follow up with anything that's happened earlier in your career (even if you let it go
                untreated). If you don't get follow-up treatments for something, later the VA will assume it didn't
                bother you so therefore it wasn't a problem.
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                <span style={{ fontWeight: 'bold' }}>
                  The best way to complete the VA disability process is through the Benefits Delivery at Discharge (BDD)
                  program when you're between 180 and 90 days from your ETS.
                </span>{' '}
                The benefit of doing a BDD is that it will be substantially easier to get your benefits paid upon ETS.
                You will upload your medical record (which you will have to request at about the 7-8 month mark),
                complete a physical, register a VA account, and initiate an online BDD claim.
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                <span style={{ fontWeight: 'bold' }}>
                  DO NOT PAY ANYBODY FOR ANY HELP WITH VA DISABILITY SERVICES. THERE ARE A LOT OF SCAM ARTISTS WHO WILL
                  TAKE YOUR MONEY
                </span>{' '}
                There are so many free resources that can help you. TAP has a good VA disability class, YouTube has some
                popular disability content creators, and there are plenty of resources online. The VA has dedicated
                people to help you, veteran organizations like DAV provide free help, and I'm sure you know folks who
                have gone through the process that can help. Do not do it alone, but also don't pay anyone to help.
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
                You need to start thinking about important life-altering questions
              </Typography>

              <Typography component="ol" sx={{ marginBottom: 2, paddingLeft: 3 }}>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  What date do you want to leave the Army? (ETS date)
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  What do you want to do after you leave the Army? Do you want to start a different career, pursue
                  education, start your own company, retire etc?
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  What are your priorities for your transition? How important is moving to a specific location to you vs
                  getting your dream job vs maximizing total compensation etc?
                </Typography>
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                Here are some books to help you think about what the next stage of your life can look like
              </Typography>

              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                Immerse yourself in transition books, videos, and surround yourself with people willing to answer your
                questions
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                Here are the most highly recommended books from West Point's Association of Graduates. I recommend
                reading them all.
              </Typography>

              <Typography sx={{ marginBottom: 1 }}>
                <Link
                  to="https://www.amazon.com/Designing-Your-Life-Well-Lived-Joyful/dp/1101875321"
                  target="_blank"
                  rel="noopener"
                >
                  Designing Your Life
                </Link>{' '}
                by Burnett and Evans
              </Typography>

              <Typography sx={{ marginBottom: 1 }}>
                <Link
                  to="https://www.amazon.com/s?k=What+Color+is+Your+Parachute&amp;i=stripbooks&amp;crid=3RMCTPGJOX0WG&amp;sprefix=what+color+is+your+parachute%2Cstripbooks%2C116&amp;ref=nb_sb_noss_1"
                  target="_blank"
                  rel="noopener"
                >
                  What Color is Your Parachute
                </Link>{' '}
                by Richard Bolles – updated annually
              </Typography>

              <Typography sx={{ marginBottom: 1 }}>
                <Link
                  to="https://www.amazon.com/Good-They-Cant-Ignore-You/dp/0349415862/ref=monarch_sidesheet"
                  target="_blank"
                  rel="noopener"
                >
                  So Good They Can’t Ignore You
                </Link>{' '}
                by Cal Newport
              </Typography>

              <Typography sx={{ marginBottom: 1 }}>
                <Link to="https://www.amazon.com/Mission-Transition-Matthew-Louis/dp/1400236533/ref=monarch_sidesheet">
                  Mission Transition
                </Link>{' '}
                by Matt Louis USMA ‘91
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
                Below are some books you can read to help you <strong>learn how businesses operate</strong>. This is
                important because you don't want to stand out during your job search. If you want to start your own
                business this is even more important.
              </Typography>

              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                You want to give your future employer the impression that you're an experienced professional that is
                pivoting to another career.
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                You don't want to give the impression that you're "starting over" as an entry-level employee. They'll
                assume you require extensive training, they're unsure if you'll be an asset to their organization, and
                they don't know if you have professional attributes (being on time, communicating expectations, teamwork
                etc.)
              </Typography>

              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>Read an MBA summary book</Typography>

              <Typography sx={{ marginBottom: 1 }}>
                <Link
                  to="https://www.amazon.com/Ten-Day-MBA-4th-Step-Step/dp/0062199579"
                  target="_blank"
                  rel="noopener"
                >
                  "The Ten Day MBA"
                </Link>
              </Typography>

              <Typography sx={{ marginBottom: 1 }}>
                <Link
                  to="https://www.amazon.com/Personal-MBA-10th-Anniversary/dp/0525543023/ref=pd_lpo_1?pd_rd_i=0525543023&amp;psc=1"
                  target="_blank"
                  rel="noopener"
                >
                  "The Personal MBA"
                </Link>
              </Typography>

              <Typography sx={{ marginBottom: 1 }}>
                <Link
                  to="https://www.amazon.com/Visual-MBA-Business-Priceless-Awesomeness/dp/035834364X/ref=sr_1_1?crid=3JSCM5D8PLSU3&amp;keywords=visual+mba&amp;qid=1664458110&amp;qu=eyJxc2MiOiIyLjMyIiwicXNhIjoiMi4xMSIsInFzcCI6IjIuMTEifQ%3D%3D&amp;s=books&amp;sprefix=visual+mb%2Cstripbooks%2C145&amp;sr=1-1"
                  target="_blank"
                  rel="noopener"
                >
                  "The Visual MBA"
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
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                Identify companies you want to do an internship at. Here are some popular websites to find internships:
              </Typography>

              <Typography sx={{ marginBottom: 1 }}>
                <Link to="https://skillbridge.osd.mil/locations.htm" target="_blank" rel="noopener">
                  DOD Skillbridge
                </Link>
              </Typography>

              <Typography sx={{ marginBottom: 1 }}>
                <Link to="https://www.hiringourheroes.org/career-services/fellowships/" target="_blank" rel="noopener">
                  Hiring our Heroes
                </Link>
              </Typography>

              <Typography sx={{ marginBottom: 1 }}>
                <Link to="https://military.microsoft.com/mssa/" target="_blank" rel="noopener">
                  Microsoft Software and Systems Academy
                </Link>
              </Typography>

              <Typography sx={{ marginBottom: 1 }}>
                <Link to="https://amazon.jobs/en/landing_pages/mil-transition" target="_blank" rel="noopener">
                  Amazon Skillbridge Fellowship
                </Link>
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                You can find more by googling the company's name plus "SkillBridge" or "military transition."
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                Some internships are competitive or have specific cohorts. Find ones that meet your timeline. You can
                also create your own CSP.{' '}
                <strong>There are additional requirements to create your own CSP but it'll be worth it.</strong> You can
                see the required documents if you scroll to the CSP portion below or go to the{' '}
                <Link to="https://github.com/nebyou-abera/transition/tree/main/csp" target="_blank" rel="noopener">
                  CSP folder
                </Link>{' '}
                of my{' '}
                <Link to="https://github.com/nebyou-abera/transition" target="_blank" rel="noopener">
                  GitHub
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
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>Recommendations during TAP</Typography>

              <Typography component="ol" sx={{ marginBottom: 2, paddingLeft: 3 }}>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  <strong>Take it seriously</strong>, take notes, and passionately prioritize your transition.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  <strong>Do more</strong>! Get your resume reviewed iteratively, take nonchargeable leave to job
                  conferences, <strong>develop your LinkedIn network, read developmental books</strong>, etc.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  <strong>Tell your TAP counselor what date you want to be TAP complete</strong> so that they can plan
                  accordingly. They have the flexibility, if there's availability, to move your appointments around so
                  that you can be done as soon as possible.
                </Typography>
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
                Develop your network as early and as often as possible.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>Here is a quote from a mentor on Veterati:</Typography>

              <Typography sx={{ fontStyle: 'italic', marginBottom: 2 }}>
                "Most jobs aren't publicly advertised. Most jobs, advertised or not, are filled by networking of some
                kind; applicants known to current employees, applicants known to someone the employee knows, (and
                trusts), or recruiters. Few jobs are filled by blindly interviewing random resumes that survived the
                artificial intelligence (AI)-powered algorithm “keyword” screening."
              </Typography>

              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                Understand how to have a productive 20-minute networking meeting or you will waste each other's time.
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                West Point's Association of Graduates recommends reading{' '}
                <Link
                  to="https://www.amazon.com/20-Minute-Networking-Meeting-Veterans-Network-ebook/dp/B086M5D8DB/ref=sr_1_7?crid=ECAP0J58N3SH&amp;keywords=The+20-Minute+Networking+Meeting&amp;qid=1665184085&amp;qu=eyJxc2MiOiIyLjE2IiwicXNhIjoiMi4xOSIsInFzcCI6IjIuMDkifQ%3D%3D&amp;s=books&amp;sprefix=the+20-minute+networking+meeting%2Cstripbooks%2C261&amp;sr=1-7"
                  target="_blank"
                  rel="noopener"
                >
                  "The 20-Minute Networking Meeting"
                </Link>{' '}
                by Perez and Ballinger. There is also an accompanying video in Handshake in USMA's Career Center
                Resources.
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                Below are two general transition and networking advice documents.
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
              <CardMedia
                component="iframe"
                title="Veteran Basics"
                src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/networking/Veteran%20Basics.pdf&embedded=true"
                sx={{ width: '100%', height: '500px' }}
              />
              <Typography variant="body1" component="p" sx={{ marginTop: 2 }}>
                This document, from a Veterati mentor, provides essential tips and advice for veterans transitioning to
                civilian careers.
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
              <CardMedia
                component="iframe"
                title="Networking Tips"
                src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/networking/Networking_Tips.pdf&embedded=true"
                sx={{ width: '100%', height: '500px' }}
              />
              <Typography variant="body1" component="p" sx={{ marginTop: 2 }}>
                Networking tips from USMA's AOG
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
                You need to master LinkedIn before you begin your job search.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                You need to know how to effectively find people and opportunities on LinkedIn.
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                You need to be able to do a precision search on LinkedIn.
              </Typography>

              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                I recommend looking at profiles of people that have jobs you're interested in or came from the same
                background as you.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                You can even reach out to them for a networking meeting to answer any questions you have. It's best to
                develop relationships in advance. You don't want to cold call someone and ask for a referral.
              </Typography>

              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                Add/follow people that have jobs you're interested in, their managers, the company page, and the
                company's HR managers and recruiters.
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                In some companies, managers do the hiring. In others, HR does the hiring. In some, both have a say in
                the process.
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                Recruiters often post job opportunities, networking events, highlight trends, give out excellent advice,
                etc.
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
                marginBottom: 2
              })}
            >
              <CardMedia
                component="iframe"
                title="test"
                src="https://www.youtube.com/embed/AbfcJMSNFDk"
                sx={{ width: '100%', height: 'auto', aspectRatio: '16/9', marginBottom: 2 }}
              />
              <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                How to search on LinkedIn effectively
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
              <CardMedia
                component="iframe"
                title="LinkedIn Job Search Checklist"
                src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/networking/LinkedIn_Job_Search_Checklist.pdf&embedded=true"
                sx={{ width: '100%', height: '500px' }}
              />
              <Typography variant="body1" component="p" sx={{ marginTop: 2 }}>
                You can find countless resources online on{' '}
                <Link to="https://www.linkedin.com/business/sales/blog/profile-best-practices/17-steps-to-a-better-linkedin-profile-in-2017">
                  how to develop a great LinkedIn profile
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
                marginBottom: 2
              })}
            >
              <CardMedia
                component="iframe"
                title="test"
                src="https://www.youtube.com/embed/fWN9FejxAkM"
                sx={{ width: '100%', height: 'auto', aspectRatio: '16/9' }}
              />
              <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                You can also take a look at LinkedIn's instructions for boolean search{' '}
                <Link to="https://www.linkedin.com/help/linkedin/answer/a524335/using-boolean-search-on-linkedin?lang=en">
                  here
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
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                Join organizations that will get you in contact with people that want to help you.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                They can be veteran-focused charities, educational institutions, minority organizations, professional
                societies, etc. Use your network to find people that know the answers to your questions. You aren't the
                first person to do anything; find someone who has done what you want to do and get their help!
              </Typography>

              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                Lookup local events on sites such as Meetup.com or Eventbrite.com.
              </Typography>
            </Box>
          </TimelineContent>
        </TimelineItem>
        {/* nebdebug TODO add a replacement to veterati here */}
      </Timeline>
    </Box>
  );
}
