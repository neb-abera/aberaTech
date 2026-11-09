import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Link from "@mui/material/Link";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { ReactNode } from "react";
import { Link as RouterLink } from "react-router";
import type { Solves, TrainingDocument } from "../core/progress";
import {
  formatMinutes,
  type Lesson,
  lessonMinutes,
  type Unit,
} from "../core/syllabus";
import ItemCard from "./ItemCard";

/**
 * One lesson: each item points to its place in its own system, says what
 * to do there and how it is recorded. `noteFor` renders a note field under
 * the lesson and each item, when the page has one to offer.
 */
export default function LessonView({
  unit,
  lesson,
  owner,
  document,
  solves,
  change,
  noteFor,
}: {
  unit: Unit;
  lesson: Lesson;
  owner: boolean;
  document: TrainingDocument;
  solves: Solves;
  change: (fn: (current: TrainingDocument) => TrainingDocument) => void;
  noteFor?: (id: string, label: string) => ReactNode;
}) {
  const position = unit.lessons.findIndex((other) => other.id === lesson.id);
  const previous = unit.lessons[position - 1];
  const next = unit.lessons[position + 1];
  return (
    <Stack spacing={2.5} component="section" aria-label={lesson.title}>
      <Box>
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          <Link component={RouterLink} to="/training?view=map">
            Map
          </Link>{" "}
          ›{" "}
          <Link component={RouterLink} to={`/training?unit=${unit.id}`}>
            {unit.title}
          </Link>{" "}
          · lesson {position + 1} of {unit.lessons.length}
        </Typography>
        <Typography variant="h2" sx={{ fontSize: "1.75rem" }}>
          {lesson.title}
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {formatMinutes(lessonMinutes(lesson))} in all.
        </Typography>
      </Box>
      {lesson.items.map((item) => (
        <ItemCard
          key={item.id}
          item={item}
          owner={owner}
          document={document}
          solves={solves}
          change={change}
        >
          {owner && noteFor ? noteFor(item.id, `Note on ${item.title}`) : null}
        </ItemCard>
      ))}
      {owner && noteFor ? noteFor(lesson.id, `Lesson notes`) : null}
      <Stack
        direction="row"
        spacing={2}
        sx={{ justifyContent: "space-between" }}
      >
        {previous ? (
          <Button component={RouterLink} to={`/training?lesson=${previous.id}`}>
            Previous: {previous.title}
          </Button>
        ) : (
          <span />
        )}
        {next && (
          <Button component={RouterLink} to={`/training?lesson=${next.id}`}>
            Next: {next.title}
          </Button>
        )}
      </Stack>
    </Stack>
  );
}
