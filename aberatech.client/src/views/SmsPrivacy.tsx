import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import CssBaseline from '@mui/material/CssBaseline';
import Divider from '@mui/material/Divider';
import Link from '@mui/material/Link';
import Typography from '@mui/material/Typography';
import AppAppBar from '../components/AppAppBar';
import AppTheme from '../theme/AppTheme';

/**
 * What happens to a phone number given to this site.
 *
 * The sentence the carriers care about is the one saying numbers are not shared
 * with third parties for marketing, and it has to be on a publicly reachable
 * page. The rest is here because somebody handing over a mobile number deserves
 * a straight answer about where it goes.
 */
export default function SmsPrivacy(props: { disableCustomTheme?: boolean }) {
  return (
    <AppTheme {...props}>
      <CssBaseline enableColorScheme />
      <AppAppBar />
      <Container maxWidth="md" sx={{ pt: { xs: 14, sm: 16 }, pb: 8 }}>
        <Typography variant="h4" component="h1" sx={{ fontWeight: 600 }}>
          Text message privacy
        </Typography>
        <Typography variant="body2" sx={{ color: 'text.secondary', mt: 1 }}>
          What happens to a phone number given to abera.tech.
        </Typography>
        <Divider sx={{ my: 3 }} />

        <Box sx={{ '& h2': { mt: 4, mb: 1 }, '& p': { mb: 2 } }}>
          <Typography variant="h6" component="h2">
            The short version
          </Typography>
          <Typography variant="body1" component="p">
            Your phone number is used to text you about your own appointment and for nothing else. It is not sold, not
            rented, and not shared with anybody for marketing. If you do not ask for text updates, you are not asked for
            a number at all.
          </Typography>

          <Typography variant="h6" component="h2">
            What is collected
          </Typography>
          <Typography variant="body1" component="p">
            The name you type, the time you booked, the time zone your browser reports, and, only if you ask for text
            updates, your mobile number. Nothing else. There is no account, no password, and no tracking of you across
            other sites.
          </Typography>

          <Typography variant="h6" component="h2">
            Who it is shared with
          </Typography>
          <Typography variant="body1" component="p">
            Only the messaging provider that carries the text, and only so that it can be delivered. Mobile information
            is not shared with third parties or affiliates for marketing or promotional purposes. Opt-in data and
            consent are never shared with anyone.
          </Typography>

          <Typography variant="h6" component="h2">
            Where it is kept
          </Typography>
          <Typography variant="body1" component="p">
            In a database in Microsoft Azure, encrypted at rest, reachable only by this site. Phone numbers are
            deliberately kept out of application logs.
          </Typography>

          <Typography variant="h6" component="h2">
            How long it is kept
          </Typography>
          <Typography variant="body1" component="p">
            For as long as needed to run the appointment and keep a record that it happened. Ask and it will be deleted.
          </Typography>

          <Typography variant="h6" component="h2">
            Stopping messages, and removing your number
          </Typography>
          <Typography variant="body1" component="p">
            Reply <strong>STOP</strong> to any message to stop all of them. To have your number removed entirely, email{' '}
            <Link href="mailto:nebyouabera@gmail.com">nebyouabera@gmail.com</Link>.
          </Typography>

          <Typography variant="h6" component="h2">
            Related
          </Typography>
          <Typography variant="body1" component="p">
            See the <Link href="/sms-terms">text message terms</Link> for what gets sent and how often.
          </Typography>
        </Box>
      </Container>
    </AppTheme>
  );
}
