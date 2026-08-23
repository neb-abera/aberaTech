import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import CssBaseline from '@mui/material/CssBaseline';
import Divider from '@mui/material/Divider';
import Link from '@mui/material/Link';
import Typography from '@mui/material/Typography';
import AppAppBar from '../components/AppAppBar';
import AppTheme from '../theme/AppTheme';

/**
 * The text messaging terms.
 *
 * Required by the carriers rather than chosen: a 10DLC campaign registration
 * has to point at a publicly reachable page carrying the brand name, what will
 * be sent, how often, that rates may apply, how to get help, and how to stop.
 * Written plainly for the person reading it, since the people receiving these
 * messages are soldiers being asked to hand over a phone number.
 */
export default function SmsTerms(props: { disableCustomTheme?: boolean }) {
  return (
    <AppTheme {...props}>
      <CssBaseline enableColorScheme />
      <AppAppBar />
      <Container maxWidth="md" sx={{ pt: { xs: 14, sm: 16 }, pb: 8 }}>
        <Typography variant="h4" component="h1" sx={{ fontWeight: 600 }}>
          Text message terms
        </Typography>
        <Typography variant="body2" sx={{ color: 'text.secondary', mt: 1 }}>
          For text messages sent by Neb Abera about appointments booked on abera.tech.
        </Typography>
        <Divider sx={{ my: 3 }} />

        <Box sx={{ '& h2': { mt: 4, mb: 1 }, '& p': { mb: 2 } }}>
          <Typography variant="h6" component="h2">
            What you are agreeing to
          </Typography>
          <Typography variant="body1" component="p">
            If you tick the box asking for text updates when you book a time or join a queue, Neb Abera will send you
            text messages about that appointment and nothing else. You are never signed up automatically: the box is
            unticked until you tick it, and leaving it alone means you get no texts at all.
          </Typography>

          <Typography variant="h6" component="h2">
            What you will receive
          </Typography>
          <Typography variant="body1" component="p">
            A confirmation when you book, a reminder the day before, a reminder about an hour before, and a message if
            the appointment is cancelled. If you join a queue, a message confirming your place, a message if your
            estimated time moves by more than about ten minutes, one shortly before your turn, and one when it is your
            turn.
          </Typography>

          <Typography variant="h6" component="h2">
            How often
          </Typography>
          <Typography variant="body1" component="p">
            Message frequency varies, and depends entirely on what you book. A single appointment is usually four
            messages. There is no marketing, no newsletter, and nothing is sent to you for any reason other than an
            appointment you asked for.
          </Typography>

          <Typography variant="h6" component="h2">
            Cost
          </Typography>
          <Typography variant="body1" component="p">
            There is no charge from Neb Abera for these messages. Message and data rates may apply, depending on your
            plan with your mobile carrier.
          </Typography>

          <Typography variant="h6" component="h2">
            Stopping messages
          </Typography>
          <Typography variant="body1" component="p">
            Reply <strong>STOP</strong> to any message to stop all of them. You may also reply QUIT, END, CANCEL,
            REVOKE, OPT OUT or UNSUBSCRIBE. You will get one confirmation and then nothing further. Reply{' '}
            <strong>START</strong> to begin again.
          </Typography>
          <Typography variant="body1" component="p">
            Stopping texts does not cancel your appointment. It only stops the messages. If you want to cancel the
            appointment itself, use the cancellation link or email the address below.
          </Typography>

          <Typography variant="h6" component="h2">
            Help
          </Typography>
          <Typography variant="body1" component="p">
            Reply <strong>HELP</strong> to any message, or email{' '}
            <Link href="mailto:nebyouabera@gmail.com">nebyouabera@gmail.com</Link>.
          </Typography>

          <Typography variant="h6" component="h2">
            Carriers
          </Typography>
          <Typography variant="body1" component="p">
            Mobile carriers are not liable for delayed or undelivered messages.
          </Typography>

          <Typography variant="h6" component="h2">
            Related
          </Typography>
          <Typography variant="body1" component="p">
            See the <Link href="/sms-privacy">text message privacy policy</Link> for what happens to your phone number.
          </Typography>
        </Box>
      </Container>
    </AppTheme>
  );
}
