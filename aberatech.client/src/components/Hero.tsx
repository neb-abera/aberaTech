import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import IconButton from '@mui/material/IconButton';
import LinkedInIcon from '@mui/icons-material/LinkedIn';
import GitHubIcon from '@mui/icons-material/GitHub';
import PhoneIphoneIcon from '@mui/icons-material/PhoneIphone';
import InstagramIcon from '@mui/icons-material/Instagram';
import {pageBackground} from '../theme/pageBackground';

export default function Hero() {
  return (
    <Box
      id="hero"
      sx={pageBackground}
    >
      <Container
        sx={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          pt: { xs: 14, sm: 20 },
          pb: { xs: 8, sm: 12 }
        }}
      >
        <Stack spacing={2} useFlexGap sx={{ alignItems: 'center', width: { xs: '100%', sm: '70%' } }}>
          <Typography
            variant="h1"
            sx={{
              display: 'flex',
              flexDirection: { xs: 'column', sm: 'row' },
              alignItems: 'center',
              fontSize: 'clamp(3rem, 10vw, 3.5rem)'
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
                  color: 'primary.light'
                })
              })}
            >
              Abera
            </Typography>
          </Typography>
          <Typography
            sx={{
              textAlign: 'left',
              color: 'text.primary'
            }}
          >
            I really enjoy detail-oriented problem-solving, and I have years of experience leading teams of technical professionals. I specialize in building secure embedded systems. I have worked on large-scale teams and have independently developed novel research projects to support RF Engineering / Digital Signal Processing programs.
          </Typography>
          <Typography
            sx={{
              textAlign: 'left',
              color: 'text.primary'
            }}
          >
            I am currently working on projects in C++, Python, VHDL in Vivado, and web development (TypeScript/React/HTML/CSS + C# using .NET). When I used to have free time... I dedicated it to Mixed Martial Arts, mostly Brazilian Jiu-Jitsu and Judo.
          </Typography>
          <Typography
              sx={{
                textAlign: 'left',
                color: 'text.primary'
              }}
            >
            I am an avid reader, and I love learning how to solve problems that require an interdisciplinary approach. If you have any book recommendations please send them my way!
          </Typography>
          <Stack direction="row" spacing={1} useFlexGap sx={{ justifyContent: 'left', color: 'text.secondary' }}>
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
              <PhoneIphoneIcon />
            </IconButton>
            <IconButton
              color="inherit"
              size="large"
              href="https://www.instagram.com/neb_abera"
              aria-label="Instagram"
              sx={{ alignSelf: 'center' }}
            >
              <InstagramIcon />
            </IconButton>
          </Stack>
        </Stack>
      </Container>
    </Box>
  );
}
