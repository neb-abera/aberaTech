import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import Divider from '@mui/material/Divider';
import IconButton from '@mui/material/IconButton';
import Link from '@mui/material/Link';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import GitHubIcon from '@mui/icons-material/GitHub';
import InstagramIcon from '@mui/icons-material/Instagram';
import LinkedInIcon from '@mui/icons-material/LinkedIn';
import { Link as RouterLink } from 'react-router';
import { guides, label, projects } from '../site/sections';

/**
 * The site footer. It carries the identity on every page, which is why the
 * pages themselves no longer open with a full height introduction.
 *
 * This replaced the Material UI template footer, which shipped with a newsletter
 * form that went nowhere, a copyright line crediting Sitemark, and social links
 * pointing at Material UI's own accounts rather than mine.
 */

const socials = [
  { label: 'LinkedIn', href: 'https://www.linkedin.com/in/neb-abera/', Icon: LinkedInIcon },
  { label: 'GitHub', href: 'https://github.com/neb-abera', Icon: GitHubIcon },
  { label: 'Instagram', href: 'https://www.instagram.com/neb_abera', Icon: InstagramIcon }
];

export default function Footer() {
  return (
    <Container component="footer" maxWidth="lg" sx={{ py: { xs: 5, sm: 7 } }}>
      <Divider sx={{ mb: { xs: 3, sm: 4 } }} />
      <Box
        sx={{
          display: 'flex',
          flexDirection: { xs: 'column', sm: 'row' },
          justifyContent: 'space-between',
          gap: 3
        }}
      >
        <Stack spacing={1} sx={{ maxWidth: 340 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
            Neb Abera
          </Typography>
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            Senior Computer Scientist at MITRE, specializing in secure embedded systems.
          </Typography>
          <Stack direction="row" spacing={0.5} sx={{ ml: -1, mt: 0.5 }}>
            {socials.map(({ label, href, Icon }) => (
              <IconButton key={label} size="small" href={href} target="_blank" rel="noopener" aria-label={label}>
                <Icon fontSize="small" />
              </IconButton>
            ))}
          </Stack>
        </Stack>

        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={{ xs: 2, sm: 5 }} component="nav" aria-label="Footer">
          <Stack spacing={0.75}>
            <Typography variant="caption" sx={{ color: 'text.disabled' }}>
              Guides
            </Typography>
            {guides.map((entry) => (
              <Link
                key={entry.to}
                component={RouterLink}
                to={entry.to}
                variant="body2"
                underline="hover"
                sx={{ color: 'text.secondary' }}
              >
                {label(entry)}
              </Link>
            ))}
          </Stack>
          <Stack spacing={0.75}>
            <Typography variant="caption" sx={{ color: 'text.disabled' }}>
              Projects
            </Typography>
            {projects.map((entry) =>
              entry.external ? (
                <Link
                  key={entry.to}
                  href={entry.to}
                  target="_blank"
                  rel="noopener noreferrer"
                  variant="body2"
                  underline="hover"
                  sx={{ color: 'text.secondary' }}
                >
                  {label(entry)}
                </Link>
              ) : (
                <Link
                  key={entry.to}
                  component={RouterLink}
                  to={entry.to}
                  variant="body2"
                  underline="hover"
                  sx={{ color: 'text.secondary' }}
                >
                  {label(entry)}
                </Link>
              )
            )}
          </Stack>
        </Stack>
      </Box>
      <Typography variant="caption" sx={{ color: 'text.disabled', display: 'block', mt: 4 }}>
        © {new Date().getFullYear()} Neb Abera
        {' · '}
        {/* Plain hrefs, not router links: these pages are rendered by the
            server (CompliancePages.cs) so campaign vetting sees real HTML,
            and a router link would swallow them into the SPA's empty shell. */}
        <Link href="/sms-privacy" underline="hover" sx={{ color: 'text.disabled' }}>
          Privacy
        </Link>
        {' · '}
        <Link href="/sms-terms" underline="hover" sx={{ color: 'text.disabled' }}>
          Text message terms
        </Link>
      </Typography>
    </Container>
  );
}
