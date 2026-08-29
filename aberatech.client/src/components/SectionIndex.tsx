import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import Box from "@mui/material/Box";
import Card from "@mui/material/Card";
import CardActionArea from "@mui/material/CardActionArea";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { Link } from "react-router";
import type { Entry } from "../site/sections";
import PageShell from "./PageShell";

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
          display: "grid",
          gap: 2,
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            md: "repeat(3, 1fr)",
          },
        }}
      >
        {entries.map((entry) => (
          <Card key={entry.to} variant="outlined" sx={{ height: "100%" }}>
            <CardActionArea
              {...(entry.external
                ? {
                    component: "a",
                    href: entry.to,
                    target: "_blank",
                    rel: "noopener noreferrer",
                  }
                : { component: Link, to: entry.to })}
              sx={{
                // '&&' because CardActionArea's own styleOverride sets
                // display:block and lands after sx in the stylesheet.
                "&&": {
                  height: "100%",
                  p: 2.5,
                  display: "flex",
                  flexDirection: "column",
                  alignItems: "stretch",
                  justifyContent: "flex-start",
                },
              }}
            >
              {/* The card fills its grid cell and the path caption is pushed to
                  the foot of it, so a title that wraps to two lines does not
                  drag its card's caption 70px below the others in the row. */}
              <Stack spacing={1} sx={{ flexGrow: 1, width: "100%" }}>
                <Stack
                  direction="row"
                  spacing={0.75}
                  sx={{ alignItems: "center" }}
                >
                  <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                    {entry.title}
                  </Typography>
                  {entry.external && (
                    <OpenInNewIcon
                      sx={{ fontSize: 15, color: "text.disabled" }}
                    />
                  )}
                </Stack>
                <Typography
                  variant="body2"
                  sx={{ color: "text.secondary", flexGrow: 1 }}
                >
                  {entry.blurb}
                </Typography>
                <Typography variant="caption" sx={{ color: "text.disabled" }}>
                  {entry.external ? "Opens on another site" : entry.to}
                </Typography>
              </Stack>
            </CardActionArea>
          </Card>
        ))}
      </Box>
    </PageShell>
  );
}
