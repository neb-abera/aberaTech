import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import TimelineSeparatorCustom from './TimelineSeparatorCustom.tsx';
import Timeline from '@mui/lab/Timeline';
import TimelineItem, { timelineItemClasses } from '@mui/lab/TimelineItem';
import TimelineContent from '@mui/lab/TimelineContent';
import TimelineSeparator from '@mui/lab/TimelineSeparator';
import TimelineDot from '@mui/lab/TimelineDot';
import CircleIcon from '@mui/icons-material/Circle';
import TimelineConnector from '@mui/lab/TimelineConnector';
import { Link } from 'react-router';

export default function NineToTwelveMonths() {
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
                  Retirement & UQR/REFRAD packets can be sent up through your chain of command at 12 months until ETS.
                </Typography>
                <Typography sx={{ marginBottom: 2 }}>
                  You need to get the contact information for the relevant section at your installation's G1. BN or BDE
                  S1s will send your packet to G1, and it is on G1 to send the packet to HRC. S1s usually do this by
                  sending your packet to a group G1 email. G1 can be picky and unresponsive, so you need to follow up
                  with them. They've been known not to respond if something isn't done correctly.
                </Typography>

                <Typography sx={{ marginBottom: 2 }}>
                  At Fort Moore, the G1 equivalent for the installation is called the{' '}
                  <Link to="https://www.moore.army.mil/Garrison/DHR/Contacts.html" target="_blank" rel="noopener">
                    Directorate of Human Resources
                  </Link>{' '}
                  and they are at building 35. The{' '}
                  <strong>information they put out will likely not make it down to you</strong> due to personnel
                  turnover and the "telephone game" that happens during info distribution through the chain of command.{' '}
                  <strong>Requirements, and the people in charge, change</strong>, so you will need to{' '}
                  <strong>know what every echelon in your chain of command requires</strong> all the way up to HRC. It's
                  daunting but worth it.
                </Typography>

                <Typography sx={{ marginBottom: 2 }}>
                  The Army needs an electronic administrative action processing and approval system. S1s are overwhelmed
                  with administrative tasks and don't have good systems to track and process them. On top of that, S1s
                  in SFAB don't have clerks to help them out. I really feel for them. Email is not a good method for
                  processing and tracking administrative actions. People change duty positions at every level, and
                  requirements change all the time. S1s are good people stuck in a bad system.
                </Typography>

                <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>STORY ABOUT HOW I LEARNED THIS:</Typography>
                <Typography sx={{ marginBottom: 2 }}>
                  My S1 said he sent my request for orders at around the 8-month mark. He would respond with something
                  to the effect of "it's pending approval at G1" when I asked for a progress update. At the 6-month mark
                  before ETS, when it was getting close for me to move to Chicago for my CSP, he told my 1SG that I had
                  never sent my request for orders to him. I sent him an email confronting him about what I perceived as
                  lying and telling him that I was going to escalate the situation if I didn't get help with my CSP,
                  orders, and Reserve transition.
                </Typography>
                <Typography sx={{ marginBottom: 2 }}>
                  I went to building 35, found the permanent party separation department, and found out that my request
                  for orders, whether it was sent or not, would not get approved because for the first time in 2 years,
                  since before COVID, they were now requiring in-person ETS briefs before soldiers can request orders.
                  The civilians also said that they had repeatedly pushed this information out to the S1 channel.{' '}
                  <strong>
                    Every week at every Army installation there are people suffering transition nightmares.
                  </strong>{' '}
                  <strong>
                    The Army will then pay the price for these nightmares financially via unemployment, or the Army's
                    reputation will suffer when stories of these transition nightmares are spread.
                  </strong>{' '}
                  I personally witnessed three examples on my Final Out day when I cleared Fort Moore on 30 September. I
                  also faced a lot of bureaucratic heartaches and had to fight hard to get what I needed.
                </Typography>

                <Typography component="ol" sx={{ marginBottom: 2, paddingLeft: 3 }}>
                  <Typography component="li" sx={{ marginBottom: 1 }}>
                    There are people unable to retire on their original timeline because they didn't attend a mandatory
                    retirement brief (that they did not know about).
                  </Typography>
                  <Typography component="li" sx={{ marginBottom: 1 }}>
                    Leaders escorting people to clear installation a month after their ETS date. Their units didn't
                    prioritize the soldier's transition, and the soldier didn't fight them back hard enough. The person
                    that pays the price in these scenarios is the soldier. Their transition timeline is messed up and
                    they are clearing without getting paid.
                  </Typography>
                  <Typography component="li" sx={{ marginBottom: 1 }}>
                    People trying to final out with just a unit clearance record (there are so many more requirements
                    than that).
                  </Typography>
                </Typography>

                <Typography sx={{ marginBottom: 2 }}>
                  I asked the civilian who issued my DD214 how often horror stories like that happen. She said they
                  happen every week and that{' '}
                  <strong>
                    the information G1 distributes does not make it down through the S1 channels to the soldiers.
                  </strong>
                </Typography>

                <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                  You need to be proactive, keep receipts of all interactions, and be willing to escalate to a higher
                  authority if you don't get the help you need in an acceptable timeframe.
                </Typography>
                <Typography sx={{ marginBottom: 2 }}>
                  You have the S1 chain, the leadership chain, speaking to civilian supervisors, ICE complaints, your
                  congressperson, or you can consult a lawyer. The longer you wait, the less time you give yourself and
                  others to fix your problem before you have to use a "nuclear" option like a senior-level commander or
                  a lawyer.
                </Typography>

                <Typography sx={{ marginBottom: 2 }}>
                  <strong>The system is very bureaucratic</strong> and it's easy for your administrative actions to fail
                  to make it through. It's worth fighting for.{' '}
                  <strong>Do not feel awkward or ashamed about advocating for yourself.</strong>
                </Typography>

                <Typography sx={{ marginBottom: 2 }}>"the squeaky wheel gets the grease"</Typography>
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
                  Submit your signed and completed CSP packet to your installation coordinator several months before
                  your CSP start date.
                </Typography>
                <Typography sx={{ marginBottom: 2 }}>
                  It might take JAG up to 4-8 weeks to review/sign your packet and send it to the installation command.
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
                  Do not take no for an answer if your CSP, retirement/UQR, or other transition documents get
                  "disapproved."
                </Typography>

                <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                  Keep escalating it. Your transition is worth fighting for.
                </Typography>
                <Typography sx={{ marginBottom: 2 }}>
                  You’re your own best advocate.{' '}
                  <strong>
                    Your more senior commanders probably don’t even know about your denial, and likely won't support the
                    denial
                  </strong>
                  . “People first” isn’t just a saying. Soldiers transitioning well is in the best financial and
                  political interests of the Army, on top of the morally right thing to do.
                </Typography>

                <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                  Do not let anyone other than the approval authority stop your packet from reaching the approval
                  authority.
                </Typography>
                <Typography sx={{ marginBottom: 2 }}>
                  The lowest approval authority for a regular CSP is a field grade officer which is usually a BN CDR. An
                  O3 is not the approval authority for any CSP. If the CSP is &gt;60 days, then the approval authority
                  is the CG or an O6 that has delegation authority.{' '}
                  <strong>Get justifications for disapprovals in writing.</strong>
                </Typography>

                <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>
                  If they do support the denial, your best solution is to fight so hard for your CSP that it makes
                  approving your CSP the path of least resistance for them.
                </Typography>

                <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>STORY ABOUT HOW I LEARNED THIS:</Typography>
                <Typography sx={{ marginBottom: 2 }}>
                  I had to cause a ruckus to get my transition, CSP, and Reserve follow-on schools requests processed.
                  My Company Commander was incredible, but he forgot to send my CSP or transition packets up, which cost
                  me a couple of weeks. He was an incredible leader, though, so he apologized and rectified the
                  situation. But then, my BN S1 didn't send up my CSP or transition documents to the approval authority
                  after multiple reminders.
                </Typography>
                <Typography sx={{ marginBottom: 2 }}>
                  With a week to go until I was supposed to move out of my apartment and leave for Chicago for my CSP, I
                  had no orders, no approved CSP, and no idea if I could get a job after ETS or if I had follow-on
                  Reserve schools. The straw that broke the camel's back was when my S1 asked me to forge the O6
                  signature on my CSP (which was going to JAG for approval by a lawyer and then installation command). I
                  asked my BN CDR to sign my CSP directly but didn’t get anything back after a couple of weeks.
                </Typography>
                <Typography sx={{ marginBottom: 2 }}>
                  With one email to the CG about my problems with my transition to the Reserve, and a call from my
                  lawyer to the BDE JAG about my CSP, I got my packets approved all the way to installation command and
                  back in a couple of days. I had politely been fighting fruitlessly on my own for so long. It takes
                  personal courage to advocate for yourself after being a team player for so long.
                </Typography>
                <Typography sx={{ marginBottom: 2 }}>
                  My perception at the time was that I gave the Army almost 12 years of my life, between the NG/USMA/RA,
                  but my unit butchered my transition and then tried a bit of retaliation when I got help. My BN XO
                  called me, and among other things, threatened retaliation. I documented and reported the retaliation
                  to my lawyer and BN CDR. It was all worth the fighting though.
                </Typography>
                <Typography sx={{ marginBottom: 2 }}>
                  I completed an incredible CSP, I got to move in with my wife in Chicago after six years of a
                  long-distance relationship, I have a VET TEC software engineer bootcamp lined up, and I am executing a
                  transition plan I am proud of.
                </Typography>

                <Typography sx={{ marginBottom: 2 }}>"Closed mouths don’t get fed"</Typography>
                <Typography sx={{ marginBottom: 2 }}>"The squeaky wheel gets the grease"</Typography>

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
                  If your CSP packet gets mismanaged, you might find yourself in a position where your DA 31,
                  authorizing administrative absence from your unit, has been approved but your CSP packet, authorizing
                  you to work as an intern, is disapproved or missing.
                </Typography>
                <Typography sx={{ marginBottom: 2 }}>
                  <strong>You can get free legal service at your installation's Trial Defense Service</strong> (where
                  the defense counsels hang out at your installation).{' '}
                  <strong>You can also consult your BDE JAG, but realize that they work for your BDE CDR,</strong> so
                  that's equivalent to asking a prosecutor for legal help.
                </Typography>

                <Typography sx={{ fontWeight: 'bold', marginBottom: 2 }}>STORY ABOUT HOW I LEARNED THIS:</Typography>
                <Typography sx={{ marginBottom: 2 }}>
                  I found myself in the position described above. My CSP DA 31 was signed and approved, but the rest of
                  my CSP packet was still stuck in the bureaucracy a week or so out from leaving. I told my CO
                  leadership and BN S1 that I was planning to leave anyway since it wasn't my fault they fumbled my
                  transition. But, I asked my lawyer to help me with that situation, since I was legally authorized to
                  leave but not legally authorized to do an internship.
                </Typography>
                <Typography sx={{ marginBottom: 2 }}>
                  It seemed that my unit was actually okay with letting me be gone, even if the rest of my packet was
                  not approved. What they didn't seem to like was how much I was embarrassing them by reaching out to
                  the CG and BDE JAG about the unit messing up my transition paperwork. I was worried that if something
                  went wrong, my unit could throw the book at me for being AWOL and pretend like it was my fault.
                </Typography>
                <Typography sx={{ marginBottom: 2 }}>
                  I did end up getting my CSP approved last minute, but I think my unit would have let me be gone either
                  way since I already had a signed DA 31.
                </Typography>
              </Box>
            </TimelineContent>
          </TimelineItem>
        </TimelineContent>
      </Timeline>
    </Box>
  );
}
