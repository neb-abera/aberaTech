import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardActionArea from '@mui/material/CardActionArea';
import Container from '@mui/material/Container';
import CssBaseline from '@mui/material/CssBaseline';
import Divider from '@mui/material/Divider';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import { Link } from 'react-router';
import AppAppBar from './AppAppBar';
import Footer from './Footer';
import AppTheme from '../theme/AppTheme';
import type { Entry } from '../site/sections';

interface Props {
  title: string;
  intro: string;
  entries: Entry[];
}

/**
 * The shared shape of /guides and /projects.
 *
 * Both are the same page with different contents, so they are the same
 * component. A card is a single CardActionArea, which means the whole card is
 * the link rather than the words inside it.
 */
export default function SectionIndex({ title, intro, entries, ...props }: Props & { disableCustomTheme?: boolean }) {
  return (
    <AppTheme {...props}>
      <CssBaseline enableColorScheme />
      <AppAppBar />
      <Container maxWidth="lg" sx={{ pt: { xs: 12, sm: 14 }, pb: 4 }}>
        <Box sx={{ mb: 3 }}>
          <Typography variant="h4" component="h1" sx={{ fontWeight: 600 }}>
            {title}
          </Typography>
          <Typography variant="body1" sx={{ color: 'text.secondary', mt: 1, maxWidth: 700 }}>
            {intro}
          </Typography>
        </Box>
        <Divider sx={{ mb: 3 }} />
        <Box
          sx={{
            display: 'grid',
            gap: 2,
            gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', md: 'repeat(3, 1fr)' }
          }}
        >
          {entries.map((entry) => (
            <Card key={entry.to} variant="outlined" sx={{ height: '100%' }}>
              <CardActionArea
                {...(entry.external
                  ? { component: 'a', href: entry.to, target: '_blank', rel: 'noopener noreferrer' }
                  : { component: Link, to: entry.to })}
                sx={{ height: '100%', p: 2.5, alignItems: 'flex-start', justifyContent: 'flex-start' }}
              >
                <Stack spacing={1}>
                  <Stack direction="row" spacing={0.75} alignItems="center">
                    <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                      {entry.title}
                    </Typography>
                    {entry.external && <OpenInNewIcon sx={{ fontSize: 15, color: 'text.disabled' }} />}
                  </Stack>
                  <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                    {entry.blurb}
                  </Typography>
                  <Typography variant="caption" sx={{ color: 'text.disabled' }}>
                    {entry.external ? 'Opens on another site' : entry.to}
                  </Typography>
                </Stack>
              </CardActionArea>
            </Card>
          ))}
        </Box>
      </Container>
      <Footer />
    </AppTheme>
  );
}
