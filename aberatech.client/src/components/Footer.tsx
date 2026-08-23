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

/**
 * The site footer. It carries the identity on every page, which is why the
 * pages themselves no longer open with a full height introduction.
 *
 * This replaced the Material UI template footer, which shipped with a newsletter
 * form that went nowhere, a copyright line crediting Sitemark, and social links
 * pointing at Material UI's own accounts rather than mine.
 */
const pages = [
  { label: 'Home', to: '/' },
  { label: 'Military Transition Guide', to: '/transition' },
  { label: 'Learning Software Development', to: '/technical' },
  { label: 'Learning RF and Signal Processing', to: '/planner' },
  { label: 'Schedule time with me', to: '/schedule' }
];

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
            Electrical engineer working in signal processing and RF, with .NET and React on the side.
          </Typography>
          <Stack direction="row" spacing={0.5} sx={{ ml: -1, mt: 0.5 }}>
            {socials.map(({ label, href, Icon }) => (
              <IconButton key={label} size="small" href={href} target="_blank" rel="noopener" aria-label={label}>
                <Icon fontSize="small" />
              </IconButton>
            ))}
          </Stack>
        </Stack>

        <Stack spacing={0.75} component="nav" aria-label="Footer">
          {pages.map(({ label, to }) => (
            <Link
              key={to}
              component={RouterLink}
              to={to}
              variant="body2"
              underline="hover"
              sx={{ color: 'text.secondary' }}
            >
              {label}
            </Link>
          ))}
          <Link
            href="https://facewoof.abera.tech"
            target="_blank"
            rel="noopener"
            variant="body2"
            underline="hover"
            sx={{ color: 'text.secondary' }}
          >
            Facewoof
          </Link>
        </Stack>
      </Box>
      <Typography variant="caption" sx={{ color: 'text.disabled', display: 'block', mt: 4 }}>
        © {new Date().getFullYear()} Neb Abera
      </Typography>
    </Container>
  );
}
