import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import IconButton from '@mui/material/IconButton';
import LinkedInIcon from '@mui/icons-material/LinkedIn'
import CalendarMonthIcon from '@mui/icons-material/CalendarMonth';
import GitHubIcon from '@mui/icons-material/GitHub';

export default function Hero() {
  return (
    <Box
      id="hero"
      sx={(theme) => ({
        width: '100%',
        backgroundRepeat: 'no-repeat',

        backgroundImage:
          'radial-gradient(ellipse 80% 50% at 50% -20%, hsl(210, 100%, 90%), transparent)',
        ...theme.applyStyles('dark', {
          backgroundImage:
            'radial-gradient(ellipse 80% 50% at 50% -20%, hsl(210, 100%, 16%), transparent)',
        }),
      })}
    >
      <Container
        sx={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          pt: { xs: 14, sm: 20 },
          pb: { xs: 8, sm: 12 },
        }}
      >
        <Stack
          spacing={2}
          useFlexGap
          sx={{ alignItems: 'center', width: { xs: '100%', sm: '70%' } }}
        >
          <Typography
            variant="h1"
            sx={{
              display: 'flex',
              flexDirection: { xs: 'column', sm: 'row' },
              alignItems: 'center',
              fontSize: 'clamp(3rem, 10vw, 3.5rem)',
            }}
          >
            Neb&nbsp;
            <Typography
              component="span"
              variant="h1"
              sx={(theme) => ({
                fontSize: 'inherit',
                color: 'primary.main',
                ...theme.applyStyles('dark', {
                  color: 'primary.light',
                }),
              })}
            >
              Abera
            </Typography>
          </Typography>
          <Typography
            sx={{
              textAlign: 'center',
              color: 'text.primary',
            }}
          >
                      I am a Software Engineer from Chicago. I am an avid reader, and I love learning how to solve problems that require an interdisciplinary approach.
                      If you have any book recommendations please send them my way! I am currently working on projects in C++, TypeScript, and C#.
          </Typography>
                  <Stack
                      direction="row"
                      spacing={1}
                      useFlexGap
                      sx={{ justifyContent: 'left', color: 'text.secondary' }}
                  >
                      <IconButton
                          color="inherit"
                          size="large"
                          href="https://www.linkedin.com/in/neb-abera/"
                          aria-label="LinkedIn"
                          sx={{ alignSelf: 'center' }}
                      >
                          <LinkedInIcon />
                      </IconButton>
                      <IconButton
                          color="inherit"
                          size="large"
                          href="https://github.com/neb-abera"
                          aria-label="GitHub"
                          sx={{ alignSelf: 'center' }}
                      >
                          <GitHubIcon />
                      </IconButton>
                      <IconButton
                          color="inherit"
                          size="large"
                          href="https://calendar.app.google/5yRXc1xbu1S2TTjA8"
                          aria-label="Google"
                          sx={{ alignSelf: 'center' }}
                      >
                          <CalendarMonthIcon />
                      </IconButton>
                  </Stack>

        </Stack>
      </Container>
    </Box>
  );
}
