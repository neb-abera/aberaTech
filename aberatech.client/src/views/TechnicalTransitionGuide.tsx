import React, { Suspense, useState } from 'react';
import AppAppBar from '../components/AppAppBar';
import Hero from '../components/Hero';
import Divider from '@mui/material/Divider';
import Typography from '@mui/material/Typography';
import CssBaseline from '@mui/material/CssBaseline';
import AppTheme from '../theme/AppTheme';
import Container from '@mui/material/Container';
import Box from '@mui/material/Box';
import Accordion from '@mui/material/Accordion';
import AccordionSummary from '@mui/material/AccordionSummary';
import AccordionDetails from '@mui/material/AccordionDetails';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { Link } from 'react-router';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemText from '@mui/material/ListItemText';
import { CardMedia } from '@mui/material';
import MachineLearningPathwayImage from '../assets/machine_learning_pathway.png';
import Dialog from '@mui/material/Dialog';
import CloseIcon from '@mui/icons-material/Close';
import IconButton from '@mui/material/IconButton';

// Fallback loading spinner or placeholder
const LoadingFallback = () => <div>Loading...</div>;

export default function TechnicalTransitionGuide(props: { disableCustomTheme?: boolean }) {
  // Maintain open state for multiple accordions (optional)
  const [expanded, setExpanded] = useState<string | false>(false);

  const handleAccordionChange = (panel: string) => (_: React.SyntheticEvent, isExpanded: boolean) => {
    setExpanded(isExpanded ? panel : false);
  };

  const [open, setOpen] = useState(false);
  const handleOpen = () => setOpen(true);
  const handleClose = () => setOpen(false);

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
          Everything I learned transitioning from the Army to Software Development
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
            The target audience for this are people in the military that are interested in working in tech, but don't
            know how to program. I made these guides during my transition from the Army to Software Development. The
            more I learn, the more I'll update. I embedded links, documents, and videos. I encourage you to click on
            those to ensure that you understand the topic in its breadth and depth. This is a work in progress so feel
            free to make send me questions, comments, and recommendations.{' '}
            <Link to="https://github.com/neb-abera/transition" target="_blank" rel="noopener">
              Everything I learned transitioning from the Army to Software Development
            </Link>
          </Typography>
          <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
            There are so many incredible resources out there to help transition people into tech. But, the enormity of
            resources can give people "paralysis by analysis" as they try to decide which option to pick. This document
            below is meant to answer questions like:
          </Typography>
          <List>
            <ListItem>
              <ListItemText primary="How do I learn to program?" />
            </ListItem>
            <ListItem>
              <ListItemText primary="Which programming language should I learn?" />
            </ListItem>
            <ListItem>
              <ListItemText primary="How do I transition to a job in software development, information technology, or cyber?" />
            </ListItem>
          </List>
          <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
            Here is a good intro video from Andy Sterkowitz on some of the topics I will cover below.
          </Typography>
          <CardMedia
            component="iframe"
            title="test"
            src="https://www.youtube.com/embed/ueXjGMrmn8k"
            sx={{ width: '100%', height: 'auto', aspectRatio: '16/9', marginBottom: 2 }}
          />
        </Box>
        <Typography variant="h2" component="h2" sx={{ color: 'text.primary' }}>
          My recommendation on how to learn to program
        </Typography>
        {/* Suspense-wrapped lazy-loaded components */}
        <Accordion expanded={expanded === 'panel1'} onChange={handleAccordionChange('panel1')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>1. Learn the fundamentals of programming</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                <Typography variant="body1" gutterBottom>
                  There's really no wrong way to learn the fundamentals of programming. Just pick one and stick with it.
                  However, I recommend that you focus on doing and ensure that you don't get stuck in "tutorial hell."
                  Tutorial Hell is what happens when you passively watch tutorials and don't learn much, most likely
                  because you aren't learning by doing, and will iteratively start and stop tutorials in perpetuity.
                </Typography>
                <List>
                  <ListItem>
                    <ListItemText
                      primary={
                        <>
                          <Link
                            to="https://github.com/ossu/computer-science#intro-cs"
                            target="_blank"
                            rel="noopener noreferrer"
                          >
                            Open Source Society's Intro to Programming and Intro to Computer Science
                          </Link>{' '}
                          I think this course is a good program and what I would recommend to people with no programming
                          experience and who don't want to pay for a course. It is free, has a beginner-friendly
                          structure, is taught in Python, and has a good roadmap for future topics. I disagree with how
                          OSCS requires discrete math BEFORE data structures and algorithms. Math like discrete math and
                          linear algebra are great to study once you're ready to do more than{' '}
                          <Link
                            to="https://www.codecademy.com/article/what-is-crud"
                            target="_blank"
                            rel="noopener noreferrer"
                          >
                            CRUD{' '}
                          </Link>
                          operations. Make sure you stay actively engaged by doing the exercises, problem sets, etc.
                          It's easy to think you can learn from just watching the videos.. you can't. You learn by doing
                          things repeatedly
                        </>
                      }
                    />
                  </ListItem>
                  <ListItem>
                    <ListItemText
                      primary={
                        <>
                          <Link
                            to="https://www.hackreactor.com/prep-programs"
                            target="_blank"
                            rel="noopener noreferrer"
                          >
                            The prep course from Hack Reactor
                          </Link>{' '}
                          This is a good free alternative to the first course if you're okay with learning JavaScript. I
                          like it because it is free, forces repetition, and has one path to completion. I think prep
                          courses from boot camps are valuable because these companies know how to teach beginners to
                          become software developers. They do what they know works. I also believe that the repetition
                          they provide is essential to the learning process. This program forces you to get the
                          repetitions in. There is some instruction, you try stuff on your own, and then there's a video
                          to watch if you get lost and need help. It is taught in JavaScript so it's only useful if
                          you'd like to learn or work in web development.
                        </>
                      }
                    />
                  </ListItem>
                  <ListItem>
                    <ListItemText
                      primary={
                        <>
                          <Typography variant="body1" gutterBottom>
                            There are many exceptional intro programming courses for sale on{' '}
                            <Link
                              to="https://www.udemy.com/courses/search/?q=programming&src=sac&kw=progr"
                              target="_blank"
                              rel="noopener"
                            >
                              Udemy
                            </Link>{' '}
                            such as{' '}
                            <Link
                              to="https://www.udemy.com/course/beginning-c-plus-plus-programming/"
                              target="_blank"
                              rel="noopener"
                            >
                              Beginning C++ Programming - From Beginner to Beyond
                            </Link>{' '}
                            or{' '}
                            <Link
                              to="https://www.udemy.com/course/cpp-4skills/?amp=&aff_code=Ewh3Y1lWQH8FQR93MkBPbG1RGXFfW1h8B14ZeU5TQ3YBRxFzWj5XMRM%3D&pmtag=CAREERS24LEARN15&utm_campaign=careers24octlaunch&utm_medium=web"
                              target="_blank"
                              rel="noopener"
                            >
                              Mastering 4 Critical SKILLS using C++ 17
                            </Link>{' '}
                            by Dr. Moustafa Saad Ibrahim.
                          </Typography>
                          <Typography variant="body1" gutterBottom>
                            The first course focuses on long instruction and limited practice, while the second is short
                            on instruction and focused on practice and problem solving. I like Dr. Moustafa's course the
                            most out of all intro courses since it provides the best problem solving foundation. He also
                            provides follow-up courses on data structures, algorithms, coding interviews, and runs
                            competitive programming instruction.
                          </Typography>
                        </>
                      }
                    />
                  </ListItem>
                  <ListItem>
                    <ListItemText
                      primary={
                        <>
                          <Link to="https://cs50.harvard.edu/x/2022/" target="_blank" rel="noopener noreferrer">
                            Harvard's CS50x
                          </Link>{' '}
                          - An incredible but challenging course covering a variety of languages and data structures.
                        </>
                      }
                    />
                  </ListItem>
                </List>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
        <Accordion expanded={expanded === 'panel2'} onChange={handleAccordionChange('panel2')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>
              2. Include dedicated coding and problem-solving training in your schedule (daily repetition is best)
            </Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                <Typography variant="body1" gutterBottom>
                  I recommend{' '}
                  <Link to="https://www.codewars.com/" target="_blank" rel="noopener">
                    Codewars
                  </Link>{' '}
                  to all beginners for daily practice.{' '}
                  <Link to="https://www.hackerrank.com/dashboard" target="_blank" rel="noopener">
                    HackerRank
                  </Link>{' '}
                  is a good option if you're still trying to learn the syntax of a language. Codewars is a free website
                  that will give you problems to solve based on your skill level and the language you want to get better
                  at. They also have solutions posted and a forum to discuss stuff. It’s also fun for kids because you
                  get a “rank” and it’s fun to level up.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  Practicing a little bit every day is better than practicing a lot not very often.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  I recommend setting aside 5x60 minutes sessions a week. This is my methodology for training:
                </Typography>
                <Typography variant="body1" component="ol" gutterBottom>
                  <li>Set a clock for 20 minutes</li>
                  <li>Try to solve the problem with limited use of documentation</li>
                  <li>Compare my solution to other solutions provided</li>
                  <li>If there's a significantly better way to solve the problem, then I learn that pattern.</li>
                  <li>Redo the problem until I can do it with no documentation or outside assistance</li>
                  <li>Find a different problem and restart this process until I'm done with the training session</li>
                </Typography>
                <Typography variant="body1" gutterBottom>
                  You need to use sites to train your problem-solving and programming language-specific skills:
                </Typography>
                <Typography variant="body1" component="ul" gutterBottom>
                  <li>
                    <Link to="https://www.codewars.com/" target="_blank" rel="noopener">
                      Codewars
                    </Link>{' '}
                    is great for beginners to rank up and is free
                  </li>
                  <li>
                    <Link to="https://edabit.com/" target="_blank" rel="noopener">
                      Edabit
                    </Link>{' '}
                    has a lot of good problems to practice, but lacks the structure of Codewars and I used up the free
                    trial in like two days
                  </li>
                  <li>
                    <Link to="https://leetcode.com/" target="_blank" rel="noopener">
                      LeetCode
                    </Link>{' '}
                    but you'll need to finish a DSA course before you'll be able to solve most of the problems. It's
                    free. I only recommend getting a subscription if you're REALLY interested in knowing what companies
                    focus on specific DSA problems.
                  </li>
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>

        <Accordion expanded={expanded === 'panel3'} onChange={handleAccordionChange('panel3')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>3. Take a data structures and algorithms (DSA) course</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                <Typography variant="body1" gutterBottom>
                  If you can afford it, Abdul Bari's{' '}
                  <Link to="https://www.udemy.com/course/datastructurescncpp/" target="_blank" rel="noopener">
                    Mastering Data Structures & Algorithms using C and C++
                  </Link>{' '}
                  is an incredible combo with Dr Moustafa Saad Ibrahim's{' '}
                  <Link to="https://www.udemy.com/course/dscpp-skills/" target="_blank" rel="noopener">
                    data structures
                  </Link>
                  ,{' '}
                  <Link to="https://www.udemy.com/course/skills-algorithms-cpp/" target="_blank" rel="noopener">
                    algorithms part 1
                  </Link>{' '}
                  and{' '}
                  <Link to="https://www.udemy.com/course/skills-algorithms-cpp2/" target="_blank" rel="noopener">
                    part 2
                  </Link>
                  , and course for{' '}
                  <Link to="https://www.udemy.com/course/skills-coding-interviews/" target="_blank" rel="noopener">
                    technical interview problems
                  </Link>
                  . Abdul Bari's course provides in-depth instruction with little problem solving, while Dr Moustafa's
                  has concise instruction and lots of problem solving.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  Another option is getting{' '}
                  <Link
                    to="https://www.amazon.com/Grokking-Algorithms-illustrated-programmers-curious/dp/1617292230"
                    target="_blank"
                    rel="noopener"
                  >
                    Grokking Algorithms
                  </Link>{' '}
                  by Aditya Bhargava and pairing that with{' '}
                  <Link to="https://structy.net/" target="_blank" rel="noopener">
                    Structy
                  </Link>
                  . The book is illustrated, programmed in Python, and intuitive. Structy provides video instruction and
                  an environment to code DSA in Python, JavaScript, and C++. Structy is a paid subscription by my
                  favorite programming instructor. The website has videos, guides, lets you practice DSA problems, and
                  has solutions all in one place. I found it very worth it. However, there is only one problem per topic
                  so it lacks the depth of some of the other options.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  There are plenty of free resources online to learn DSA. You can get a free account on Structy and then
                  just YouTube/Google the section title for free resources. I needed the easy access, accountability,
                  and structure of the book and course to learn.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  I think DSAs should be taught in a separate course from intro to programming. I think people should be
                  comfortable with the fundamentals of programming in one language before they give bandwidth to DSA.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  If you want a follow-up DSA text, or just want a more robust reference, then I recommend the{' '}
                  <Link
                    to="https://www.amazon.com/Introduction-Algorithms-fourth-Thomas-Cormen/dp/026204630X/ref=pd_lpo_3?pd_rd_i=026204630X&psc=1"
                    target="_blank"
                    rel="noopener"
                  >
                    Algorithms
                  </Link>{' '}
                  textbook.
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>

        <Accordion expanded={expanded === 'panel4'} onChange={handleAccordionChange('panel4')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>4. Learn to think like a Software Engineer</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                <Typography variant="body1" gutterBottom>
                  Thinking like a programmer is the most important part of learning to program. That’s why no matter
                  what route you take in programming, you’ll learn the skills of a programmer. That's because memorizing
                  programming language syntax does not make you a programmer; applying the problem-solving mindset is
                  what makes you a programmer.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  I like{' '}
                  <Link to="https://youtu.be/9RkUYObX8us?t=1018" target="_blank" rel="noopener">
                    Hack Reactor's problem solving methodology
                  </Link>
                  . I've added my own additions in parentheses:
                </Typography>
                <Typography variant="body1" component="ol" gutterBottom>
                  <li>Interpret the prompt</li>
                  <li>IOCE - Inputs, outputs, constraints, edge cases</li>
                  <li>Write Tests</li>
                  <li>
                    High Level Strategy(s) (& break the problem down into easier-to-tackle portions)
                    <ul>
                      <li>Optional: Make a Diagram (can "whiteboard" the problem. I like to use pen and a notebook)</li>
                    </ul>
                  </li>
                  <li>Pseudocode the Strategy (comment pseudocode directly into the program)</li>
                  <li>Implement the Solution</li>
                </Typography>
                <Typography variant="body1" gutterBottom>
                  I also add the following portions when I'm studying:
                </Typography>
                <Typography variant="body1" component="ol" start={8} gutterBottom>
                  <li>
                    Try your plan.
                    <ul>
                      <li>Try not to use any outside resources</li>
                      <li>
                        If you get stuck for a reasonable amount of time, then allow yourself to use documentation
                      </li>
                      <li>
                        If documentation doesn't help, then as a last resort research what the best way to do each
                        technique is
                      </li>
                    </ul>
                  </li>
                  <li>
                    If your solution worked:
                    <ul>
                      <li>
                        Look up to see if there were better ways to do it and re-try the problem using the optimal
                        techniques
                      </li>
                      <li>See what the best people in the field are doing</li>
                    </ul>
                  </li>
                  <li>If your solution didn't work, then go back to step 1</li>
                </Typography>
                <Typography variant="body1" gutterBottom>
                  <Link to="https://www.youtube.com/watch?v=azcrPFhaY9k" target="_blank" rel="noopener">
                    Andy Harris has a long, but thorough, video on this topic
                  </Link>
                  . You can also search for other videos on the topic. Don't feel compelled to watch the video; you can
                  always come back to it.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  I had never read{' '}
                  <Link
                    to="https://www.freecodecamp.org/news/how-to-think-like-a-programmer-lessons-in-problem-solving-d1d8bf1de7d2/"
                    target="_blank"
                    rel="noopener"
                  >
                    this website
                  </Link>
                  , until I made this document, but they say the same thing I just did.
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>

        <Accordion expanded={expanded === 'panel5'} onChange={handleAccordionChange('panel5')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>5. Learn how to search for solutions</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                <Typography variant="body1" gutterBottom>
                  You need to learn how to effectively search for the answer to your questions. The sum of human
                  knowledge is at your fingertips, via a phone or computer; you just need to know how to sort and
                  analyze relevant information. Being good at googling is a skill you should develop. You should always
                  see what the best or most recommended way to do something is. Whether you’re buying a car, looking at
                  how to learn a programming language, or solving a coding problem at an interview, it’s all the same
                  problem-solving skill.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  There are different ways of using{' '}
                  <Link
                    to="https://www.lifehack.org/articles/technology/20-tips-use-google-search-efficiently.html"
                    target="_blank"
                    rel="noopener"
                  >
                    Google search
                  </Link>{' '}
                  to its full potential. I like to{' '}
                  <Link
                    to="https://techcrunch.com/2022/09/28/google-search-now-queries-reddit-and-quora-in-response-to-open-ended-questions/"
                    target="_blank"
                    rel="noopener"
                  >
                    use Google to search Reddit and Quora
                  </Link>{' '}
                  for the answers I'm looking for. Adding "Reddit" or "Quora" to your search terms, and looking for the
                  most recent posts with the most discussion, will usually filter out the spam. The most upvoted answers
                  are usually the best. The dissenting opinions are also important to understand.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  Most of my search results on any given day are probably "best ______ Reddit" or "best ______ Quora."
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>

        <Accordion expanded={expanded === 'panel6'} onChange={handleAccordionChange('panel6')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>6. Be a curious and consistent learner</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                <Typography variant="body1" gutterBottom>
                  Continue to develop your problem-solving skills, and study follow-on topics that interest you or that
                  you want to work in.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  I recommend using{' '}
                  <Link to="https://teachyourselfcs.com/" target="_blank" rel="noopener">
                    Teach Yourself CS
                  </Link>{' '}
                  if you are a self-taught programmer.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  Programming is just like any other skill. You're only good at what you repeatedly do. Find what works
                  best for you to understand a topic. You should use as many resources as it takes to find the best way
                  to make a topic click for you. You can register for online courses, watch YouTube videos, get
                  textbooks, read forums, etc.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  You can get ideas on what to study from roadmaps like{' '}
                  <Link to="https://roadmap.sh/python" target="_blank" rel="noopener">
                    roadmap.sh
                  </Link>{' '}
                  until you know what you're doing. I also like the{' '}
                  <Link to="https://github.com/ossu/computer-science#intro-cs" target="_blank" rel="noopener">
                    Open Source Computer Science Bachelors
                  </Link>{' '}
                  for ideas on what I should study. I disagree with how OSCS requires discrete math BEFORE data
                  structures and algorithms. Math like discrete math and linear algebra are great to study once you're
                  ready to do more than{' '}
                  <Link to="https://www.codecademy.com/article/what-is-crud" target="_blank" rel="noopener">
                    CRUD operations
                  </Link>
                  .
                </Typography>
                <Typography variant="body1" gutterBottom>
                  AVOID PASSIVE "LEARNING" - This is one of the most common mistakes people make when they try to learn
                  to program. You should spend more time "doing" than passively learning how to do something. Usually
                  spending &le;30% of your time reading/watching and &ge;70% coding.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  Here are two videos on mistakes beginners make. The first is from{' '}
                  <Link to="https://www.youtube.com/watch?v=T7aSI-E1fCE" target="_blank" rel="noopener">
                    Aaron Jack
                  </Link>{' '}
                  and the second is a video from{' '}
                  <Link to="https://www.youtube.com/watch?v=bVKHRtafgPc" target="_blank" rel="noopener">
                    William Lin
                  </Link>{' '}
                  on competitive programming, but the principles still apply.
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>

        <Accordion expanded={expanded === 'panel7'} onChange={handleAccordionChange('panel7')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>7. Follow talented content producers</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                <Typography variant="body1" gutterBottom>
                  Here are some of my favorite content producers:
                </Typography>
                <Typography variant="body1" component="ul" gutterBottom>
                  <li>
                    Alvin Zablan on{' '}
                    <Link to="https://www.youtube.com/c/AlvintheProgrammer" target="_blank" rel="noopener">
                      YouTube
                    </Link>
                    . He is my favorite instructor on freeCodeCamp.org and he also owns{' '}
                    <Link to="https://structy.net" target="_blank" rel="noopener">
                      structy.net
                    </Link>
                    .
                  </li>
                  <li>
                    <Link to="https://www.youtube.com/@ColinGalen" target="_blank" rel="noopener">
                      Colin Galen
                    </Link>{' '}
                    and{' '}
                    <Link to="https://www.youtube.com/@tmwilliamlin168" target="_blank" rel="noopener">
                      William Lin
                    </Link>{' '}
                    are talented competitive programmers.
                  </li>
                  <li>
                    Clement Mihailescu on{' '}
                    <Link to="https://www.youtube.com/channel/UCaO6VoaYJv4kS-TQO_M-N_g" target="_blank" rel="noopener">
                      YouTube
                    </Link>
                    . He went from having never coded to getting hired at Google in 6 months. He also owns{' '}
                    <Link to="https://algoexpert.io" target="_blank" rel="noopener">
                      AlgoExpert
                    </Link>
                    .
                  </li>
                  <li>
                    Alexander Nguyen on{' '}
                    <Link to="https://www.linkedin.com/in/alxngu/" target="_blank" rel="noopener">
                      LinkedIn
                    </Link>{' '}
                    and{' '}
                    <Link to="https://alexcancode.medium.com/" target="_blank" rel="noopener">
                      Medium
                    </Link>
                    . He is a talented software engineer that publishes about how to get hired at tech companies.
                  </li>
                  <li>
                    freeCodeCamp - Tons of free courses and videos on their{' '}
                    <Link to="https://www.youtube.com/c/Freecodecamp" target="_blank" rel="noopener">
                      YouTube
                    </Link>{' '}
                    and{' '}
                    <Link to="https://www.freecodecamp.org/" target="_blank" rel="noopener">
                      website
                    </Link>
                    .
                  </li>
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
        <Typography variant="h2" component="h2" sx={{ color: 'text.primary' }}>
          Frequently asked questions
        </Typography>
        <Accordion expanded={expanded === 'panel8'} onChange={handleAccordionChange('panel8')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>What programming languages should you learn</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                <Typography variant="body1" gutterBottom>
                  Once you learn your first programming language, you’ll find that you need to learn more programming
                  languages because you need better-suited tools for the tasks you're trying to accomplish. Just
                  remember that specialization is more important, will get you better paid, and will make you a better
                  programmer. You should have one or two languages you are an expert at, and then one or two that you
                  are familiar with. I recommend learning the following languages so that you’re well-rounded:
                </Typography>
                <Typography variant="body1" component="ol" gutterBottom>
                  <li>
                    <strong>Python</strong>: a high-level general-purpose language.
                    <ul>
                      <li>
                        <strong>Pros:</strong> Its syntax is similar to English, so it’s very natural to read and write
                        in. It is very easy to create programs quickly in Python. There are a lot of job opportunities.
                        It is very well supported. It is a growing language. It’s used in software development, data
                        science, artificial intelligence, and cyber security.
                      </li>
                      <li>
                        <strong>Cons:</strong> It is a high-level, interpreted, dynamically typed language, so you’ll
                        need another language if you want to work in high-performance, embedded, or systems programming.
                        It is a “slow” language compared to languages like C++.
                      </li>
                    </ul>
                  </li>
                  <li>
                    <strong>JavaScript</strong>: the dominant language of web development (only if you're interested in
                    web development or a coding boot camp).
                    <ul>
                      <li>
                        <strong>Pros:</strong> Web development is the easiest way to get into high-paying software
                        engineer jobs.
                      </li>
                      <li>
                        <strong>Cons:</strong> It isn’t really useful for anything else. You can use frameworks to
                        create applications using JavaScript, but they will be slower/worse than their native
                        counterparts.
                      </li>
                    </ul>
                  </li>
                  <li>
                    <strong>Other languages</strong>:
                    <Typography variant="body1" gutterBottom>
                      You'll need a more performant programming language if you need to do work that requires serious
                      performance or memory management. If you{' '}
                      <Link to="https://www.youtube.com/watch?v=9U684GbFST4" target="_blank" rel="noopener">
                        don't know what you should learn
                      </Link>
                      , I recommend that you learn C++. It is widely applicable, widely supported, and has a vast
                      ecosystem of resources for learning it. If you're interested in competitive programming, then I
                      especially recommend C++. At this point, you’ll have already learned one or two programming
                      languages, so picking up additional ones will be much easier for you.
                    </Typography>
                    <Typography variant="body1" gutterBottom>
                      I prefer statically typed and compiled languages that offer access to low-level operations and
                      resource management. I think the greatest combination of languages to get the ability to do the
                      most while knowing the least amount of languages is C++,{' '}
                      <Link to="https://dotnet.microsoft.com/en-us/languages/csharp" target="_blank" rel="noopener">
                        C# through .Net
                      </Link>
                      , TypeScript rather than JavaScript, and Python.
                    </Typography>
                  </li>
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
        <Accordion expanded={expanded === 'panel9'} onChange={handleAccordionChange('panel9')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>How to stay focused on learning to program</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                <Typography variant="body1" gutterBottom>
                  There's really no best way to learn software development. It’s a wide field, with tons of sub-fields,
                  and a lot of information. Here are some important tips I’ve found:
                </Typography>
                <Typography variant="body1" component="ol" gutterBottom>
                  <li>
                    <strong>Staying organized:</strong> I had to start journaling key takeaways so that I don’t get
                    lost, storing stuff in bookmarks, using Outlook calendar, etc. I recommend that you download a joint
                    email/calendar manager like Microsoft Outlook, get a meeting manager like{' '}
                    <Link to="https://calendly.com/" target="_blank" rel="noopener">
                      Calendly
                    </Link>{' '}
                    to sync to your calendar, and then sync both of those to a video conferencing site like Zoom. That
                    way you can automatically update your calendar with important meetings/reminders and seamlessly
                    send/receive video conferencing requests.
                  </li>
                  <li>
                    <strong>Making and sticking to a plan:</strong> It’s easy to get distracted, so I found that I need
                    accountability to stay on track. I got an agenda and forced myself to follow a daily routine, turned
                    on “screen time” and notification limits on my phone and computer, signed up for classes that have
                    structure, found friends online, and set appointments so I had a deadline to force myself to get
                    better by. You have your kid, so that’s a good accountability buddy.
                  </li>
                  <li>
                    <strong>Learning the best way you learn:</strong> I’ve found that I learn best from textbooks and
                    using code-grind websites to solve problems (like CodeWars or LeetCode). If you’re going to use
                    code-grind websites, set a 20-minute timer and try to solve the problem with no help besides looking
                    at the documentation. Then look at the solution afterward and look up anything you didn’t know. I’ve
                    found that I learn best by working through a textbook and then YouTubing anything I don’t understand
                    right away. I buy textbooks because I like knowing the “why” behind stuff, and I can bookmark it and
                    reference it later. The danger with videos/books is that you can think you “know” how to do
                    something by watching them. Don’t fall into that trap.
                  </li>
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
        <Accordion expanded={expanded === 'panel10'} onChange={handleAccordionChange('panel10')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>How do you set your computer up for programming</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                <Typography variant="body1" gutterBottom>
                  I recommend just using a free flagship IDE like Visual Studio Community or XCode for Apple until you
                  know enough to move on. If I'm not using Visual Studio Community, I like to use{' '}
                  <Link to="https://code.visualstudio.com/" target="_blank" rel="noopener">
                    Visual Studio Code
                  </Link>{' '}
                  with a whole host of extensions such as{' '}
                  <Link
                    to="https://www.google.com/search?q=visual+studio+code+extensions+reddit&sxsrf=ALiCzsZjFzulwX0oGGxnimjVpOUIIj0ErA%3A1665338511638&ei=jwxDY8KdJsyh5NoPubiF4A8&ved=0ahUKEwiCoaqz3dP6AhXMEFkFHTlcAfwQ4dUDCA4&uact=5&oq=visual+studio+code+extensions+reddit&gs_lcp=Cgdnd3Mtd2l6EAMyBQgAEIAEMgYIABAeEBYyBggAEB4QFjIFCAAQhgMyBQgAEIYDMgUIABCGAzIFCAAQhgM6CggAEEcQ1gQQsAM6BwgAELADEEM6DQguEMcBENEDELADEEM6DQgAEOQCENYEELADGAE6BAgAEENKBAhNGAFKBAhBGABKBAhGGABQ3gpYihhggRloAXABeACAAV-IAbAEkgEBN5gBAKABAcgBEcABAdoBBggBEAEYCQ&sclient=gws-wiz"
                    target="_blank"
                    rel="noopener"
                  >
                    these
                  </Link>
                  .
                </Typography>
                <Typography variant="body1" gutterBottom>
                  Use{' '}
                  <Link to="https://repl.it" target="_blank" rel="noopener">
                    repl.it
                  </Link>{' '}
                  to practice programming if you're intimidated by setting up a text editor or integrated development
                  environment.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  The intro material you choose will give you instructions on what they recommend to do for setup. Here
                  is a list of{' '}
                  <Link to="https://www.reddit.com/r/learnprogramming/wiki/tools" target="_blank" rel="noopener">
                    common text editors and integrated development environments (IDEs)
                  </Link>
                  .
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
        <Accordion expanded={expanded === 'panel11'} onChange={handleAccordionChange('panel11')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>How can kids learn to program</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                <Typography variant="body1" gutterBottom>
                  If you have smaller kids, then I recommend getting them to try the recommendations below. You can
                  graduate them to the recommendations above when they've shown interest and seem ready. The tough part
                  of teaching kids is keeping them engaged and focused on one task for a set period of time. These
                  websites try to make the learning process fun for kids. So, I think they are worth a shot. But, they
                  don't teach actual coding, so they will eventually have to switch to something like the above
                  recommendations. Programming involves sitting at a computer and problem-solving for extended periods
                  of time. There's really no way around that, and some kids may not be into that. Teenagers may be ready
                  to just jump into the recommendations above, but you can have them try the suggestions below to gauge
                  their interest.
                </Typography>
                <Typography variant="body1" component="ul" gutterBottom>
                  <li>
                    <Link to="https://codecombat.com/" target="_blank" rel="noopener">
                      CodeCombat
                    </Link>{' '}
                    is a game that includes coding. It seems like a fun way to get kids interested in programming. It's
                    free but it may bug you to upgrade periodically.
                  </li>
                  <li>
                    <Link to="https://scratch.mit.edu/" target="_blank" rel="noopener">
                      Scratch
                    </Link>{' '}
                    is MIT's attempt to appeal to kids. Kids can learn to program by dragging and dropping stuff to make
                    stories and programs. It isn't as gamified as CodeCombat but still seems like it would be fun for
                    kids.
                  </li>
                  <li>
                    <Link to="https://www.pygame.org/news" target="_blank" rel="noopener">
                      PyGame
                    </Link>{' '}
                    is great for kids, teens, or even adults interested in making simpler video games. This one requires
                    more setup though, so you'll probably have to help them out in the early stages.
                  </li>
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
        <Accordion expanded={expanded === 'panel12'} onChange={handleAccordionChange('panel12')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>
              My subjective opinion on working in three popular areas in tech (software development, IT, cyber)
            </Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                <Typography variant="body1" gutterBottom>
                  <strong>1. Software Development:</strong> Develop and maintain software products. This field is
                  usually a money maker for companies, so it is treated well by corporate leadership. Software
                  developers are typically highly paid and their roles are resistant to employment downturns. However,
                  this career requires constant study and improvement to stay on top of trends. Not every company has
                  software engineers, but the field is growing exponentially, and there is a significant demand for
                  talent.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  <strong>2. Information Technology:</strong> A support role that ensures the electronic “plumbing” of a
                  company, like the network, remains operational. IT is often seen as a "cost" depending on the company
                  culture. In companies that do not value IT, compensation may be lower. Unlike software development,
                  every company has IT needs. However, this field has somewhat stagnated in recent years.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  <strong>3. Cybersecurity:</strong> Protecting a company's network from infiltration (blue team) or
                  testing defenses by attempting to break in (red team). Cybersecurity roles usually offer higher pay
                  than IT since it is a subspecialty requiring additional knowledge. Not all companies have dedicated
                  cybersecurity teams, and some only prioritize it after experiencing an attack. Despite this,
                  cybersecurity is a rapidly growing field with an increasing demand for talent.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  Here is a{' '}
                  <Link to="https://lensdump.com/i/iHcJHP" target="_blank" rel="noopener">
                    general career roadmap
                  </Link>{' '}
                  in the industry.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  You'll eventually need to figure out what field of computer science you want to work in. This is
                  important because specialization is what gets you paid, but there are countless sub-fields,
                  programming tools/languages, companies, etc. I recommend looking up the various fields and finding out
                  what interests you. In the picture below, there are countless areas of specialization in each job
                  title.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  If you don't know what to pick, you can just apply to a wide breadth of jobs and take whatever you
                  get. You can worry about specialization after you figure out what you do and don't like at your first
                  job.
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
        <Accordion expanded={expanded === 'panel13'} onChange={handleAccordionChange('panel13')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>
              What matters most for getting a Software Engineering job? Do certifications, degrees, or skills matter for
              getting a job in the industry
            </Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                <Typography variant="body1" gutterBottom>
                  For jobs in computer science fields, the most important things are:
                </Typography>
                <Typography variant="body1" component="ul" gutterBottom>
                  <li>Relevant job experience using the software stack and skills they are hiring for</li>
                  <li>Years of experience in that specific field using adjacent stacks and skills</li>
                  <li>Years of experience in a general computer science field</li>
                  <li>
                    Passing the hiring assessments (usually they use sites like “LeetCode” or their own assessment)
                  </li>
                  <li>
                    Government organizations, contractors, and some companies also give veteran's preference, especially
                    if they have security clearances (since it saves them time and tens of thousands of dollars).
                  </li>
                  <li>
                    Networking and personability. Join professional organizations, attend conferences, attend local
                    meetups, etc. This will help build your network.
                  </li>
                </Typography>
                <Typography variant="body1" gutterBottom>
                  You don't need a degree in computer science, IT, or cyber to get into those fields. The degree, and
                  some certs, will matter most when you're looking for your first role in the industry. The degree will
                  help you get your foot in the door, make you more competitive than your peers, probably give you
                  relevant skills, and teach you “how to learn” and how you learn best. Getting a degree is great and
                  makes you more competitive, but isn’t a requirement.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  Once you learn how to program, it’ll take you about 3-6 months, if you're dedicated, to learn data
                  structures and algorithms well enough to pass “hard” level problems on LeetCode, which is what the
                  MAANG companies require. If you want a regular web development job, you won’t even need to master data
                  structures and algorithms. You could be employable after learning how to program with 6-12 months of
                  practice. Some companies are willing to hire beginners and train them as junior developers, so you
                  might get a job quickly after learning how to program.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  The key thing to know is that in these fields, what pays is experience. The better you are at your
                  job, no matter what field, the more you’ll make. The top-level IT professionals make just as much, if
                  not more, than software engineers. The important thing is to pick something that interests you, or
                  that you could stand doing, and the money will come.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  There aren’t really any certs worth their weight in software engineering. In IT and cyber, there are
                  certs that will help since they certify the knowledge you have, but they are not a replacement for it.
                  However, certs are good for use as a study guide for what skills you need to learn. I used to use a
                  specific site (the link is broken, but I’ve sent them a message). To answer your question, it depends
                  on what part of software development you’re interested in. But generally, no one cares about certs
                  past the first software development job. Certs and degrees will help candidates get their first
                  entry-level job. After that, companies just look for years of experience and what you can build for
                  them to make them money.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  For more information, check out this roadmap of{' '}
                  <Link
                    to="https://www.itresourcescorp.com/top-it-certifications-roadmap/"
                    target="_blank"
                    rel="noopener"
                  >
                    top IT certifications
                  </Link>
                  .
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
        <Accordion expanded={expanded === 'panel14'} onChange={handleAccordionChange('panel14')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>How do you get a technical job in the tech industry as a veteran or service member</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                <Typography variant="body1" gutterBottom>
                  The only real difference for veterans is that there are some dedicated/funded job readiness programs,
                  plus a network to market yourself through. If you're interested in Software Engineering, the best
                  thing you can do is to "grind LeetCode," publish software, and expand and utilize your network. You
                  can do the majority of your networking via LinkedIn by finding veterans that are working in areas
                  you're interested in. This is a good example of a resource I used early on from{' '}
                  <Link to="https://kalan.io/posts/military-to-software" target="_blank" rel="noopener">
                    Kalan Snyder
                  </Link>
                  .
                </Typography>
                <Typography variant="body1" gutterBottom>
                  By "grind LeetCode," I mean mastering data structures and algorithms in a programming language and
                  grinding coding challenges on popular sites like LeetCode. Consider using{' '}
                  <Link
                    to="https://www.va.gov/education/about-gi-bill-benefits/how-to-use-benefits/vettec-high-tech-program/"
                    target="_blank"
                    rel="noopener"
                  >
                    VET TEC
                  </Link>{' '}
                  to attend coding boot camps. These programs upskill you and have connections in technical job
                  placement.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  Service members and veterans should also apply for{' '}
                  <Link to="https://breakline.org/" target="_blank" rel="noopener">
                    BreakLine
                  </Link>{' '}
                  and take their{' '}
                  <Link to="https://breakline.org/public-classes-events/" target="_blank" rel="noopener">
                    free online classes
                  </Link>
                  . BreakLine’s purpose is to help veterans, minorities, and women get into high-paying tech roles. This
                  is one of the best ways to secure a tech job.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  Remember that depending on the company, you will likely have to pass technical assessments to get a
                  job offer. If you have a security clearance, it may make you a more attractive applicant to government
                  contractors since clearances can save companies time and tens of thousands of dollars.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  However, these companies often do capital E Engineering, not typical web development, which has a
                  lower bar to entry. This involves using STEM skills to optimize the performance of manufactured
                  products, working in enterprise-scale teams, and adhering to legal and moral liabilities. They prefer
                  STEM graduates. If you don’t have a STEM degree, preferably in Computer Science or Electrical
                  Engineering, consider pursuing graduate studies to make yourself more appealing.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  Understanding capitalism will help you focus your job search:
                </Typography>
                <Typography variant="body1" component="ul" gutterBottom>
                  <li>
                    <strong>Corporations:</strong> Always optimizing for profit. They value exceptional performance and
                    proven results. They seek 10xers or unicorns and prefer safe bets when hiring.
                  </li>
                  <li>
                    <strong>NGC (Northrop Grumman):</strong> Specializes in aerospace and defense projects. Involves
                    strict legal and ethical requirements. Focused on professional Engineering with complex problems
                    rooted in math, physics, and systems engineering.
                  </li>
                  <li>
                    <strong>Tech companies:</strong> Often reliant on investment. Hiring trends can shift based on
                    market conditions and interest rates.
                  </li>
                  <li>
                    <strong>Enterprise-scale problems:</strong> Knowing how to code isn’t enough. Understanding design
                    patterns, large-scale upgrades, testing, CI/CD, and scaling issues is crucial.
                  </li>
                </Typography>
                <Typography variant="body1" gutterBottom>
                  Fundamentals of hirability:
                </Typography>
                <Typography variant="body1" component="ul" gutterBottom>
                  <li>Relevant professional experience</li>
                  <li>Exceptional performance in competitions, certifications, or professional exams</li>
                  <li>Security clearance (an advantage if you already have one)</li>
                  <li>Military service (can provide experience, clearance, and networking opportunities)</li>
                  <li>Education (relevant course of study and institutional reputation)</li>
                  <li>Performance and accolades (consistent high performance and adaptability)</li>
                  <li>Networking (breadth and depth of connections, conferences, professional organizations)</li>
                  <li>Market economics (target less competitive or niche fields if needed)</li>
                  <li>Likeability</li>
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
        <Accordion expanded={expanded === 'panel15'} onChange={handleAccordionChange('panel15')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>How do you train to pass a technical assessment or interview</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                {' '}
                <Typography variant="body1" gutterBottom>
                  This level of preparation is probably overkill for most roles at most companies. However, it's good to
                  expose yourself to what top-level companies expect from technical interviews:
                </Typography>
                <Typography variant="body1" component="ul" gutterBottom>
                  <li>
                    An article from Alex Nguyen on the resources he uses to prepare for technical interviews{' '}
                    <Link
                      to="https://levelup.gitconnected.com/how-i-got-in-to-amazon-microsoft-google-all-from-studying-these-resources-31724508ce0e"
                      target="_blank"
                      rel="noopener"
                    >
                      here
                    </Link>
                    .
                  </li>
                  <li>
                    An article from Alex Nguyen on his system for solving problems in technical interviews{' '}
                    <Link
                      to="https://alexcancode.medium.com/the-coding-interview-formula-that-will-get-you-any-faang-maang-offer-45e71a3681e7"
                      target="_blank"
                      rel="noopener"
                    >
                      here
                    </Link>
                    .
                  </li>
                  <li>
                    A video from Clement Mihailescu on technical interviews{' '}
                    <Link to="https://www.youtube.com/watch?v=-QxUp8MwbWw" target="_blank" rel="noopener">
                      here
                    </Link>
                    .
                  </li>
                  <li>
                    A video from Clement Mihailescu on the most important concepts for technical interviews{' '}
                    <Link to="https://www.youtube.com/watch?v=Ge0Udbws1kc" target="_blank" rel="noopener">
                      here
                    </Link>
                    .
                  </li>
                  <li>
                    <Link to="https://algoexpert.io" target="_blank" rel="noopener">
                      AlgoExpert
                    </Link>{' '}
                    may help with DSA problems prep for technical interviews. I haven't tried it yet, so I can't give my
                    personal recommendation for it. I've heard it is a great product but lacks systems design and should
                    be used in tandem with products like Cracking the Coding Interview and Elements of Programming
                    Interviews. I'll give it a try when I get ready for technical interviews in about six months.
                  </li>
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
        <Typography variant="h2" component="h2" sx={{ color: 'text.primary' }}>
          What learning resources have I personally purchased or used
        </Typography>
        <Accordion expanded={expanded === 'panel16'} onChange={handleAccordionChange('panel16')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>What C++ resources have I personally purchased or used</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                {' '}
                <Typography variant="body1" component="ul" gutterBottom>
                  <li>
                    <Link
                      to="https://www.udemy.com/course/cpp-4skills/?amp=&aff_code=Ewh3Y1lWQH8FQR93MkBPbG1RGXFfW1h8B14ZeU5TQ3YBRxFzWj5XMRM%3D&pmtag=CAREERS24LEARN15&utm_campaign=careers24octlaunch&utm_medium=web"
                      target="_blank"
                      rel="noopener"
                    >
                      Mastering 4 Critical SKILLS using C++ 17
                    </Link>{' '}
                    by Dr. Moustafa Saad Ibrahim is the most concise way to learn C++ and develop problem-solving skills
                    at the same time. The emphasis is on short lectures and lots of problem-solving.
                  </li>
                  <li>
                    <Link
                      to="https://www.udemy.com/course/beginning-c-plus-plus-programming/"
                      target="_blank"
                      rel="noopener"
                    >
                      Beginning C++ Programming - From Beginner to Beyond
                    </Link>{' '}
                    is the most updated, best organized, and most intuitive way I've found to learn C++. There's also
                    lots of hours of material. However, it lacks a problem-solving focus to force you into writing lots
                    of C++.
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/gp/product/0321992784/ref=ppx_yo_dt_b_search_asin_title?ie=UTF8&psc=1"
                      target="_blank"
                      rel="noopener"
                    >
                      Programming: Principles and Practice using C++ (2nd Edition)
                    </Link>{' '}
                    is how I was introduced to C++. It's a great resource but there are quicker, and more intuitive,
                    ways to learn C++ for self-taught folks. My critiques are that it sometimes uses concepts before
                    they're introduced, seems to pontificate on some subjects unnecessarily or too early, and isn't
                    designed to get folks quickly trained by focusing on what I think are "fundamental."
                  </li>
                  <li>
                    <Link to="https://www.learncpp.com/" target="_blank" rel="noopener">
                      LearnCPP.com
                    </Link>{' '}
                    is a free resource that is very well-structured and easy to use as a quick way to practice.
                  </li>
                  <li>
                    <Link to="https://www.cppreference.com/" target="_blank" rel="noopener">
                      cppreference.com
                    </Link>{' '}
                    is a free reference resource that is very well-structured and easy to use as a quick reference.
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/gp/product/0321958322/ref=ppx_yo_dt_b_search_asin_title?ie=UTF8&psc=1"
                      target="_blank"
                      rel="noopener"
                    >
                      The C++ Programming Language (4th Edition)
                    </Link>{' '}
                    is a valuable reference (covers C++ 11 and 14).
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/gp/product/0321334876/ref=ppx_yo_dt_b_search_asin_title?ie=UTF8&psc=1"
                      target="_blank"
                      rel="noopener"
                    >
                      Effective C++
                    </Link>
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/gp/product/1491903996/ref=ppx_yo_dt_b_search_asin_title?ie=UTF8&psc=1"
                      target="_blank"
                      rel="noopener"
                    >
                      Effective Modern C++
                    </Link>
                  </li>
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
        <Accordion expanded={expanded === 'panel17'} onChange={handleAccordionChange('panel17')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>What data structures and algorithms resources have I personally purchased or used</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                <Typography variant="body1" component="ul" gutterBottom>
                  <li>
                    Dr. Moustafa Saad Ibrahim has an excellent series on{' '}
                    <Link to="https://www.udemy.com/course/dscpp-skills/" target="_blank" rel="noopener">
                      data structures
                    </Link>
                    ,{' '}
                    <Link to="https://www.udemy.com/course/skills-algorithms-cpp/" target="_blank" rel="noopener">
                      algorithms part 1
                    </Link>
                    ,{' '}
                    <Link to="https://www.udemy.com/course/skills-algorithms-cpp2/" target="_blank" rel="noopener">
                      part 2
                    </Link>
                    , and a course for{' '}
                    <Link to="https://www.udemy.com/course/skills-coding-interviews/" target="_blank" rel="noopener">
                      technical interview problems
                    </Link>
                    . He is a competitive programming coach and knows what it takes to excel at problem-solving and DSA.
                    His courses have a higher bar of entry for programming familiarity and are focused on using data
                    structures and algorithms to solve problems.
                  </li>
                  <li>
                    Abdul Bari's{' '}
                    <Link to="https://www.udemy.com/course/datastructurescncpp/" target="_blank" rel="noopener">
                      Mastering Data Structures & Algorithms using C and C++
                    </Link>{' '}
                    is the best instructor I have found for understanding data structures and algorithms if you're into
                    a lecture format. The course is more about building data structures than using them and is light on
                    algorithms or ways to try out the topics you've learned. Supplementing with other resources like
                    Structy can be beneficial. Abdul Bari also runs a popular{' '}
                    <Link to="https://www.youtube.com/@abdul_bari/featured" target="_blank" rel="noopener">
                      YouTube channel
                    </Link>{' '}
                    with additional DSA videos.
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/Grokking-Algorithms-illustrated-programmers-curious/dp/1617292230"
                      target="_blank"
                      rel="noopener"
                    >
                      Grokking Algorithms
                    </Link>{' '}
                    was my first introduction to DSA. Written for Python, it’s like a children’s book—a nice and
                    comfortable introduction but not ideal as a reference or for in-depth understanding.
                  </li>
                  <li>
                    <Link to="https://structy.net" target="_blank" rel="noopener">
                      Structy
                    </Link>{' '}
                    is an excellent one-stop shop for DSA and common operations in an interview format. It covers
                    JavaScript, C++, and Python. While it has only one practice problem per topic, it’s great for
                    learning optimal ways to tackle problems. You’ll need other sites like LeetCode for additional
                    problem-solving practice.
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/Introduction-Algorithms-fourth-Thomas-Cormen/dp/026204630X/ref=sr_1_5?crid=3VWUPSM6S9CFJ&keywords=algorithms&qid=1665340606&qu=eyJxc2MiOiI1LjAyIiwicXNhIjoiNC42NiIsInFzcCI6IjQuOTcifQ%3D%3D&sprefix=algorithms%2Caps%2C111&sr=8-5"
                      target="_blank"
                      rel="noopener"
                    >
                      Intro to Algorithms
                    </Link>{' '}
                    is a fantastic reference. If what you've found online isn't sufficient, this book likely has the
                    answer. It delves into the mathematical and computer science underpinnings of topics at an academic
                    level. However, I DO NOT recommend reading it cover-to-cover or using it to learn a new topic. It’s
                    best as a reference for deeper understanding.
                  </li>
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
        <Accordion expanded={expanded === 'panel18'} onChange={handleAccordionChange('panel18')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>What technical interview resources have I personally purchased or used</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                <Typography variant="body1" component="ul" gutterBottom>
                  <li>
                    Dr. Moustafa Saad Ibrahim has an excellent series on{' '}
                    <Link to="https://www.udemy.com/course/dscpp-skills/" target="_blank" rel="noopener">
                      data structures
                    </Link>
                    ,{' '}
                    <Link to="https://www.udemy.com/course/skills-algorithms-cpp/" target="_blank" rel="noopener">
                      algorithms part 1
                    </Link>
                    ,{' '}
                    <Link to="https://www.udemy.com/course/skills-algorithms-cpp2/" target="_blank" rel="noopener">
                      part 2
                    </Link>
                    , and a course for{' '}
                    <Link to="https://www.udemy.com/course/skills-coding-interviews/" target="_blank" rel="noopener">
                      technical interview problems
                    </Link>
                    . He is a competitive programming coach and knows what it takes to excel at problem-solving and DSA.
                    His courses have a higher bar of entry for programming familiarity and are focused on using data
                    structures and algorithms to solve problems.
                  </li>
                  <li>
                    Abdul Bari's{' '}
                    <Link to="https://www.udemy.com/course/datastructurescncpp/" target="_blank" rel="noopener">
                      Mastering Data Structures & Algorithms using C and C++
                    </Link>{' '}
                    is the best instructor I have found for understanding data structures and algorithms if you're into
                    a lecture format. The course is more about building data structures than using them and is light on
                    algorithms or ways to try out the topics you've learned. Supplementing with other resources like
                    Structy can be beneficial. Abdul Bari also runs a popular{' '}
                    <Link to="https://www.youtube.com/@abdul_bari/featured" target="_blank" rel="noopener">
                      YouTube channel
                    </Link>{' '}
                    with additional DSA videos.
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/Grokking-Algorithms-illustrated-programmers-curious/dp/1617292230"
                      target="_blank"
                      rel="noopener"
                    >
                      Grokking Algorithms
                    </Link>{' '}
                    was my first introduction to DSA. Written for Python, it’s like a children’s book—a nice and
                    comfortable introduction but not ideal as a reference or for in-depth understanding.
                  </li>
                  <li>
                    <Link to="https://structy.net" target="_blank" rel="noopener">
                      Structy
                    </Link>{' '}
                    is an excellent one-stop shop for DSA and common operations in an interview format. It covers
                    JavaScript, C++, and Python. While it has only one practice problem per topic, it’s great for
                    learning optimal ways to tackle problems. You’ll need other sites like LeetCode for additional
                    problem-solving practice.
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/Introduction-Algorithms-fourth-Thomas-Cormen/dp/026204630X/ref=sr_1_5?crid=3VWUPSM6S9CFJ&keywords=algorithms&qid=1665340606&qu=eyJxc2MiOiI1LjAyIiwicXNhIjoiNC42NiIsInFzcCI6IjQuOTcifQ%3D%3D&sprefix=algorithms%2Caps%2C111&sr=8-5"
                      target="_blank"
                      rel="noopener"
                    >
                      Intro to Algorithms
                    </Link>{' '}
                    is a fantastic reference. If what you've found online isn't sufficient, this book likely has the
                    answer. It delves into the mathematical and computer science underpinnings of topics at an academic
                    level. However, I DO NOT recommend reading it cover-to-cover or using it to learn a new topic. It’s
                    best as a reference for deeper understanding.
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/dp/1537713949/?coliid=I3MPRC82SZIL8X&colid=V4W7RR71H1OW&psc=1&ref_=lv_ov_lig_dp_it"
                      target="_blank"
                      rel="noopener"
                    >
                      Elements of Programming Interviews in Python
                    </Link>{' '}
                    and{' '}
                    <Link
                      to="https://www.amazon.com/dp/1479274836/?coliid=I2GHGD8ZX3UV0H&colid=V4W7RR71H1OW&psc=1&ref_=lv_ov_lig_dp_it"
                      target="_blank"
                      rel="noopener"
                    >
                      C++
                    </Link>
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/dp/0984782850/?coliid=ICZLURMSLNBNY&colid=V4W7RR71H1OW&psc=1&ref_=lv_ov_lig_dp_it"
                      target="_blank"
                      rel="noopener"
                    >
                      Cracking the Coding Interview
                    </Link>
                  </li>
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
        <Accordion expanded={expanded === 'panel19'} onChange={handleAccordionChange('panel19')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>What Python resources have I personally purchased or used</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                {' '}
                <Typography variant="body1" gutterBottom>
                  Go to{' '}
                  <Link to="https://pythonbooks.org/for-programming-beginners/" target="_blank" rel="noopener">
                    pythonbooks
                  </Link>{' '}
                  to see a breakdown of highly rated books by skill level and topic.
                </Typography>
                <Typography variant="body1" component="ul" gutterBottom>
                  <li>
                    <Link
                      to="https://www.oreilly.com/library/view/learning-python-5th/9781449355722/"
                      target="_blank"
                      rel="noopener"
                    >
                      Learning Python, 5th Edition
                    </Link>{' '}
                    is a great textbook. However, it is no longer updated.
                  </li>
                  <li>
                    <Link to="https://automatetheboringstuff.com/#toc" target="_blank" rel="noopener">
                      Automate the Boring Stuff
                    </Link>{' '}
                    teaches the basics quickly and then dives into building projects. The approach forces learners to
                    build and figure things out independently. It’s a great resource for learning practical skills and
                    projects, although it may not suit everyone’s learning style.
                  </li>
                  <li>
                    <Link
                      to="https://www.oreilly.com/library/view/fluent-python-2nd/9781492056348/"
                      target="_blank"
                      rel="noopener"
                    >
                      Fluent Python
                    </Link>{' '}
                    comes highly recommended. While I haven’t tackled it yet, it’s next on my list after graduating from
                    Hack Reactor.
                  </li>
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
        <Accordion expanded={expanded === 'panel20'} onChange={handleAccordionChange('panel20')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>What other career-related resources have I personally purchased or used</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                {' '}
                <Typography variant="body1" component="ul" gutterBottom>
                  <li>
                    <Link
                      to="https://www.amazon.com/gp/product/0062199579/ref=ppx_yo_dt_b_search_asin_title?ie=UTF8&psc=1"
                      target="_blank"
                      rel="noopener"
                    >
                      The Ten Day MBA
                    </Link>
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/gp/product/035834364X/ref=ppx_yo_dt_b_search_asin_title?ie=UTF8&psc=1"
                      target="_blank"
                      rel="noopener"
                    >
                      The Visual MBA
                    </Link>
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/gp/product/0525543023/ref=ppx_yo_dt_b_search_asin_title?ie=UTF8&psc=1"
                      target="_blank"
                      rel="noopener"
                    >
                      The Personal MBA
                    </Link>
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/dp/0988262509?psc=1&ref=ppx_yo2ov_dt_b_product_details"
                      target="_blank"
                      rel="noopener"
                    >
                      The Phoenix Project
                    </Link>
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/dp/B087FFMMBW?psc=1&ref=ppx_yo2ov_dt_b_product_details"
                      target="_blank"
                      rel="noopener"
                    >
                      The Sales Engineer Manager's Handbook
                    </Link>
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/dp/0814434835?psc=1&ref=ppx_yo2ov_dt_b_product_details"
                      target="_blank"
                      rel="noopener"
                    >
                      Selling Above and Below the Line
                    </Link>
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/dp/0985910690?psc=1&ref=ppx_yo2ov_dt_b_product_details"
                      target="_blank"
                      rel="noopener"
                    >
                      The 20-Minute Networking Meeting
                    </Link>
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/dp/1400214750?psc=1&ref=ppx_yo2ov_dt_b_product_details"
                      target="_blank"
                      rel="noopener"
                    >
                      Mission Transition
                    </Link>
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/dp/1984860348?psc=1&ref=ppx_yo2ov_dt_b_product_details"
                      target="_blank"
                      rel="noopener"
                    >
                      What Color is Your Parachute
                    </Link>
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/dp/1101875321?psc=1&ref=ppx_yo2ov_dt_b_product_details"
                      target="_blank"
                      rel="noopener"
                    >
                      Designing Your Life
                    </Link>
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/dp/0349415862?psc=1&ref=ppx_yo2ov_dt_b_product_details"
                      target="_blank"
                      rel="noopener"
                    >
                      So Good They Can't Ignore You
                    </Link>
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/dp/0306827751?psc=1&ref=ppx_yo2ov_dt_b_product_details"
                      target="_blank"
                      rel="noopener"
                    >
                      Hack your Bureaucracy
                    </Link>
                  </li>
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
        <Accordion expanded={expanded === 'panel21'} onChange={handleAccordionChange('panel21')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>What computer science-related books have I personally purchased or read for fun</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              <Box sx={{ marginTop: 4 }}>
                {' '}
                <Typography variant="body1" component="ul" gutterBottom>
                  <li>
                    <Link
                      to="https://www.amazon.com/dp/1250118360?psc=1&ref=ppx_yo2ov_dt_b_product_details"
                      target="_blank"
                      rel="noopener"
                    >
                      Algorithms to Live By
                    </Link>
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/Sandworm-Cyberwar-Kremlins-Dangerous-Hackers/dp/0525564632/ref=tmm_pap_swatch_0?_encoding=UTF8&qid=1665341427&sr=8-1"
                      target="_blank"
                      rel="noopener"
                    >
                      Sandworm
                    </Link>
                  </li>
                  <li>
                    <Link
                      to="https://www.amazon.com/dp/1594205221?psc=1&ref=ppx_yo2ov_dt_b_product_details"
                      target="_blank"
                      rel="noopener"
                    >
                      How Not to be Wrong
                    </Link>
                  </li>
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
        <Accordion expanded={expanded === 'panel22'} onChange={handleAccordionChange('panel22')} sx={{ width: '100%' }}>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography>What Artificial Intelligence (Machine Learning) resources have I used?</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Suspense fallback={<LoadingFallback />}>
              {/* Centered Image */}
              <Box sx={{ marginTop: 4, display: 'flex', justifyContent: 'center' }}>
                <CardMedia
                  component="img"
                  image={MachineLearningPathwayImage}
                  alt="ETS Briefing Schedule"
                  sx={{
                    width: '100%',
                    maxWidth: 1000, // Adjust as needed
                    height: 'auto',
                    borderRadius: 2,
                    boxShadow: 3,
                    cursor: 'pointer' // Add pointer cursor to show it's clickable
                  }}
                  onClick={handleOpen} // Clicking image opens modal
                />
              </Box>

              {/* Modal/Fullscreen Dialog */}
              <Dialog open={open} onClose={handleClose} maxWidth="xl">
                {/* Close Button */}
                <IconButton
                  onClick={handleClose}
                  sx={{
                    position: 'absolute',
                    top: 8,
                    right: 8,
                    color: 'white',
                    zIndex: 1
                  }}
                >
                  <CloseIcon />
                </IconButton>

                {/* Expanded Image */}
                <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', backgroundColor: 'black' }}>
                  <CardMedia
                    component="img"
                    image={MachineLearningPathwayImage}
                    alt="ETS Briefing Schedule"
                    sx={{
                      width: '100%',
                      maxWidth: '90vw', // Responsive max width
                      maxHeight: '90vh',
                      borderRadius: 0 // Reset border-radius for better fullscreen display
                    }}
                  />
                </Box>
              </Dialog>

              {/* Non-centered Text */}
              <Box sx={{ marginTop: 2 }}>
                <Typography variant="body1" gutterBottom>
                  Watch some videos from Michael I. Jordan on what AI/ML is and is not:{' '}
                  <Link to="https://www.youtube.com/watch?v=SRF4bXKOGSI" target="_blank" rel="noopener">
                    here for lecture style
                  </Link>{' '}
                  or{' '}
                  <Link to="https://www.youtube.com/watch?v=EYIKy_FM9x0&t=1s" target="_blank" rel="noopener">
                    here for a podcast
                  </Link>
                  .
                </Typography>
                <Typography variant="body1" gutterBottom>
                  Terms like Artificial Intelligence, Cybernetics, Machine Learning, and Deep Learning describe an
                  emerging interdisciplinary field that takes years of study and practice to become competent in. People
                  often enter the field from backgrounds in computer science, data science, math, neuroscience, etc.
                  Some folks take the "hands-on" approach while others spend years attaining math maturity.
                </Typography>
                <Typography variant="body1" gutterBottom>
                  If you're interested in working in the field, and not just learning enough to use it as a hobby or to
                  solve some of the problems you encounter in your own field, I encourage you to talk to people in the
                  industry, write down your priorities, make a 5- or 10-year plan, and backwards plan everything you
                  need to do to be successful. Your daily actions should feed into your priorities.
                </Typography>
              </Box>
            </Suspense>
          </AccordionDetails>
        </Accordion>
      </Container>
    </AppTheme>
  );
}
