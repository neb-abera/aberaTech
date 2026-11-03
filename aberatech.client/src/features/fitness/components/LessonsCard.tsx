import Alert from "@mui/material/Alert";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Typography from "@mui/material/Typography";
import * as React from "react";
import Blocks from "../../playbook/components/Blocks";
import type { PlaybookPage } from "../../playbook/core/api";

type Lessons =
  | { configured: true; page: PlaybookPage | null }
  | { configured: false; message: string };

/**
 * The athlete's lessons learned, read live from his Notion page at each
 * visit. Notion stays the record: nothing here is stored.
 */
export default function LessonsCard() {
  const [lessons, setLessons] = React.useState<Lessons | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    const controller = new AbortController();
    fetch("/api/fitness/lessons", { signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) throw new Error(await response.text());
        setLessons((await response.json()) as Lessons);
      })
      .catch((e: unknown) => {
        if (controller.signal.aborted) return;
        setError(e instanceof Error ? e.message : String(e));
      });
    return () => controller.abort();
  }, []);

  return (
    <Card
      variant="outlined"
      component="section"
      aria-labelledby="lessons-heading"
    >
      <CardContent>
        <Typography variant="h6" component="h2" id="lessons-heading">
          Lessons learned
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          From your Notion page, read at each visit.
        </Typography>
        {error ? (
          <Alert severity="warning">Notion did not answer: {error}</Alert>
        ) : lessons === null ? (
          <Typography variant="body2">Loading…</Typography>
        ) : !lessons.configured ? (
          <Alert severity="info">{lessons.message}</Alert>
        ) : lessons.page ? (
          <Blocks blocks={lessons.page.blocks} />
        ) : (
          <Alert severity="info">The lessons page is empty.</Alert>
        )}
      </CardContent>
    </Card>
  );
}
