import Link from "@mui/material/Link";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { useState } from "react";
import { Link as RouterLink } from "react-router";
import { clock, localDateOf, shortDate } from "../core/dates";
import { searchNotes } from "../core/notes";
import type { TrainingDocument } from "../core/progress";
import { countOf } from "../core/syllabus";

/** Every note, searchable, each with the lesson it belongs to. */
export default function NotesView({
  document,
}: {
  document: TrainingDocument;
}) {
  const [query, setQuery] = useState("");
  const hits = searchNotes(document, query);
  const total = Object.keys(document.notes).length;
  return (
    <Stack spacing={2} component="section" aria-label="Notes">
      <TextField
        size="small"
        type="search"
        label="Search notes"
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        helperText="Every word must appear in the note or its title."
      />
      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        {query.trim()
          ? `${countOf(hits.length, "note")} of ${total} match.`
          : `${countOf(total, "note")}, newest first.`}
      </Typography>
      <Stack component="ul" spacing={1} sx={{ p: 0, m: 0 }}>
        {hits.map((hit) => (
          <Paper
            key={hit.id}
            variant="outlined"
            component="li"
            sx={{ p: 1.5, listStyle: "none" }}
          >
            <Typography variant="subtitle2" component="h3">
              <Link
                component={RouterLink}
                to={
                  hit.lessonId
                    ? `/training?lesson=${hit.lessonId}`
                    : `/training?unit=${hit.unitId}`
                }
              >
                {hit.title}
              </Link>
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {hit.unitTitle} · kept {shortDate(localDateOf(hit.note.at))}{" "}
              {clock(hit.note.at)}
            </Typography>
            <Typography variant="body2" sx={{ whiteSpace: "pre-wrap" }}>
              {hit.note.text}
            </Typography>
          </Paper>
        ))}
      </Stack>
    </Stack>
  );
}
