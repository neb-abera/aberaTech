import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import Link from "@mui/material/Link";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { ReactNode } from "react";
import { Link as RouterLink } from "react-router";
import { longDate } from "../core/dates";
import type { TrainingDocument } from "../core/progress";
import {
  answerReview,
  type Grade,
  preview,
  Rating,
  relearnDays,
  reviewMinutesBasis,
  type Scheduled,
  schedulerSource,
} from "../core/reviews";
import { countOf, formatMinutes, trackLabel } from "../core/syllabus";

const grades: { rating: Grade; label: string; meaning: string }[] = [
  { rating: Rating.Again, label: "Again", meaning: "missed it" },
  { rating: Rating.Hard, label: "Hard", meaning: "right, with effort" },
  { rating: Rating.Good, label: "Good", meaning: "right" },
  { rating: Rating.Easy, label: "Easy", meaning: "right, at once" },
];

/** What to do for one review, by what it is. */
function instructions(entry: Scheduled): string {
  const { source } = entry;
  if (source.kind === "lesson")
    return "Without looking, say or write the lesson's main ideas and one example of each. Then check against your notes and the source.";
  return "Solve it again from a blank page, without your old solution. Then rate how it went.";
}

const days = (n: number): string => countOf(n, "day");

/**
 * A review session: the due reviews one at a time, mixed across units.
 * Each answer goes to FSRS and sets the next date.
 */
export default function ReviewView({
  due,
  document,
  change,
  now,
  tomorrow,
  week,
  extra,
}: {
  due: Scheduled[];
  document: TrainingDocument;
  change: (fn: (current: TrainingDocument) => TrainingDocument) => void;
  now: Date;
  /** How many come due tomorrow, and over the next seven days. */
  tomorrow: number;
  week: number;
  /** A renderer for review kinds the assessment increment adds. */
  extra?: (entry: Scheduled, answer: (rating: Grade) => void) => ReactNode;
}) {
  const current = due[0];
  const minutes = due.reduce((sum, entry) => sum + entry.source.minutes, 0);
  return (
    <Stack spacing={2.5} component="section" aria-label="Review">
      <Box>
        <Typography variant="h2" sx={{ fontSize: "1.5rem" }}>
          Review
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {countOf(due.length, "review")} due, about {formatMinutes(minutes)}.{" "}
          {tomorrow} more due tomorrow, {week} over the next 7 days.
        </Typography>
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          {schedulerSource} {reviewMinutesBasis} Reviews are mixed across units,
          so no two in a row come from one unit while another waits. A miss
          comes back each day until it is right on {relearnDays} later days.
        </Typography>
      </Box>
      {!current ? (
        <Alert severity="success">Nothing due. New material is open.</Alert>
      ) : (
        <Paper
          variant="outlined"
          component="article"
          aria-label={`Review: ${current.source.title}`}
          sx={{ p: 2 }}
        >
          <Stack spacing={1.5}>
            <Stack
              direction="row"
              spacing={1}
              sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 1 }}
            >
              <Chip
                size="small"
                label={current.source.kind === "lesson" ? "Lesson" : "Problem"}
              />
              {current.card.relearning && (
                <Chip
                  size="small"
                  color="warning"
                  label={`Relearning: ${current.card.relearning.correct.length} of ${relearnDays} days right`}
                />
              )}
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {trackLabel[current.source.track]} · {current.source.unit.title}{" "}
                · {formatMinutes(current.source.minutes)} · due{" "}
                {longDate(current.due)}
              </Typography>
            </Stack>
            <Typography
              variant="subtitle1"
              component="h3"
              sx={{ fontWeight: 600 }}
            >
              {current.source.title}
            </Typography>
            {extra?.(current, (rating) =>
              change((doc) => answer(doc, current, rating, now)),
            ) ?? (
              <>
                <Typography variant="body2">{instructions(current)}</Typography>
                {current.source.lesson && (
                  <Link
                    component={RouterLink}
                    to={`/training?lesson=${current.source.lesson.id}`}
                  >
                    Open the lesson
                  </Link>
                )}
                <Grades
                  entry={current}
                  document={document}
                  now={now}
                  answer={(rating) =>
                    change((doc) => answer(doc, current, rating, now))
                  }
                />
              </>
            )}
          </Stack>
        </Paper>
      )}
    </Stack>
  );
}

function answer(
  document: TrainingDocument,
  entry: Scheduled,
  rating: Grade,
  now: Date,
): TrainingDocument {
  return answerReview(document, entry.source, rating, now);
}

/** Again, Hard, Good, Easy, each with the interval it would give. */
export function Grades({
  entry,
  document,
  now,
  answer,
}: {
  entry: Scheduled;
  document: TrainingDocument;
  now: Date;
  answer: (rating: Grade) => void;
}) {
  const next = preview(entry.source, document, now);
  return (
    <Stack
      direction="row"
      spacing={1}
      sx={{ flexWrap: "wrap", rowGap: 1 }}
      role="group"
      aria-label="How did it go?"
    >
      {grades.map((grade) => (
        <Button
          key={grade.rating}
          size="small"
          variant={grade.rating === Rating.Again ? "outlined" : "contained"}
          color={grade.rating === Rating.Again ? "warning" : "primary"}
          onClick={() => answer(grade.rating)}
          aria-label={`${grade.label}: ${grade.meaning}. Next in ${days(next[grade.rating])}.`}
        >
          {grade.label} · {days(next[grade.rating])}
        </Button>
      ))}
    </Stack>
  );
}
