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

export default function TwelveToEighteenMonths() {
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
                Once you are TAP complete, you will get a signed DD Form 2648 and you can use that to contact the Career
                Skills Program Installation Administrator (IA) and ask for the requirements to do a CSP.
              </Typography>

              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                Successfully completing a CSP at a company often results in a job offer from the company.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                At a minimum, they have to give you an interview. Most of the time, they only accept people they are
                interested in hiring. The benefit for them is they got a cost-free employee that they got to try out for
                months. You will be a known quantity, they'll have built a relationship with you, you'll have already
                had months of relevant experience doing that specific job, etc.
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                They will send you an email similar to the message and documents below.
              </Typography>

              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>Key highlights and recommendations</Typography>

              <Typography component="ol" sx={{ marginBottom: 2, paddingLeft: 3 }}>
                <Typography component="li" sx={{ marginBottom: 2 }}>
                  The <strong>approval authority for CSPs within a 50-mile radius of your installation</strong> is the
                  first field grade in your chain with command authority, which{' '}
                  <strong>is usually your Battalion Commander</strong>. The{' '}
                  <strong>
                    approval authority for CSPs outside of a 50-mile radius and lasting 60 or fewer days is an O6
                    commander (can be delegated to O5). The approval authority for CSPs outside of a 50-mile radius, and
                    between 61 and 180 days, is the first commanding general in your chain of command (can be delegated
                    down to an O6 commander).
                  </strong>{' '}
                  I heard a rumor that the MCOE CG is now the approval authority for Fort Moore CSPs outside of 50 miles
                  and longer than 60 days. I used 1st SFAB's O6 commander for my CSP approval.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 2 }}>
                  You aren't limited to the CSPs offered by your installation. You can do any internship in the DOD
                  SkillBridge network.{' '}
                  <strong>
                    You can also make your own internships if none of the offered options fit your needs and desires.
                  </strong>{' '}
                  Check below, or see the{' '}
                  <Link to="https://github.com/nebyou-abera/transition/tree/main/csp" target="_blank" rel="noopener">
                    CSP folder
                  </Link>{' '}
                  on my{' '}
                  <Link to="https://github.com/nebyou-abera/transition" target="_blank" rel="noopener">
                    GitHub
                  </Link>
                  , to see what documents you need for that. In general, you need to find a company that will work with
                  you, create a training plan, and get JAG and INSCOM approval. Make sure you give JAG about 30 days to
                  review and approve your CSP.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 2 }}>
                  <strong>
                    Your CSP, and any terminal leave you decide to take, need to start and end within the 180-day window
                    between your ETS.
                  </strong>{' '}
                  For example, you can do 180 days of a CSP but you'll have 0 days of terminal leave (so you'll have to
                  take your "ordinary" leave before the CSP). You can do 120 days of CSP and 60 days of terminal leave,
                  which will equal 180 days together.{' '}
                  <strong>
                    You can take leave before the 180-day window but you can't mark the DA 31 as transition leave. It
                    needs to be marked as annual (ordinary) leave.
                  </strong>{' '}
                  If you decide to take leave before the 180 days before CSP, do not call it terminal leave to your S1
                  or leadership, that will only cause confusion.{' '}
                  <strong>
                    I recommend that you leave 10 business days before your terminal leave date so that you can come
                    back and clear the installation.
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
                  but they will be taxed at the marginal tax rate of ~22-25% and you won't get BAH for it.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 2 }}>
                  If you want to maximize the amount of money you get, use your earned leave before and/or after your
                  CSP.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 2 }}>
                  Reach out and apply to companies early; you need their signature for your CSP packet.
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
                as a veteran. I'd recommend activating it when you feel comfortable navigating LinkedIn and you are
                ready to grow your network.
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                The main benefit I saw from premium was being able to send messages to people outside your network. I
                didn't really care about seeing who was looking at my profile.{' '}
                <strong>
                  I don't see the value of LinkedIn Premium once you've done the heavy lifting of building your network
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
                Thank you for your interest in the CSP Individual Internship. Please review the eligibility criteria
                below and the instructions for submitting the documents.
              </Typography>

              <Typography component="ol" sx={{ marginBottom: 2, paddingLeft: 3 }}>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  Requires Soldier to be 6 months/180 days of ETS or retirement date [not inclusive of the terminal
                  leave time].
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  CSP participation may only involve ONE company or training program within the 180 days not to exceed
                  120 days.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  Establish POC with the company, internship start/end dates, and a weekly internship curriculum.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  Requires Command approval and signatures on submission of all documents required.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  Completion of TAP [include the Form 2648 with packet received at completion].
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  Requires submission of the Internship packet 1 month prior to your planned start date [for Legal and
                  Regional Coordinator approval process, at this time Legal is taking 15-30 days to process packets].
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  An approved Administrative Absence signed by the O-6 must be acquired to participate outside 50 miles
                  of Fort Moore.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  Be mindful that CSP must begin after or be complete before transition/terminal leave if you plan to
                  take it during the 6-month window of CSP participation.
                </Typography>
              </Typography>

              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                INSTRUCTIONS FOR COMPLETING INTERNSHIP DOCUMENTS:
              </Typography>

              <Typography component="ol" sx={{ marginBottom: 2, paddingLeft: 3 }}>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  CSP Individual Internship Agreement: Complete and submit in the same fillable format originally
                  provided with digital signatures. (Do not print or scan this document. Return in the editable digital
                  PDF format with digital signatures.)
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  CSP Participation Memo: Complete and submit in the fillable format provided with digital signatures.
                  Must have O-6 approval for Administrative Absence.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  DA31: Complete with O-6 signature only if the internship is 50 miles outside of Fort Moore.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  CSP Commander's Checklist: Complete with both your and Commander's initials. Please select "No" if any
                  of the questions do not apply.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  Please include the TAP Form 2648 provided by your counselor.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  Please attach a separate training curriculum including each week you will be interning (See example
                  attached).
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  Please ensure the dates on the start and end dates DA31 and Participation Memo are the same.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  Please include in the Internship Agreement, outcome section, there will be an interview opportunity.
                  Please label and attach each document individually. Do not submit the packet as one scanned PDF
                  document.
                </Typography>
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                Please ensure you mention in #4 of the agreement the internship will lead to an interview with the
                company. Follow all instructions provided to prevent delays with the final approval process. If you have
                any questions or concerns, please do not hesitate to contact me.
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>Warm Regards,</Typography>
              <Typography sx={{ marginBottom: 2 }}>XXXXX</Typography>
              <Typography sx={{ marginBottom: 2 }}>Career Skills Program Installation Administrator (IA)</Typography>
              <Typography sx={{ marginBottom: 2 }}>Soldier for Life TAP Bldg 9230</Typography>
              <Typography sx={{ marginBottom: 2 }}>8150 Marne Rd. Fort Moore, GA 31905</Typography>
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
                This document is only required if you're going through a "med board" and want to do a CSP.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                This is important because administratively there will be long wait times without any notice if you're
                being kicked out or not. You can use this time to do a CSP instead of languishing in uncertainty. When
                they do decide to med-board you or not, all of a sudden they might tell you you've been med-boarded,
                they'll send you a VA and DOD compensation offer to negotiate, and you'll have about 30-60 days or less
                to get your affairs together and out-process the Army.
              </Typography>

              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>STORY ABOUT HOW I LEARNED THIS:</Typography>

              <Typography sx={{ marginBottom: 2 }}>
                My friend and I worked together in the same unit, started separation processes at around the same time,
                and ETS'ed at around the same time. I UQR/REFRAD'ed while he went through a med board.
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                <strong>
                  He went from languishing for months not knowing if he was able to stay in the Army, to being told he
                  needed to quickly get out.
                </strong>{' '}
                <strong>He didn't do a CSP</strong>, since none that Fort Moore CSP offered fit his timeline or needs.
                He also didn't feel he had enough time to make a CSP with another company. And on top of that,{' '}
                <strong>he stayed at our unit working</strong> which messed with his ability to focus on his transition.
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                <strong>
                  Meanwhile, I spent my time doing an internship, interviewing at companies, and got dedicated time to
                  work on my transition.
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
                Here is an example training plan for creating your own CSP with a company. Some companies already have
                their own training plan if they have already hosted CSP interns. This is much more thorough than
                required. My CSP coordinator ended up only submitting the first sheet. That makes me think that's all
                that would be required for approval.
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
                If you are interested in education, you should apply to universities and scholarships at this time.{' '}
                <strong>
                  You should apply to schools outside of what you "think" you can't get into. Top schools like Harvard,
                  Yale, University of Chicago want veteran representation
                </strong>{' '}
                and it'll be easier for you to get in as a veteran. You will still have to put in a lot of work to "sell
                yourself" but it will be worth it.
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
                <Link to="https://pattillmanfoundation.org/apply/" target="_blank" rel="noopener">
                  Pat Tilman scholarship
                </Link>
                , and a variety of other resources to cover additional expenses. Ensure that you understand how the GI
                Bill works. The TAP gives a great GI Bill course and teaches you tools like the{' '}
                <Link to="https://www.va.gov/education/gi-bill-comparison-tool/" target="_blank" rel="noopener">
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
                TAP has a great course on helping you understand what career fields have great opportunities, and what
                locations have better opportunities for your chosen career field.
              </Typography>

              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                You should decide if you are restricted by location.
              </Typography>

              <Typography sx={{ marginBottom: 2 }}>
                If you're not, where are you interested in living? Are you willing to relocate for a job? If you are,
                what is the minimum compensation package that makes moving worth it to you?
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
                Develop a couple of versions of your resume. The <strong>most important part is</strong> doing a{' '}
                <strong>brainstorming</strong> exercise where you{' '}
                <strong>write down every result you've produced in your career</strong>. You can use old OERs and
                counseling to help you start. This will help you translate your Army experiences to civilian
                terminology. Use the Project, Action, Result model.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                You can use the brainstorming exercise to tailor your resume to highlight experiences that match the job
                description.
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
                  BDE CDR counseling signed by your CDR and addressed to HRC. This requires you to draft the document
                  and then <strong>schedule a counseling by your BDE CDR (so they can sign it).</strong>
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  Get a counseling signed by your installation's Reserve counselor.
                </Typography>
                <Typography component="li" sx={{ marginBottom: 1 }}>
                  A memo stating that you aren't resigning due to SHARP.
                </Typography>
              </Typography>
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                Do not let your company commander, battalion commander, or S1 try to deter you from submitting your
                UQR/REFRAD. They are not the approval authority.
              </Typography>
              <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>STORY ABOUT HOW I LEARNED THIS:</Typography>
              <Typography sx={{ marginBottom: 2 }}>
                I had incredible Company, Battalion, and Brigade commanders at my last unit in the Army. I scheduled my
                BDE CDR counseling at about 13 months until ETS. I had an awesome Battalion Commander who asked me to
                cancel it so that he could work to try to find a BN in Chicago that could take me in a recruiting or
                ROTC role. We exhausted those options while I got sent to Airborne school at the 11-month mark and I
                couldn't do anything about my UQR/REFRAD.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                It then took an additional couple of weeks to schedule a counseling session and get my BDE CDR to sign
                it. Once I submitted my UQR/REFRAD at about the 9-10 month mark, it waited at HRC. I later found out
                from my lawyer that the reviewer/approver was on leave. The unintended consequence of listening to my
                Battalion Commander cost my UQR/REFRAD to be delayed by 3 or so months and could have easily derailed
                the rest of my transition if my lawyer hadn't made sure the reviewer/approver prioritized it when he got
                back.
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
  );
}
