import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import CssBaseline from '@mui/material/CssBaseline';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import AppAppBar from './AppAppBar';
import Footer from './Footer';
import AppTheme from '../theme/AppTheme';
import { pageBackground } from '../theme/pageBackground';

export interface PageShellProps {
  title: string;
  intro?: ReactNode;
  /** A note under the intro, for provenance or caveats. */
  note?: ReactNode;
  /** Reading pages want lg. A wide board wants xl. */
  maxWidth?: 'md' | 'lg' | 'xl';
  children: ReactNode;
  disableCustomTheme?: boolean;
}

/**
 * One page layout, used by every page except the home page.
 *
 * Before this the seven pages had four different container widths and three
 * different top paddings between them, so headings sat at a different height
 * and a different left edge on each one. The shell fixes the chrome, the
 * gradient, the spacing above the title and the centred header; the page
 * supplies only its own body.
 */
export default function PageShell({ title, intro, note, maxWidth = 'lg', children, ...props }: PageShellProps) {
  return (
    <AppTheme {...props}>
      <CssBaseline enableColorScheme />
      <AppAppBar />
      <Box sx={pageBackground}>
        <Container maxWidth={maxWidth}>
          <Stack
            spacing={1.5}
            sx={{
              alignItems: 'center',
              textAlign: 'center',
              pt: { xs: 14, sm: 18 },
              pb: { xs: 5, sm: 7 }
            }}
          >
            <Typography variant="h3" component="h1" sx={{ fontWeight: 600 }}>
              {title}
            </Typography>
            {intro && (
              <Typography variant="body1" sx={{ color: 'text.secondary', maxWidth: 680 }}>
                {intro}
              </Typography>
            )}
            {note && (
              <Typography variant="caption" sx={{ color: 'text.disabled', maxWidth: 680 }}>
                {note}
              </Typography>
            )}
          </Stack>
        </Container>
      </Box>
      <Container maxWidth={maxWidth} sx={{ pb: { xs: 6, sm: 10 } }}>
        {children}
      </Container>
      <Footer />
    </AppTheme>
  );
}
