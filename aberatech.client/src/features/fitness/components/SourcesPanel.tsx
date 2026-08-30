import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Link from "@mui/material/Link";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import * as React from "react";
import { type Citation, fetchCitations } from "../core/api";

/**
 * Every model on the site, sourced — the people, the works, the years.
 * Discipline-matched on purpose: running claims cite running coaches and
 * physiologists; strength claims cite strength researchers.
 */
export default function SourcesPanel() {
  const [citations, setCitations] = React.useState<Citation[] | null>(null);

  React.useEffect(() => {
    fetchCitations().then(setCitations).catch(() => setCitations([]));
  }, []);

  if (citations === null) {
    return <CircularProgress size={28} aria-label="Loading sources" />;
  }

  return (
    <Stack spacing={2}>
      {citations.map((citation) => (
        <Card key={citation.id} variant="outlined">
          <CardContent>
            <Chip
              size="small"
              label={citation.id}
              sx={{ mb: 1, fontFamily: "monospace" }}
            />
            <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
              {citation.claim}
            </Typography>
            <Typography
              variant="body2"
              sx={{ color: "text.secondary", mt: 0.5 }}
            >
              {citation.who}
            </Typography>
            <Typography variant="body2" sx={{ mt: 0.5 }}>
              {citation.url ? (
                <Link href={citation.url} target="_blank" rel="noreferrer">
                  {citation.work}
                </Link>
              ) : (
                citation.work
              )}{" "}
              ({citation.year})
            </Typography>
          </CardContent>
        </Card>
      ))}
    </Stack>
  );
}
