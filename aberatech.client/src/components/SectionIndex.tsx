import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardActionArea from '@mui/material/CardActionArea';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import { Link } from 'react-router';
import PageShell from './PageShell';
import type { Entry } from '../site/sections';

interface Props {
  title: string;
  entries: Entry[];
  disableCustomTheme?: boolean;
}

/**
 * The shared shape of /guides and /projects: the same page with different
 * contents. A card is a single CardActionArea, so the whole card is the link
 * rather than the words inside it.
 */
export default function SectionIndex({ title, entries, ...props }: Props) {
  return (
    <PageShell {...props} title={title}>
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
    </PageShell>
  );
}
