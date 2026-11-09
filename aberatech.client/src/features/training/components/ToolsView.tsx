import Button from "@mui/material/Button";
import Link from "@mui/material/Link";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { useEffect, useState } from "react";
import { ankiCsv } from "../core/assessment";
import type { TrainingDocument } from "../core/progress";
import { type Syllabus, syllabus } from "../core/syllabus";

const handlePattern = /^[A-Za-z0-9_.-]{3,24}$/;

function Handle({
  label,
  value,
  save,
}: {
  label: string;
  value: string;
  save: (value: string) => void;
}) {
  const [draft, setDraft] = useState(value);
  useEffect(() => setDraft(value), [value]);
  const valid = draft === "" || handlePattern.test(draft);
  return (
    <TextField
      size="small"
      label={label}
      value={draft}
      error={!valid}
      helperText={
        valid
          ? "Read live, never stored."
          : "3 to 24 letters, digits, dots, hyphens, or underscores."
      }
      onChange={(event) => setDraft(event.target.value.trim())}
      onBlur={() => {
        if (valid && draft !== value) save(draft);
      }}
    />
  );
}

/**
 * Each system the syllabus sends you to, once: what it is for, and how it
 * is recorded. The owner sets the judge handles here.
 */
export default function ToolsView({
  from = syllabus,
  settings,
  change,
}: {
  from?: Syllabus;
  settings?: TrainingDocument["settings"];
  change?: (fn: (current: TrainingDocument) => TrainingDocument) => void;
}) {
  return (
    <Stack spacing={2.5}>
      {settings && change && (
        <Paper
          variant="outlined"
          component="section"
          aria-label="Handles"
          sx={{ p: 2 }}
        >
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <Handle
              label="Codeforces handle"
              value={settings.codeforces}
              save={(codeforces) =>
                change((current) => ({
                  ...current,
                  settings: { ...current.settings, codeforces },
                }))
              }
            />
            <Handle
              label="AtCoder handle"
              value={settings.atcoder}
              save={(atcoder) =>
                change((current) => ({
                  ...current,
                  settings: { ...current.settings, atcoder },
                }))
              }
            />
          </Stack>
        </Paper>
      )}
      <AnkiExport from={from} />
      <Stack component="ul" spacing={1.5} sx={{ p: 0, m: 0 }}>
        {from.systems.map((system) => (
          <Paper
            key={system.id}
            variant="outlined"
            component="li"
            sx={{ p: 2, listStyle: "none" }}
          >
            <Typography
              variant="subtitle1"
              component="h3"
              sx={{ fontWeight: 600 }}
            >
              {system.url.startsWith("http") ? (
                <Link href={system.url} target="_blank" rel="noopener">
                  {system.name}
                </Link>
              ) : (
                system.name
              )}
            </Typography>
            <Typography variant="body2">{system.for}</Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {system.records}
            </Typography>
          </Paper>
        ))}
      </Stack>
    </Stack>
  );
}

/**
 * Every flashcard in the syllabus as a file Anki imports: File, Import,
 * then the file. It lands in the deck "abera.tech training", tagged by unit
 * and lesson.
 */
function AnkiExport({ from }: { from: Syllabus }) {
  const { text, cards } = ankiCsv(from);
  function download() {
    const url = URL.createObjectURL(
      new Blob([text], { type: "text/csv;charset=utf-8" }),
    );
    const link = document.createElement("a");
    link.href = url;
    link.download = "abera-tech-training-cards.csv";
    link.click();
    URL.revokeObjectURL(url);
  }
  return (
    <Paper
      variant="outlined"
      component="section"
      aria-label="Flashcards for Anki"
      sx={{ p: 2 }}
    >
      <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 600 }}>
        Flashcards for Anki
      </Typography>
      <Typography variant="body2">
        {cards === 0
          ? "No flashcards are written yet."
          : `${cards} cards. In Anki, choose File, Import, and this file. They go to the deck "abera.tech training", tagged by unit and lesson.`}
      </Typography>
      <Button
        size="small"
        variant="outlined"
        disabled={cards === 0}
        onClick={download}
        sx={{ mt: 1 }}
      >
        Download for Anki
      </Button>
    </Paper>
  );
}
