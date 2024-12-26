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
import ETSScheduleImage from '../assets/ETS_briefing_schedule.jpeg';
import G1Schedule from '../assets/g1_contact_info.png';

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
                    You can find the information for Fort Moore at{' '}
                    <Link
                      to="https://home.army.mil/moore/About/garrison/DHR/TAP"
                      target="_blank"
                      rel="noopener"
                    >
                      Fort Moore | Army Transition Assistance Program (TAP)
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
                    radius from Fort Moore. Fort Moore also didn't have any CSP options available
                    for my timeline (remote or in-person). I had to personally reach out to
                    countless companies to create a CSP, and then get JAG and Installation Command
                    approval. I even flew out to a job conference in DC to approach a Northrop
                    Grumman recruiter that I'd lost contact with. I got the approval from Northrop
                    Grumman too late for JAG approval and I lost out on my dream CSP. That didn't
                    end up mattering though because my packet had actually not been sent up to the
                    approval authority by the bureaucracy. But that's another story. I had great
                    Company, Battalion, and Brigade commanders, along with the support of my unit,
                    so everything eventually worked out. You might not, so you will need to advocate
                    for yourself and understand requirements and timelines in-depth.
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
                      I heard a rumor that the MCOE CG is now the approval authority for Fort Moore
                      CSPs outside of 50 miles and longer than 60 days. I used 1st SFAB's O6
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
                      participate outside 50 miles of Fort Moore.
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
                      of Fort Moore.
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
                    8150 Marne Rd. Fort Moore, GA 31905
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
                    <strong>He didn't do a CSP</strong>, since none that Fort Moore CSP offered fit
                    his timeline or needs. He also didn't feel he had enough time to make a CSP with
                    another company. And on top of that,{' '}
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
            <strong>9-12 months</strong>
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
            <TimelineContent>
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
                      Retirement & UQR/REFRAD packets can be sent up through your chain of command
                      at 12 months until ETS.
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      You need to get the contact information for the relevant section at your
                      installation's G1. BN or BDE S1s will send your packet to G1, and it is on G1
                      to send the packet to HRC. S1s usually do this by sending your packet to a
                      group G1 email. G1 can be picky and unresponsive, so you need to follow up
                      with them. They've been known not to respond if something isn't done
                      correctly.
                    </Typography>

                    <Typography sx={{ marginBottom: 2 }}>
                      At Fort Moore, the G1 equivalent for the installation is called the{' '}
                      <Link
                        to="https://www.moore.army.mil/Garrison/DHR/Contacts.html"
                        target="_blank"
                        rel="noopener"
                      >
                        Directorate of Human Resources
                      </Link>{' '}
                      and they are at building 35. The{' '}
                      <strong>information they put out will likely not make it down to you</strong>{' '}
                      due to personnel turnover and the "telephone game" that happens during info
                      distribution through the chain of command.{' '}
                      <strong>Requirements, and the people in charge, change</strong>, so you will
                      need to{' '}
                      <strong>know what every echelon in your chain of command requires</strong> all
                      the way up to HRC. It's daunting but worth it.
                    </Typography>

                    <Typography sx={{ marginBottom: 2 }}>
                      The Army needs an electronic administrative action processing and approval
                      system. S1s are overwhelmed with administrative tasks and don't have good
                      systems to track and process them. On top of that, S1s in SFAB don't have
                      clerks to help them out. I really feel for them. Email is not a good method
                      for processing and tracking administrative actions. People change duty
                      positions at every level, and requirements change all the time. S1s are good
                      people stuck in a bad system.
                    </Typography>

                    <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                      STORY ABOUT HOW I LEARNED THIS:
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      My S1 said he sent my request for orders at around the 8-month mark. He would
                      respond with something to the effect of "it's pending approval at G1" when I
                      asked for a progress update. At the 6-month mark before ETS, when it was
                      getting close for me to move to Chicago for my CSP, he told my 1SG that I had
                      never sent my request for orders to him. I sent him an email confronting him
                      about what I perceived as lying and telling him that I was going to escalate
                      the situation if I didn't get help with my CSP, orders, and Reserve
                      transition.
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      I went to building 35, found the permanent party separation department, and
                      found out that my request for orders, whether it was sent or not, would not
                      get approved because for the first time in 2 years, since before COVID, they
                      were now requiring in-person ETS briefs before soldiers can request orders.
                      The civilians also said that they had repeatedly pushed this information out
                      to the S1 channel.{' '}
                      <strong>
                        Every week at every Army installation there are people suffering transition
                        nightmares.
                      </strong>{' '}
                      <strong>
                        The Army will then pay the price for these nightmares financially via
                        unemployment, or the Army's reputation will suffer when stories of these
                        transition nightmares are spread.
                      </strong>{' '}
                      I personally witnessed three examples on my Final Out day when I cleared Fort
                      Moore on 30 September. I also faced a lot of bureaucratic heartaches and had
                      to fight hard to get what I needed.
                    </Typography>

                    <Typography component="ol" sx={{ marginBottom: 2, paddingLeft: 3 }}>
                      <Typography component="li" sx={{ marginBottom: 1 }}>
                        There are people unable to retire on their original timeline because they
                        didn't attend a mandatory retirement brief (that they did not know about).
                      </Typography>
                      <Typography component="li" sx={{ marginBottom: 1 }}>
                        Leaders escorting people to clear installation a month after their ETS date.
                        Their units didn't prioritize the soldier's transition, and the soldier
                        didn't fight them back hard enough. The person that pays the price in these
                        scenarios is the soldier. Their transition timeline is messed up and they
                        are clearing without getting paid.
                      </Typography>
                      <Typography component="li" sx={{ marginBottom: 1 }}>
                        People trying to final out with just a unit clearance record (there are so
                        many more requirements than that).
                      </Typography>
                    </Typography>

                    <Typography sx={{ marginBottom: 2 }}>
                      I asked the civilian who issued my DD214 how often horror stories like that
                      happen. She said they happen every week and that{' '}
                      <strong>
                        the information G1 distributes does not make it down through the S1 channels
                        to the soldiers.
                      </strong>
                    </Typography>

                    <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                      You need to be proactive, keep receipts of all interactions, and be willing to
                      escalate to a higher authority if you don't get the help you need in an
                      acceptable timeframe.
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      You have the S1 chain, the leadership chain, speaking to civilian supervisors,
                      ICE complaints, your congressperson, or you can consult a lawyer. The longer
                      you wait, the less time you give yourself and others to fix your problem
                      before you have to use a "nuclear" option like a senior-level commander or a
                      lawyer.
                    </Typography>

                    <Typography sx={{ marginBottom: 2 }}>
                      <strong>The system is very bureaucratic</strong> and it's easy for your
                      administrative actions to fail to make it through. It's worth fighting for.{' '}
                      <strong>Do not feel awkward or ashamed about advocating for yourself.</strong>
                    </Typography>

                    <Typography sx={{ marginBottom: 2 }}>
                      "the squeaky wheel gets the grease"
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>"hungry mouths don't get fed"</Typography>
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
                      Submit your signed and completed CSP packet to your installation coordinator
                      several months before your CSP start date.
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      It might take JAG up to 4-8 weeks to review/sign your packet and send it to
                      the installation command.
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      Your CSP coordinator will inform you when your CSP has been approved.
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
                      Do not take no for an answer if your CSP, retirement/UQR, or other transition
                      documents get "disapproved."
                    </Typography>

                    <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                      Keep escalating it. Your transition is worth fighting for.
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      You’re your own best advocate.{' '}
                      <strong>
                        Your more senior commanders probably don’t even know about your denial, and
                        likely won't support the denial
                      </strong>
                      . “People first” isn’t just a saying. Soldiers transitioning well is in the
                      best financial and political interests of the Army, on top of the morally
                      right thing to do.
                    </Typography>

                    <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                      Do not let anyone other than the approval authority stop your packet from
                      reaching the approval authority.
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      The lowest approval authority for a regular CSP is a field grade officer which
                      is usually a BN CDR. An O3 is not the approval authority for any CSP. If the
                      CSP is &gt;60 days, then the approval authority is the CG or an O6 that has
                      delegation authority.{' '}
                      <strong>Get justifications for disapprovals in writing.</strong>
                    </Typography>

                    <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                      If they do support the denial, your best solution is to fight so hard for your
                      CSP that it makes approving your CSP the path of least resistance for them.
                    </Typography>

                    <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                      STORY ABOUT HOW I LEARNED THIS:
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      I had to cause a ruckus to get my transition, CSP, and Reserve follow-on
                      schools requests processed. My Company Commander was incredible, but he forgot
                      to send my CSP or transition packets up, which cost me a couple of weeks. He
                      was an incredible leader, though, so he apologized and rectified the
                      situation. But then, my BN S1 didn't send up my CSP or transition documents to
                      the approval authority after multiple reminders.
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      With a week to go until I was supposed to move out of my apartment and leave
                      for Chicago for my CSP, I had no orders, no approved CSP, and no idea if I
                      could get a job after ETS or if I had follow-on Reserve schools. The straw
                      that broke the camel's back was when my S1 asked me to forge the O6 signature
                      on my CSP (which was going to JAG for approval by a lawyer and then
                      installation command). I asked my BN CDR to sign my CSP directly but didn’t
                      get anything back after a couple of weeks.
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      With one email to the CG about my problems with my transition to the Reserve,
                      and a call from my lawyer to the BDE JAG about my CSP, I got my packets
                      approved all the way to installation command and back in a couple of days. I
                      had politely been fighting fruitlessly on my own for so long. It takes
                      personal courage to advocate for yourself after being a team player for so
                      long.
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      My perception at the time was that I gave the Army almost 12 years of my life,
                      between the NG/USMA/RA, but my unit butchered my transition and then tried a
                      bit of retaliation when I got help. My BN XO called me, and among other
                      things, threatened retaliation. I documented and reported the retaliation to
                      my lawyer and BN CDR. It was all worth the fighting though.
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      I completed an incredible CSP, I got to move in with my wife in Chicago after
                      six years of a long-distance relationship, I have a VET TEC software engineer
                      bootcamp lined up, and I am executing a transition plan I am proud of.
                    </Typography>

                    <Typography sx={{ marginBottom: 2 }}>"Closed mouths don’t get fed"</Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      "The squeaky wheel gets the grease"
                    </Typography>

                    <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                      You earned these benefits, now go get them.
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
                      If your CSP packet gets mismanaged, you might find yourself in a position
                      where your DA 31, authorizing administrative absence from your unit, has been
                      approved but your CSP packet, authorizing you to work as an intern, is
                      disapproved or missing.
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      <strong>
                        You can get free legal service at your installation's Trial Defense Service
                      </strong>{' '}
                      (where the defense counsels hang out at your installation).{' '}
                      <strong>
                        You can also consult your BDE JAG, but realize that they work for your BDE
                        CDR,
                      </strong>{' '}
                      so that's equivalent to asking a prosecutor for legal help.
                    </Typography>

                    <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                      STORY ABOUT HOW I LEARNED THIS:
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      I found myself in the position described above. My CSP DA 31 was signed and
                      approved, but the rest of my CSP packet was still stuck in the bureaucracy a
                      week or so out from leaving. I told my CO leadership and BN S1 that I was
                      planning to leave anyway since it wasn't my fault they fumbled my transition.
                      But, I asked my lawyer to help me with that situation, since I was legally
                      authorized to leave but not legally authorized to do an internship.
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      It seemed that my unit was actually okay with letting me be gone, even if the
                      rest of my packet was not approved. What they didn't seem to like was how much
                      I was embarrassing them by reaching out to the CG and BDE JAG about the unit
                      messing up my transition paperwork. I was worried that if something went
                      wrong, my unit could throw the book at me for being AWOL and pretend like it
                      was my fault.
                    </Typography>
                    <Typography sx={{ marginBottom: 2 }}>
                      I did end up getting my CSP approved last minute, but I think my unit would
                      have let me be gone either way since I already had a signed DA 31.
                    </Typography>
                  </Box>
                </TimelineContent>
              </TimelineItem>
            </TimelineContent>
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
                    This is usually done at medical records at the installation hospital. It may
                    take 4-8 weeks for them to process your request. This is especially true for the
                    busy summer move cycle.
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
                      Start targeting specific companies and job titles to build a network where you
                      want to land.
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
                      Get a joint email/calendar manager like Microsoft Outlook, Google Calendar +
                      Gmail, or a meeting manager like{' '}
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
                    Usually, CIF will require an early turn-in memo signed by your commander to do
                    this (they usually keep example memos on site). If you are missing anything
                    during your first turn-in, you can find the item at a local surplus store and
                    then do a second turn-in when you get your installation clearing papers.
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
                      You need to move on or after the publication date on your orders or
                      transportation might not pay for your move.
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
                    to break leases. It is customary, but not a requirement, to give a 30-day
                    notice.{' '}
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
                    This is usually done at medical records at the installation hospital. It may
                    take 4-8 weeks for them to process your request. This is especially true for the
                    busy summer move cycle.
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
                    Attend your installation's ETS brief, if it is a requirement by your higher
                    headquarters' G1, as soon as possible.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    Sometimes these are done online, and sometimes they are done in person. Once
                    complete, you can send a request for orders packet with your ETS briefing
                    certificate to G1 through your BN S1. It will take G1 about 2 weeks to produce
                    orders for you.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    At Fort Moore, you fall under the DHR, so look at the box to get info on
                    speaking to G1.
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
                    Reach out to your G1 separations sections to get the most up-to-date information
                    on separation requirements. For Fort Moore, you can find their contact info{' '}
                    <Link
                      to="https://www.moore.army.mil/Garrison/DHR/Contacts.html"
                      target="_blank"
                      rel="noopener"
                    >
                      here
                    </Link>
                    .
                  </Typography>
                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    STORY ABOUT HOW I LEARNED THIS:
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    I was "waiting" for my orders for months without any believable updates from S1.
                    I went to my unit's G1, DHR at Fort Moore, and{' '}
                    <strong>
                      I found out that my packet was never submitted correctly by my BN S1 so they
                      weren't going to process it
                    </strong>
                    . That's because{' '}
                    <strong>they started requiring in-person ETS briefs THAT MONTH</strong>, after a
                    two-year break due to COVID, and I wouldn't get my orders unless I attended
                    their brief and received a certificate to put in my request for orders packet.
                    This info had not been communicated down to the soldier level by BN S1s.{' '}
                    <strong>
                      I had to drive 4 hours from a field training exercise to do the ETS brief at
                      Fort Moore, and then drive another 4 hours back to the field training
                      exercise.
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
                    Identify any vocational training you're interested in and apply for acceptance
                    in the program. Remember, you need to apply for VA benefits <strong>AND</strong>{' '}
                    apply for acceptance from the company.
                  </Typography>
                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    For VET TEC, find which company offers what you’re interested in, complete the
                    train-up programs, then apply for acceptance.
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
                    Look at the benefits of an in-person course vs remote. You will get less BAH for
                    remote vs in-person attendance. Let your social network know you'll be
                    unavailable during the BootCamp.{' '}
                    <strong>
                      To start the program, you need to be on terminal leave, DOD Skillbridge, or
                      have ETSed.
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
                    You'll need it to access VA resources, network, work from home, etc. There are
                    many resources out there on desktop vs laptop and{' '}
                    <Link
                      to="https://www.nytimes.com/wirecutter/reviews/best-laptops/"
                      target="_blank"
                      rel="noopener"
                    >
                      PC vs mac
                    </Link>
                    .
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    A desktop is more powerful. You can save even more money by{' '}
                    <Link
                      to="https://www.reddit.com/r/buildapc/wiki/index"
                      target="_blank"
                      rel="noopener"
                    >
                      building your own desktop
                    </Link>
                    . A laptop is less powerful, and usually more expensive per unit of computing
                    power, but is more portable. I recommend Apple MacBooks, especially a MacBook
                    Pro, if you want a laptop. Stay away from MacBook Airs if you have any plans to
                    use it for more than typical web browsing and streaming. MacBooks are more
                    expensive than PC laptops but include better integration with cell phones, a
                    more secure environment, and are a requirement if you want to do software
                    development.
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
                    . This is especially important if you want to pursue a work-from-home or remote
                    position.
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
                    Our curriculum, like the rest of the software engineering industry, is heavily
                    dependent on open-source software, which traditionally has not worked very well
                    on Windows platforms. For that reason, a computer running macOS or Linux is
                    greatly preferred. Windows users are required to either switch to a Mac or Linux
                    computer (preferred), or install Windows Subsystem for Linux 2 (WSL2). Windows
                    users should expect to spend extra time setting up WSL, installing an Ubuntu
                    dual boot configuration, and/or running Linux via a virtual machine (via Virtual
                    Box). Please note that the minimum specs below may not be suitable for running a
                    virtual machine.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    You should not use a computer that is actively shared with another person
                    (especially with another software developer) since you will be making
                    system-level configuration changes, and possibly deleting information that will
                    affect those users.
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
                      <Link
                        to="https://support.apple.com/en-us/HT211683"
                        target="_blank"
                        rel="noopener"
                      >
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
                      - We do not provide full instructional support for Windows users. Our staff
                      can assist with WSL2/Ubuntu-related issues but may be unable to troubleshoot
                      Windows-specific issues.
                    </Typography>
                    <Typography component="li" sx={{ marginBottom: 1 }}>
                      <strong>Acceptable Alternative:</strong> Ubuntu Linux (LTS minimum)
                    </Typography>
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    Note that Zoom and other communication apps, webcams, and microphones may be
                    buggy on Ubuntu. This is outside of the control of staff as they cannot support
                    debugging these issues.
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
                    Look up job conferences to attend in the upcoming months. Don't forget that you
                    can go to professional conventions or company-hosted conventions to network and
                    find employment (e.g., DEFCON, Blue Team CON).
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
                      Our hiring events connect military community job seekers from around the globe
                      with American businesses hiring for local, national, and remote opportunities.
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
                    If you're interested in the Reserve/Guard, make sure to explore available MOS,
                    benefits, bonuses, and locations you're interested in. The reserve career
                    counselors can send you a list to look through. You can also find the list of
                    open positions on{' '}
                    <Link
                      to="https://www.milsuite.mil/book/community/spaces/apf/s1net"
                      target="_blank"
                      rel="noopener"
                    >
                      milsuite in the S1 Net
                    </Link>
                    .
                  </Typography>
                </Box>
              </TimelineContent>
            </TimelineItem>
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
                    Submit your request for orders through your BN S1 who will then send it up to
                    get processed by your higher headquarters' G1 equivalent. At Fort Benning, you
                    can <strong>request your orders 90 days</strong> out from your terminal leave or
                    ETS date.{' '}
                    <strong>
                      You can also request your orders earlier for extenuating circumstances
                    </strong>
                    . Your orders are one of the key documents that will prevent you from moving
                    forward with clearing and moving.
                  </Typography>
                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    STORY ABOUT HOW I LEARNED THIS:
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    I was paying for my apartment outside of Fort Benning AND my wife's apartment in
                    Chicago while she was in residency. It made financial sense to move to Chicago
                    during my CSP so I requested my orders early. I had already briefed this as part
                    of my transition plan to my leadership.{' '}
                    <strong>
                      Transportation may not pay you if you move before the publication date on your
                      orders.
                    </strong>{' '}
                    My orders had a publication date before I moved to Chicago so I was able to get
                    paid for the move.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    You should begin applying to jobs, even if that is not your goal or you already
                    have something lined up. You will gain valuable experience during interviews.
                    Ask for feedback after an interview. Do an AAR with someone you know after every
                    interview.
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
                    to get inside info on things like the culture of a company, or what to expect
                    for compensation.
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
                      frameBorder="0"
                      scrolling="no"
                      style={{
                        width: '100%',
                        maxWidth: '800px', // Adjust width as needed
                        height: '741px', // Adjust height as needed
                        borderRadius: '8px',
                        boxShadow: '0px 4px 10px rgba(0, 0, 0, 0.1)'
                      }}
                    />
                  </Box>
                  <Typography sx={{ marginBottom: 2 }}>
                    You should also <strong>begin interviewing for jobs</strong> at this time. Most
                    companies don't want to entertain prospective applicants until they're 30-90
                    days out from their availability date.
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
                    It takes about 30 days to approve and receive your Certificate of Eligibility
                    (COE), so it's better to have it and not need it than to need it and not have
                    it.
                  </Typography>
                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    STORY ABOUT HOW I LEARNED THIS:
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    I found out about this too late. I missed out on being able to do VET TEC while
                    I was on terminal leave because I received the VA COE the weekend after the
                    course deadline for registration. That cost me about a two-month delay in
                    finding employment.
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
                    , complete the train-up, and take the entrance exam. It could take you 2 months
                    to do the train-up and multiple attempts to pass the assessment, so plan
                    accordingly.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    Look at all the different pros and cons of the companies.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    Going remote will give you less BAH than in person. But, going remote will allow
                    you to be anywhere you want to be.
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
                    If you haven't moved yet, 60-90 days out is the minimum amount of time you need
                    to start planning for your move. Schedule a transportation brief.{' '}
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
                    . If you're doing a bootcamp then apply after you graduate from the bootcamp.
                    Breakline is probably the best way to break into a tech company, even if you
                    don’t have a tech background. It's an incredible program and they teach you
                    interview/resume skills and introduce you to tech companies. Their mission is to
                    get veterans, minorities, and women into high-paying jobs at tech companies.
                  </Typography>
                </Box>
              </TimelineContent>
            </TimelineItem>
          </Timeline>
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
                    Fort Moore G1 won't process installation clearance paper requests until you've
                    completed CSP.
                  </Typography>
                </Box>
              </TimelineContent>
            </TimelineItem>
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
                    You can do this in person or via mail.{' '}
                    <strong>Submit per diem request to finance.</strong> The instructions are on the
                    packets you get when you clear those offices.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    You can calculate how much to expect from your PPM using calculators like the
                    one next to this tile.{' '}
                    <strong>You will get 95% of the cost to the government</strong> to pay
                    professionals to move you. The Army will pay you what it'd cost to get you from
                    your current duty station to your home of record at entry or the location you
                    entered service.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    <strong>
                      You need to get empty-weight and full-weight tickets to get paid for your
                      move.
                    </strong>{' '}
                    They'll give you the finer details in your transportation brief. For example, do
                    not include your personally owned vehicle as part of the net difference in
                    weight.{' '}
                    <strong>
                      Do not try to defraud the system. If you weigh it, then you must move it.
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
                      A comprehensive DITY move calculator for PCS moves, allowing the user to view
                      eligible allowances and estimate expenses and profit.
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
                      If you haven't done it already, you need to do your separation physical during
                      this period.
                    </strong>{' '}
                    If it's been more than 30 days since your Phase 1 physical, you will need to go
                    into your MEDPROS and add an update to your medical status as per the
                    instructions on your SHPE packet.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    Go to{' '}
                    <Link to="https://medpros.mods.army.mil/portal/" target="_blank" rel="noopener">
                      medpros
                    </Link>
                    , click on "periodic health assessments," go to the "SHPE" tab, and fill out a
                    medical update (I believe it's the DD Form 2807-1).
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
                    Make sure your final pay sheet at finance is accurate before you sign. For
                    example, make sure that you're{' '}
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
                    Don't forget that you'll need lodging and transportation if you've already moved
                    away from your installation. Costco provides a{' '}
                    <Link
                      to="https://www.costcotravel.com/Rental-Cars"
                      target="_blank"
                      rel="noopener"
                    >
                      rental car discount
                    </Link>
                    . You can save money by staying with a friend or even staying in your car.
                  </Typography>
                </Box>
              </TimelineContent>
            </TimelineItem>
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
                  <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                    Review your DD214 for accuracy.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    Make sure your ETS address is correct. Bring copies of awards, school records,
                    and an SRB/ORB. Correct everything on the spot and do not leave until it is
                    correct. The civilians are generally helpful. There are benefits tied to how
                    long you served, the character of your service, and what you did while you were
                    in. If you claim you did something, and it isn't on the DD214, then that's tough
                    luck for you.
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
                    Optimally, in a fire and water-resistant place. Make digital copies and email
                    them to yourself.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    <Link
                      to="https://dd214direct.com/whats-the-difference-between-a-member-4-copy-and-service-2-copy-dd214/"
                      target="_blank"
                      rel="noopener"
                    >
                      The member 4 is used at government agencies when applying for benefits. Member
                      1 is for civilian organizations.
                    </Link>
                  </Typography>
                  <Typography>
                    <strong>Upload your DD214 to the VA.</strong> It will go in as a supporting
                    document to your VA claim.
                  </Typography>
                </Box>
              </TimelineContent>
            </TimelineItem>
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
                      If you aren't gainfully employed, apply for unemployment at your local agency
                      as soon as possible. Unemployment is administered through your
                      state.Unemployment Compensation for Ex-Service members (UCX) provides
                      unemployment benefits to eligible workers who become unemployed through no
                      fault of their own, and meet certain other eligibility requirements, such as
                      being able to work, available for suitable full-time work, and actively
                      seeking work.
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
                    You have to do it in that order according to the Fort Benning Reserve officer
                    counselor.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    Once you "hit" your Army Reserve unit's books ensure that you inprocess, and
                    request a pay action and an Army Reserve DFAS account. Once you have your pay
                    setup through the USAR it'll show up in a dropdown menu on myPay.
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
                    for about 180 days after ETS. The personnel at the ETS brief said you need to
                    apply for these immediately after ETS. However, the website makes it seem like
                    it is applied automatically. It happened "automatically" for me and I was able
                    to verify it on milconnect. Do your own research and make sure you have coverage
                    before you need it.
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
                    These are two separate payments you'll receive. I put it off for about 5 months,
                    but I believe you have up to a year past your ETS date. I got paid around $5k
                    for PPM and about $500 for per diem.
                  </Typography>
                </Box>
              </TimelineContent>
            </TimelineItem>
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
                    Your first post-transition job might not be what you wanted, or how you thought
                    it was going to be. It's okay to job-hop.
                  </Typography>
                  <Typography sx={{ marginBottom: 2 }}>
                    Companies generally are more likely to pay higher compensation packages to new
                    hires than they are to give internal employees raises. Therefore,{' '}
                    <strong>job hopping is the easiest way to get a substantial raise</strong>.
                  </Typography>
                  <Typography>
                    If you can't meet the initial requirements to get hired into a company for your
                    target job, it is likely easier to transfer to that role as an internal
                    employee. For example, getting a software engineer role as an outside hire at
                    Amazon is rigorous, but moving to another team as an employee is easier.
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
                    You have benefits that last long after you ETS.
                  </Typography>
                  <Typography>
                    You learn about them during TAP. For example, you can use VET TEC a second time
                    if you need help transitioning to another job again as long as it's 18 months
                    between courses.
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
                      Veteran Service Officers help you navigate the VA. They help with gathering
                      information necessary to support a claim through the VA.
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
                      An accredited attorney, claims agent, or Veterans Service Organization (VSO)
                      representative can help you file a claim or request a decision review. Learn
                      how to find and appoint one of these types of accredited representatives to
                      help you.
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                      www.va.gov
                    </Typography>
                  </Box>
                  <Typography sx={{ marginTop: 2 }}>
                    You may need to fight for benefits. Don't hesitate to put up a fight to get the
                    benefits you deserve. The VA is a very bureaucratic system, but you have
                    government and non-governmental support. You can reach out to organizations like{' '}
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
                  <Typography>
                    If you lose your DD214, you can request a copy from{' '}
                    <Link
                      to="https://www.va.gov/records/get-military-service-records/"
                      target="_blank"
                      rel="noopener"
                    >
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
                    Give back to veterans.
                  </Typography>
                  <Typography>
                    You got a lot of help along the way. Do what you can to give back.
                  </Typography>
                </Box>
              </TimelineContent>
            </TimelineItem>
          </Timeline>
        </Box>
      </Container>
    </AppTheme>
  );
}
