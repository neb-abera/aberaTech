import EmailIcon from "@mui/icons-material/Email";
import GitHubIcon from "@mui/icons-material/GitHub";
import InstagramIcon from "@mui/icons-material/Instagram";
import LinkedInIcon from "@mui/icons-material/LinkedIn";
import Avatar from "@mui/material/Avatar";
import Box from "@mui/material/Box";
import Container from "@mui/material/Container";
import IconButton from "@mui/material/IconButton";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { tagline } from "../site/meta";
import { pageBackground } from "../theme/pageBackground";

/**
 * The home page: the photo, the name, one line, and the ways to reach me.
 *
 * This replaced three paragraphs about enjoying problem-solving, the
 * languages I was using and what I did with free time. The line is the one
 * LinkedIn carries; the guides and projects are one click away in the bar.
 */

const contacts = [
  {
    label: "LinkedIn",
    href: "https://www.linkedin.com/in/neb-abera/",
    Icon: LinkedInIcon,
  },
  { label: "GitHub", href: "https://github.com/neb-abera", Icon: GitHubIcon },
  {
    label: "Email",
    href: "mailto:support@alias.abera.tech",
    Icon: EmailIcon,
  },
  {
    label: "Instagram",
    href: "https://www.instagram.com/neb_abera",
    Icon: InstagramIcon,
  },
];

export default function Hero() {
  return (
    <Box id="hero" sx={pageBackground}>
      <Container
        sx={{
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          pt: { xs: 14, sm: 20 },
          pb: { xs: 8, sm: 12 },
        }}
      >
        <Stack
          spacing={2}
          useFlexGap
          sx={{ alignItems: "center", width: { xs: "100%", sm: "70%" } }}
        >
          {/* The same crop as LinkedIn, so the two profiles read as one
              person. Served from /headshot.jpg rather than a hashed asset so
              the structured data in site/meta.ts can name a stable URL. */}
          <Avatar
            src="/headshot.jpg"
            alt="Neb Abera"
            sx={{
              width: { xs: 144, sm: 168 },
              height: { xs: 144, sm: 168 },
              border: "3px solid",
              borderColor: "divider",
            }}
          />
          <Typography
            variant="h1"
            sx={{
              display: "flex",
              flexDirection: { xs: "column", sm: "row" },
              alignItems: "center",
              fontSize: "clamp(3rem, 10vw, 3.5rem)",
            }}
          >
            Neb&nbsp;
            <Typography
              component="span"
              variant="h1"
              sx={(theme) => ({
                fontSize: "inherit",
                color: "primary.main",
                ...theme.applyStyles("dark", {
                  color: "primary.light",
                }),
              })}
            >
              Abera
            </Typography>
          </Typography>
          <Typography
            variant="h6"
            component="p"
            sx={{ textAlign: "center", color: "text.primary", fontWeight: 500 }}
          >
            {tagline}
          </Typography>
          <Stack
            direction="row"
            spacing={1}
            useFlexGap
            sx={{ justifyContent: "center", color: "text.secondary" }}
          >
            {contacts.map(({ label, href, Icon }) => (
              <IconButton
                key={label}
                color="inherit"
                size="large"
                href={href}
                aria-label={label}
                sx={{ alignSelf: "center" }}
              >
                <Icon />
              </IconButton>
            ))}
          </Stack>
        </Stack>
      </Container>
    </Box>
  );
}
