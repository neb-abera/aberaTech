import AppAppBar from '../components/AppAppBar';
import Hero from '../components/Hero';
import Divider from '@mui/material/Divider';
import Typography from '@mui/material/Typography';
import CssBaseline from '@mui/material/CssBaseline';
import AppTheme from '../theme/AppTheme';
import Container from '@mui/material/Container';
import Box from '@mui/material/Box';
import Timeline from '@mui/lab/Timeline';
import TimelineItem from '@mui/lab/TimelineItem';
import TimelineSeparator from '@mui/lab/TimelineSeparator';
import TimelineConnector from '@mui/lab/TimelineConnector';
import TimelineContent from '@mui/lab/TimelineContent';
import TimelineDot from '@mui/lab/TimelineDot';
import CircleIcon from '@mui/icons-material/Circle';
import { Link } from 'react-router';
import { CardMedia } from '@mui/material';
import TimelineSeparatorCustom from '../components/TimelineSeparatorCustom';
import { timelineItemClasses } from '@mui/lab/TimelineItem';

export default function MilitaryTransitionGuide(props: { disableCustomTheme?: boolean }) {
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
            I made this guide to share the lessons I learned during my transition. I embedded links,
            documents, and videos. Please click on those to ensure that you understand the topic in
            its breadth and depth. This is a work in progress so feel free to send me questions,
            comments, and recommendations.
          </Typography>

          <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
            I included some personal stories in this guide, that you can skip over if you're not
            interested, to illustrate the importance of some of these lessons. Please don't think
            that I did it to attack individual people in this guide. Most people do the best they
            can with the information and tools they have. But, you can have good people stuck in a
            bad bureaucratic system that causes unintended consequences for transitioning
            servicemembers. You will need to get the bureaucracy to work for you, not against you.
            Do not get discouraged by it. You need to advocate for yourself, your transition is
            worth fighting for.
          </Typography>
        </Box>
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
                    <strong>
                      The earlier you can complete the Transition Assistance Program (TAP) the
                      better
                    </strong>
                    . I recommend completing TAP as early as possible. That's because not having a
                    completed DD Form 2648 will prevent you from being able to schedule a Career
                    Skills Program (CSP) internship.
                  </Typography>
                  <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                    <strong>
                      Retiring soldiers can begin TAP 24 months out from their ETS. Everyone else
                      can begin TAP at 18 months from their prospective ETS date
                    </strong>
                    . You start TAP by going to your installation's TAP center, registering for the
                    program, and getting a counselor assigned.
                  </Typography>
                  <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                    You can find the information for Fort Benning at{' '}
                    <Link
                      to="https://home.army.mil/moore/About/garrison/DHR/TAP"
                      target="_blank"
                      rel="noopener"
                    >
                      Fort Benning | Army Transition Assistance Program (TAP)
                    </Link>
                  </Typography>
                  <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                    <strong>
                      Ensure you have access to your TSP account, you are getting the Army matching
                      contributions, and move your retirement to a fund you like
                    </strong>
                    . The general rule of thumb is if you're young, or don't need access to your
                    retirement, invest it in "riskier" funds like the S fund. If you're older, or
                    more risk-averse, choose "safer" funds.
                  </Typography>
                  <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                    <strong>STORY ABOUT HOW I LEARNED THIS:</strong> I began TAP at around the
                    11-month mark, told my TAP counselor I wanted to do a CSP in Chicago, and then
                    my TAP counselor spread out my appointments all the way out until the 6-month
                    mark. My TAP counselor didn't seem very familiar with CSP but said we would
                    discuss at a future appointment. It seemed reasonable at the time since I didn't
                    know any better. At around the 9-month mark, I reached out to my installation's
                    CSP coordinator but she said she wouldn't give me the CSP documents until I was
                    TAP complete. I then had to go back to my TAP counselor and tell her I needed
                    her to re-schedule all of my appointments so that I could be done as quickly as
                    possible. When I completed TAP at about the 8-month mark, I found out that I had
                    to get O6 approval for my CSP in Chicago since it was outside of a 50-mile
                    radius from Fort Benning. Fort Benning also didn't have any CSP options
                    available for my timeline (remote or in-person). I had to personally reach out
                    to countless companies to create a CSP, and then get JAG and Installation
                    Command approval. I even flew out to a job conference in DC to approach a
                    Northrop Grumman recruiter that I'd lost contact with. I got the approval from
                    Northrop Grumman too late for JAG approval and I lost out on my dream CSP. That
                    didn't end up mattering though because my packet had actually not been sent up
                    to the approval authority by the bureaucracy. But that's another story. I had
                    great Company, Battalion, and Brigade commanders, along with the support of my
                    unit, so everything eventually worked out. You might not, so you will need to
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
                      The most important skill to have is the ability to search for the answer to
                      your questions
                    </strong>
                  </Typography>
                  <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
                    Learn how to use google search to find what you need. I like to google to search
                    Reddit and quora for the answers I'm looking for. Just add "Reddit" or "quora"
                    to your search terms, and try to look for the most recent posts with the most
                    discussion. The most upvoted answers are usually the best. The dissenting
                    opinions are also important to understand.
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
                    Use content creators on YouTube, TikTok, and Reddit to help your transition.
                    Just use what you're interested in as a search term and they will feed you the
                    people that are related to that topic.
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
                    Use Credential Assistance (CA) to pay for relevant education. CA does not incur
                    a service obligation for any soldier, including officers.{' '}
                    <Link
                      to="https://home.army.mil/moore/About/garrison/DHR/TAP"
                      target="_blank"
                      rel="noopener"
                    >
                      Updates to Army Credentialing Assistance Policy
                    </Link>{' '}
                    Paragraph 10a. "The service obligation for the CA program has been removed per
                    reference 1a, dated 13 October 2021."
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
                    This is one of the intro videos they show you. You can go to this website to see
                    an example of some of the classes you will take{' '}
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
                      You need to ensure that all of your illnesses and injuries are appropriately
                      recorded in your medical record.
                    </span>{' '}
                    You also need to follow up with anything that's happened earlier in your career
                    (even if you let it go untreated). If you don't get follow-up treatments for
                    something, later the VA will assume it didn't bother you so therefore it wasn't
                    a problem.
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    <span style={{ fontWeight: 'bold' }}>
                      The best way to complete the VA disability process is through the Benefits
                      Delivery at Discharge (BDD) program when you're between 180 and 90 days from
                      your ETS.
                    </span>{' '}
                    The benefit of doing a BDD is that it will be substantially easier to get your
                    benefits paid upon ETS. You will upload your medical record (which you will have
                    to request at about the 7-8 month mark), complete a physical, register a VA
                    account, and initiate an online BDD claim.
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    <span style={{ fontWeight: 'bold' }}>
                      DO NOT PAY ANYBODY FOR ANY HELP WITH VA DISABILITY SERVICES. THERE ARE A LOT
                      OF SCAM ARTISTS WHO WILL TAKE YOUR MONEY
                    </span>{' '}
                    There are so many free resources that can help you. TAP has a good VA disability
                    class, YouTube has some popular disability content creators, and there are
                    plenty of resources online. The VA has dedicated people to help you, veteran
                    organizations like DAV provide free help, and I'm sure you know folks who have
                    gone through the process that can help. Do not do it alone, but also don't pay
                    anyone to help.
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
                      What do you want to do after you leave the Army? Do you want to start a
                      different career, pursue education, start your own company, retire etc?
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      What are your priorities for your transition? How important is moving to a
                      specific location to you vs getting your dream job vs maximizing total
                      compensation etc?
                    </Typography>
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    Here are some books to help you think about what the next stage of your life can
                    look like
                  </Typography>

                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    Immerse yourself in transition books, videos, and surround yourself with people
                    willing to answer your questions
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    Here are the most highly recommended books from West Point's Association of
                    Graduates. I recommend reading them all.
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
                    Below are some books you can read to help you{' '}
                    <strong>learn how businesses operate</strong>. This is important because you
                    don't want to stand out during your job search. If you want to start your own
                    business this is even more important.
                  </Typography>

                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    You want to give your future employer the impression that you're an experienced
                    professional that is pivoting to another career.
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    You don't want to give the impression that you're "starting over" as an
                    entry-level employee. They'll assume you require extensive training, they're
                    unsure if you'll be an asset to their organization, and they don't know if you
                    have professional attributes (being on time, communicating expectations,
                    teamwork etc.)
                  </Typography>

                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    Read an MBA summary book
                  </Typography>

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
                    Identify companies you want to do an internship at. Here are some popular
                    websites to find internships:
                  </Typography>

                  <Typography sx={{ marginBottom: 1 }}>
                    <Link
                      to="https://skillbridge.osd.mil/locations.htm"
                      target="_blank"
                      rel="noopener"
                    >
                      DOD Skillbridge
                    </Link>
                  </Typography>

                  <Typography sx={{ marginBottom: 1 }}>
                    <Link
                      to="https://www.hiringourheroes.org/career-services/fellowships/"
                      target="_blank"
                      rel="noopener"
                    >
                      Hiring our Heroes
                    </Link>
                  </Typography>

                  <Typography sx={{ marginBottom: 1 }}>
                    <Link to="https://military.microsoft.com/mssa/" target="_blank" rel="noopener">
                      Microsoft Software and Systems Academy
                    </Link>
                  </Typography>

                  <Typography sx={{ marginBottom: 1 }}>
                    <Link
                      to="https://amazon.jobs/en/landing_pages/mil-transition"
                      target="_blank"
                      rel="noopener"
                    >
                      Amazon Skillbridge Fellowship
                    </Link>
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    You can find more by googling the company's name plus "SkillBridge" or "military
                    transition."
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    Some internships are competitive or have specific cohorts. Find ones that meet
                    your timeline. You can also create your own CSP.{' '}
                    <strong>
                      There are additional requirements to create your own CSP but it'll be worth
                      it.
                    </strong>{' '}
                    You can see the required documents if you scroll to the CSP portion below or go
                    to the{' '}
                    <Link
                      to="https://github.com/nebyou-abera/transition/tree/main/csp"
                      target="_blank"
                      rel="noopener"
                    >
                      CSP folder
                    </Link>{' '}
                    of my{' '}
                    <Link
                      to="https://github.com/nebyou-abera/transition"
                      target="_blank"
                      rel="noopener"
                    >
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
                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    Recommendations during TAP
                  </Typography>

                  <Typography component="ol" sx={{ marginBottom: 2, paddingLeft: 3 }}>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      <strong>Take it seriously</strong>, take notes, and passionately prioritize
                      your transition.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      <strong>Do more</strong>! Get your resume reviewed iteratively, take
                      nonchargeable leave to job conferences,{' '}
                      <strong>develop your LinkedIn network, read developmental books</strong>, etc.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      <strong>Tell your TAP counselor what date you want to be TAP complete</strong>{' '}
                      so that they can plan accordingly. They have the flexibility, if there's
                      availability, to move your appointments around so that you can be done as soon
                      as possible.
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
                  <Typography sx={{ marginBottom: 2 }}>
                    Here is a quote from a mentor on Veterati:
                  </Typography>

                  <Typography sx={{ fontStyle: 'italic', marginBottom: 2 }}>
                    "Most jobs aren't publicly advertised. Most jobs, advertised or not, are filled
                    by networking of some kind; applicants known to current employees, applicants
                    known to someone the employee knows, (and trusts), or recruiters. Few jobs are
                    filled by blindly interviewing random resumes that survived the artificial
                    intelligence (AI)-powered algorithm “keyword” screening."
                  </Typography>

                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    Understand how to have a productive 20-minute networking meeting or you will
                    waste each other's time.
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
                    by Perez and Ballinger. There is also an accompanying video in Handshake in
                    USMA's Career Center Resources.
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
                    This document, from a Veterati mentor, provides essential tips and advice for
                    veterans transitioning to civilian careers.
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
                    I recommend looking at profiles of people that have jobs you're interested in or
                    came from the same background as you.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    You can even reach out to them for a networking meeting to answer any questions
                    you have. It's best to develop relationships in advance. You don't want to cold
                    call someone and ask for a referral.
                  </Typography>

                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    Add/follow people that have jobs you're interested in, their managers, the
                    company page, and the company's HR managers and recruiters.
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    In some companies, managers do the hiring. In others, HR does the hiring. In
                    some, both have a say in the process.
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    Recruiters often post job opportunities, networking events, highlight trends,
                    give out excellent advice, etc.
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
                    Join organizations that will get you in contact with people that want to help
                    you.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    They can be veteran-focused charities, educational institutions, minority
                    organizations, professional societies, etc. Use your network to find people that
                    know the answers to your questions. You aren't the first person to do anything;
                    find someone who has done what you want to do and get their help!
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
        <Box
          sx={(theme) => ({
            p: 2,
            border: `1px solid ${theme.palette.divider}`,
            backgroundColor: theme.palette.background.paper,
            color: theme.palette.text.primary,
            boxShadow: theme.shadows[1]
          })}
        >
          <Typography variant="h2" component="h2" sx={{ marginBottom: 2, textAlign: 'center' }}>
            <strong>12-18 months</strong>
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
                    width: '100%',
                    marginBottom: 2,
                    textAlign: 'left'
                  })}
                >
                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    Once you are TAP complete, you will get a signed DD Form 2648 and you can use
                    that to contact the Career Skills Program Installation Administrator (IA) and
                    ask for the requirements to do a CSP.
                  </Typography>

                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    Successfully completing a CSP at a company often results in a job offer from the
                    company.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    At a minimum, they have to give you an interview. Most of the time, they only
                    accept people they are interested in hiring. The benefit for them is they got a
                    cost-free employee that they got to try out for months. You will be a known
                    quantity, they'll have built a relationship with you, you'll have already had
                    months of relevant experience doing that specific job, etc.
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    They will send you an email similar to the message and documents below.
                  </Typography>

                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    Key highlights and recommendations
                  </Typography>

                  <Typography component="ol" sx={{ marginBottom: 2, paddingLeft: 3 }}>
                    <Typography component="li" sx={{ marginBottom: 2 }}>
                      The{' '}
                      <strong>
                        approval authority for CSPs within a 50-mile radius of your installation
                      </strong>{' '}
                      is the first field grade in your chain with command authority, which{' '}
                      <strong>is usually your Battalion Commander</strong>. The{' '}
                      <strong>
                        approval authority for CSPs outside of a 50-mile radius and lasting 60 or
                        fewer days is an O6 commander (can be delegated to O5). The approval
                        authority for CSPs outside of a 50-mile radius, and between 61 and 180 days,
                        is the first commanding general in your chain of command (can be delegated
                        down to an O6 commander).
                      </strong>{' '}
                      I heard a rumor that the MCOE CG is now the approval authority for Fort
                      Benning CSPs outside of 50 miles and longer than 60 days. I used 1st SFAB's O6
                      commander for my CSP approval.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 2 }}>
                      You aren't limited to the CSPs offered by your installation. You can do any
                      internship in the DOD SkillBridge network.{' '}
                      <strong>
                        You can also make your own internships if none of the offered options fit
                        your needs and desires.
                      </strong>{' '}
                      Check below, or see the{' '}
                      <Link
                        to="https://github.com/nebyou-abera/transition/tree/main/csp"
                        target="_blank"
                        rel="noopener"
                      >
                        CSP folder
                      </Link>{' '}
                      on my{' '}
                      <Link
                        to="https://github.com/nebyou-abera/transition"
                        target="_blank"
                        rel="noopener"
                      >
                        GitHub
                      </Link>
                      , to see what documents you need for that. In general, you need to find a
                      company that will work with you, create a training plan, and get JAG and
                      INSCOM approval. Make sure you give JAG about 30 days to review and approve
                      your CSP.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 2 }}>
                      <strong>
                        Your CSP, and any terminal leave you decide to take, need to start and end
                        within the 180-day window between your ETS.
                      </strong>{' '}
                      For example, you can do 180 days of a CSP but you'll have 0 days of terminal
                      leave (so you'll have to take your "ordinary" leave before the CSP). You can
                      do 120 days of CSP and 60 days of terminal leave, which will equal 180 days
                      together.{' '}
                      <strong>
                        You can take leave before the 180-day window but you can't mark the DA 31 as
                        transition leave. It needs to be marked as annual (ordinary) leave.
                      </strong>{' '}
                      If you decide to take leave before the 180 days before CSP, do not call it
                      terminal leave to your S1 or leadership, that will only cause confusion.{' '}
                      <strong>
                        I recommend that you leave 10 business days before your terminal leave date
                        so that you can come back and clear the installation.
                      </strong>
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 2 }}>
                      You can{' '}
                      <Link
                        to="https://milversity.com/blog/terminal-leave-calculator-guide/"
                        target="_blank"
                        rel="noopener"
                      >
                        sell extra leave days back to the Army
                      </Link>{' '}
                      but they will be taxed at the marginal tax rate of ~22-25% and you won't get
                      BAH for it.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 2 }}>
                      If you want to maximize the amount of money you get, use your earned leave
                      before and/or after your CSP.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 2 }}>
                      Reach out and apply to companies early; you need their signature for your CSP
                      packet.
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
                  <Typography sx={{ marginBottom: 2 }}>
                    You can{' '}
                    <Link
                      to="https://news.va.gov/61535/veterans-transitioning-military-get-free-year-linkedin-premium/"
                      target="_blank"
                      rel="noopener"
                    >
                      get access to LinkedIn premium for 12 months
                    </Link>{' '}
                    as a veteran. I'd recommend activating it when you feel comfortable navigating
                    LinkedIn and you are ready to grow your network.
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    The main benefit I saw from premium was being able to send messages to people
                    outside your network. I didn't really care about seeing who was looking at my
                    profile.{' '}
                    <strong>
                      I don't see the value of LinkedIn Premium once you've done the heavy lifting
                      of building your network
                    </strong>
                    . So, I'm not going to pay for it once it expires.
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
                    [Example message from CSP administrator]
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>Hello,</Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    Thank you for your interest in the CSP Individual Internship. Please review the
                    eligibility criteria below and the instructions for submitting the documents.
                  </Typography>

                  <Typography component="ol" sx={{ marginBottom: 2, paddingLeft: 3 }}>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      Requires Soldier to be 6 months/180 days of ETS or retirement date [not
                      inclusive of the terminal leave time].
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      CSP participation may only involve ONE company or training program within the
                      180 days not to exceed 120 days.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      Establish POC with the company, internship start/end dates, and a weekly
                      internship curriculum.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      Requires Command approval and signatures on submission of all documents
                      required.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      Completion of TAP [include the Form 2648 with packet received at completion].
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      Requires submission of the Internship packet 1 month prior to your planned
                      start date [for Legal and Regional Coordinator approval process, at this time
                      Legal is taking 15-30 days to process packets].
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      An approved Administrative Absence signed by the O-6 must be acquired to
                      participate outside 50 miles of Fort Benning.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      Be mindful that CSP must begin after or be complete before transition/terminal
                      leave if you plan to take it during the 6-month window of CSP participation.
                    </Typography>
                  </Typography>

                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    INSTRUCTIONS FOR COMPLETING INTERNSHIP DOCUMENTS:
                  </Typography>

                  <Typography component="ol" sx={{ marginBottom: 2, paddingLeft: 3 }}>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      CSP Individual Internship Agreement: Complete and submit in the same fillable
                      format originally provided with digital signatures. (Do not print or scan this
                      document. Return in the editable digital PDF format with digital signatures.)
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      CSP Participation Memo: Complete and submit in the fillable format provided
                      with digital signatures. Must have O-6 approval for Administrative Absence.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      DA31: Complete with O-6 signature only if the internship is 50 miles outside
                      of Fort Benning.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      CSP Commander's Checklist: Complete with both your and Commander's initials.
                      Please select "No" if any of the questions do not apply.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      Please include the TAP Form 2648 provided by your counselor.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      Please attach a separate training curriculum including each week you will be
                      interning (See example attached).
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      Please ensure the dates on the start and end dates DA31 and Participation Memo
                      are the same.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      Please include in the Internship Agreement, outcome section, there will be an
                      interview opportunity. Please label and attach each document individually. Do
                      not submit the packet as one scanned PDF document.
                    </Typography>
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    Please ensure you mention in #4 of the agreement the internship will lead to an
                    interview with the company. Follow all instructions provided to prevent delays
                    with the final approval process. If you have any questions or concerns, please
                    do not hesitate to contact me.
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>Warm Regards,</Typography>
                  <Typography sx={{ marginBottom: 2 }}>XXXXX</Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    Career Skills Program Installation Administrator (IA)
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>Soldier for Life TAP Bldg 9230</Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    8150 Marne Rd. Fort Benning, GA 31905
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>P: XXXX</Typography>
                  <Typography sx={{ marginBottom: 2 }}>E: XXXXX</Typography>
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
                    title="CSP Checklist"
                    src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/csp/CSP%20Checklist%2023%20MAR.pdf&embedded=true"
                    sx={{ width: '100%', height: '500px' }}
                  />
                </Box>
              </TimelineContent>
            </TimelineItem>
            <TimelineItem></TimelineItem>
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
                    title="CSP Individual Internship Agreement"
                    src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/csp/Agreement%20for%20Army%20CSP%20Individual%20Internship_Approved%20DoD%20SkillBridge%20Program%2020210412.pdf&embedded=true"
                    sx={{ width: '100%', height: '500px' }}
                  />
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
                    title="CSP Participation Memo"
                    src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/csp/CSP%20Soldier%20Participation%20Memo_IMCOM%20Form%2045_FINAL.pdf&embedded=true"
                    sx={{ width: '100%', height: '500px' }}
                  />
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
                    title="Example Letter for PEBLO"
                    src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/csp/Example%20Letter%20for%20PEBLO.docx&embedded=true"
                    sx={{ width: '100%', height: '500px', marginBottom: 2 }}
                  />
                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    This document is only required if you're going through a "med board" and want to
                    do a CSP.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    This is important because administratively there will be long wait times without
                    any notice if you're being kicked out or not. You can use this time to do a CSP
                    instead of languishing in uncertainty. When they do decide to med-board you or
                    not, all of a sudden they might tell you you've been med-boarded, they'll send
                    you a VA and DOD compensation offer to negotiate, and you'll have about 30-60
                    days or less to get your affairs together and out-process the Army.
                  </Typography>

                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    STORY ABOUT HOW I LEARNED THIS:
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    My friend and I worked together in the same unit, started separation processes
                    at around the same time, and ETS'ed at around the same time. I UQR/REFRAD'ed
                    while he went through a med board.
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    <strong>
                      He went from languishing for months not knowing if he was able to stay in the
                      Army, to being told he needed to quickly get out.
                    </strong>{' '}
                    <strong>He didn't do a CSP</strong>, since none that Fort Benning CSP offered
                    fit his timeline or needs. He also didn't feel he had enough time to make a CSP
                    with another company. And on top of that,{' '}
                    <strong>he stayed at our unit working</strong> which messed with his ability to
                    focus on his transition.
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    <strong>
                      Meanwhile, I spent my time doing an internship, interviewing at companies, and
                      got dedicated time to work on my transition.
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
                  <CardMedia
                    component="iframe"
                    title="Internship Training Plan"
                    src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/csp/Internship%20Training%20Plan.xlsx&embedded=true"
                    sx={{ width: '100%', height: '500px', marginBottom: 2 }}
                  />
                  <Typography sx={{ marginBottom: 2 }}>
                    Here is an example training plan for creating your own CSP with a company. Some
                    companies already have their own training plan if they have already hosted CSP
                    interns. This is much more thorough than required. My CSP coordinator ended up
                    only submitting the first sheet. That makes me think that's all that would be
                    required for approval.
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
                    If you are interested in education, you should apply to universities and
                    scholarships at this time.{' '}
                    <strong>
                      You should apply to schools outside of what you "think" you can't get into.
                      Top schools like Harvard, Yale, University of Chicago want veteran
                      representation
                    </strong>{' '}
                    and it'll be easier for you to get in as a veteran. You will still have to put
                    in a lot of work to "sell yourself" but it will be worth it.
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    You can use the{' '}
                    <Link
                      to="https://www.va.gov/education/about-gi-bill-benefits/post-9-11/yellow-ribbon-program/"
                      target="_blank"
                      rel="noopener"
                    >
                      Yellow Ribbon program
                    </Link>
                    , the{' '}
                    <Link
                      to="https://pattillmanfoundation.org/apply/"
                      target="_blank"
                      rel="noopener"
                    >
                      Pat Tilman scholarship
                    </Link>
                    , and a variety of other resources to cover additional expenses. Ensure that you
                    understand how the GI Bill works. The TAP gives a great GI Bill course and
                    teaches you tools like the{' '}
                    <Link
                      to="https://www.va.gov/education/gi-bill-comparison-tool/"
                      target="_blank"
                      rel="noopener"
                    >
                      VA comparison tool
                    </Link>
                    .
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    If you are interested in working, be aware of{' '}
                    <Link to="https://www.va.gov/education/" target="_blank" rel="noopener">
                      VA education and job training programs
                    </Link>{' '}
                    like{' '}
                    <Link
                      to="https://www.va.gov/education/about-gi-bill-benefits/how-to-use-benefits/vettec-high-tech-program/"
                      target="_blank"
                      rel="noopener"
                    >
                      VET TEC
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
                  <Typography sx={{ marginBottom: 2 }}>
                    TAP has a great course on helping you understand what career fields have great
                    opportunities, and what locations have better opportunities for your chosen
                    career field.
                  </Typography>

                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    You should decide if you are restricted by location.
                  </Typography>

                  <Typography sx={{ marginBottom: 2 }}>
                    If you're not, where are you interested in living? Are you willing to relocate
                    for a job? If you are, what is the minimum compensation package that makes
                    moving worth it to you?
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
                    Learn the art and science of resumes.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    Develop a couple of versions of your resume. The{' '}
                    <strong>most important part is</strong> doing a <strong>brainstorming</strong>{' '}
                    exercise where you{' '}
                    <strong>write down every result you've produced in your career</strong>. You can
                    use old OERs and counseling to help you start. This will help you translate your
                    Army experiences to civilian terminology. Use the Project, Action, Result model.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    You can use the brainstorming exercise to tailor your resume to highlight
                    experiences that match the job description.
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
                    title="General Resume Guidelines"
                    src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/resume_help/GeneralResumeGuidelines.pdf&embedded=true"
                    sx={{ width: '100%', height: '500px' }}
                  />
                  <Typography variant="body1" component="p" sx={{ marginTop: 2 }}>
                    General resume guidelines
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
                    title="Booth Resume Template"
                    src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/resume_help/Booth-Resume-Template_.doc&embedded=true"
                    sx={{ width: '100%', height: '500px' }}
                  />
                  <Typography variant="body1" component="p" sx={{ marginTop: 2 }}>
                    Resume template
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
                    title="Resume Writing Guide"
                    src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/resume_help/Resume_Writing_Guide_(with_Samples).pdf&embedded=true"
                    sx={{ width: '100%', height: '500px' }}
                  />
                  <Typography variant="body1" component="p" sx={{ marginTop: 2 }}>
                    Resume template
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
                    title="Resume Formatting 101 Updated"
                    src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/resume_help/Resume%20Formatting%20101_Updated.pdf&embedded=true"
                    sx={{ width: '100%', height: '500px' }}
                  />
                  <Typography variant="body1" component="p" sx={{ marginTop: 2 }}>
                    More resume advice
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
                    title="Action Verbs"
                    src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/resume_help/actionverbs.pdf&embedded=true"
                    sx={{ width: '100%', height: '500px' }}
                  />
                  <Typography variant="body1" component="p" sx={{ marginTop: 2 }}>
                    More resume advice
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
                    title="Resume Accomplishments or Bullets Builder"
                    src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/resume_help/Resume_Accomplishments_or_Bullets_Builder%20(1).docx&embedded=true"
                    sx={{ width: '100%', height: '500px' }}
                  />
                  <Typography variant="body1" component="p" sx={{ marginTop: 2 }}>
                    Structuring accomplishments
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
                    title="Resume Fix List"
                    src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/resume_help/Resume-Fix-List.pdf&embedded=true"
                    sx={{ width: '100%', height: '500px' }}
                  />
                  <Typography variant="body1" component="p" sx={{ marginTop: 2 }}>
                    Structuring accomplishments
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
                    UQR/REFRAD personnel need to produce the following documents:
                  </Typography>
                  <Typography component="ol" sx={{ marginBottom: 2, paddingLeft: 3 }}>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      UQR/REFRAD request to HRC.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      BDE CDR counseling signed by your CDR and addressed to HRC. This requires you
                      to draft the document and then{' '}
                      <strong>schedule a counseling by your BDE CDR (so they can sign it).</strong>
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      Get a counseling signed by your installation's Reserve counselor.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      A memo stating that you aren't resigning due to SHARP.
                    </Typography>
                  </Typography>
                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    Do not let your company commander, battalion commander, or S1 try to deter you
                    from submitting your UQR/REFRAD. They are not the approval authority.
                  </Typography>
                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    STORY ABOUT HOW I LEARNED THIS:
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    I had incredible Company, Battalion, and Brigade commanders at my last unit in
                    the Army. I scheduled my BDE CDR counseling at about 13 months until ETS. I had
                    an awesome Battalion Commander who asked me to cancel it so that he could work
                    to try to find a BN in Chicago that could take me in a recruiting or ROTC role.
                    We exhausted those options while I got sent to Airborne school at the 11-month
                    mark and I couldn't do anything about my UQR/REFRAD.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    It then took an additional couple of weeks to schedule a counseling session and
                    get my BDE CDR to sign it. Once I submitted my UQR/REFRAD at about the 9-10
                    month mark, it waited at HRC. I later found out from my lawyer that the
                    reviewer/approver was on leave. The unintended consequence of listening to my
                    Battalion Commander cost my UQR/REFRAD to be delayed by 3 or so months and could
                    have easily derailed the rest of my transition if my lawyer hadn't made sure the
                    reviewer/approver prioritized it when he got back.
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
                    title="Example UQR Request"
                    src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/uqr/example%20UQR%20Request.docx&embedded=true"
                    sx={{ width: '100%', height: '500px' }}
                  />
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
                    title="Example UQR Request"
                    src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/uqr/example%20UQR%20Request.docx&embedded=true"
                    sx={{ width: '100%', height: '500px' }}
                  />
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
                    title="Example SHARP Memo"
                    src="https://docs.google.com/viewer?url=https://raw.githubusercontent.com/neb-abera/transition/main/uqr/SHARP%20memo%20example.docx&embedded=true"
                    sx={{ width: '100%', height: '500px' }}
                  />
                </Box>
              </TimelineContent>
            </TimelineItem>
          </Timeline>
        </Box>
      </Container>
    </AppTheme>
  );
}
