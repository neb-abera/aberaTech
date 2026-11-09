import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Link from "@mui/material/Link";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { Link as RouterLink } from "react-router";
import { clock, localDateOf, longDate } from "../core/dates";
import {
  checkpointState,
  countItems,
  lessonDone,
  type Solves,
  startCheckpoint,
  stopCheckpoint,
  type TrainingDocument,
  type UnitState,
} from "../core/progress";
import {
  formatMinutes,
  lessonMinutes,
  trackLabel,
  type Unit,
} from "../core/syllabus";
import ItemCard from "./ItemCard";
import { stateLabel } from "./MapView";

function Checkpoint({
  unit,
  owner,
  document,
  solves,
  change,
  now,
}: {
  unit: Unit;
  owner: boolean;
  document: TrainingDocument;
  solves: Solves;
  change: (fn: (current: TrainingDocument) => TrainingDocument) => void;
  now: Date;
}) {
  const checkpoint = unit.checkpoint;
  if (!checkpoint) return null;
  const state = checkpointState(unit, document, solves, now);
  const attempts = document.checkpoints[unit.id] ?? [];
  return (
    <Paper
      variant="outlined"
      component="section"
      aria-label="Checkpoint"
      sx={{ p: 2.5 }}
    >
      <Stack spacing={1.5}>
        <Typography variant="h2" sx={{ fontSize: "1.25rem" }}>
          Checkpoint: {checkpoint.items.length} problems in {checkpoint.minutes}{" "}
          minutes
        </Typography>
        <Typography variant="body2">{checkpoint.rule}</Typography>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Pass it and the unit is placed out. Its lessons stay here for
          reference, and the next unit opens.
        </Typography>
        {owner && state.state === "none" && (
          <Box>
            <Button
              variant="contained"
              onClick={() =>
                change((current) =>
                  startCheckpoint(current, unit.id, new Date()),
                )
              }
            >
              Start the checkpoint
            </Button>
          </Box>
        )}
        {owner && state.state === "running" && (
          <Alert
            severity="info"
            action={
              <Button
                color="inherit"
                size="small"
                onClick={() =>
                  change((current) =>
                    stopCheckpoint(current, unit.id, new Date()),
                  )
                }
              >
                Stop
              </Button>
            }
          >
            Started {clock(state.started)}. Time is up at {clock(state.ends)}.
          </Alert>
        )}
        {owner && state.state === "passed" && (
          <Alert severity="success">
            Placed out on {longDate(localDateOf(state.at))}: started{" "}
            {clock(state.started)}, last problem done {clock(state.at)}.
          </Alert>
        )}
        {owner && state.state === "failed" && (
          <Alert
            severity="warning"
            action={
              <Button
                color="inherit"
                size="small"
                onClick={() =>
                  change((current) =>
                    startCheckpoint(current, unit.id, new Date()),
                  )
                }
              >
                Try again
              </Button>
            }
          >
            Not passed on {longDate(localDateOf(state.started))}. Work the
            lessons below. {attempts.length} attempt
            {attempts.length === 1 ? "" : "s"} so far.
          </Alert>
        )}
        <Stack spacing={1.5}>
          {checkpoint.items.map((item) => (
            <ItemCard
              key={item.id}
              item={item}
              owner={owner && state.state !== "none"}
              document={document}
              solves={solves}
              change={change}
            />
          ))}
        </Stack>
      </Stack>
    </Paper>
  );
}

/**
 * One unit: its checkpoint, then its lessons in order. A placed-out unit
 * looks the same, with the date it was placed out: everything in it stays
 * open for reference.
 */
export default function UnitView({
  unit,
  index,
  total,
  owner,
  document,
  solves,
  state,
  change,
  now,
}: {
  unit: Unit;
  index: number;
  total: number;
  owner: boolean;
  document: TrainingDocument;
  solves: Solves;
  state?: UnitState;
  change: (fn: (current: TrainingDocument) => TrainingDocument) => void;
  now: Date;
}) {
  return (
    <Stack spacing={3} component="section" aria-label={unit.title}>
      <Box>
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          <Link component={RouterLink} to="/training?view=map">
            Map
          </Link>{" "}
          › {trackLabel[unit.track]} · unit {index} of {total}
          {owner && state ? ` · ${stateLabel(state)}` : ""}
        </Typography>
        <Typography variant="h2" sx={{ fontSize: "1.75rem" }}>
          {unit.title}
        </Typography>
        {unit.why && (
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {unit.why}
          </Typography>
        )}
      </Box>
      <Checkpoint
        unit={unit}
        owner={owner}
        document={document}
        solves={solves}
        change={change}
        now={now}
      />
      <Stack component="ol" spacing={1} sx={{ p: 0, m: 0 }}>
        {unit.lessons.map((lesson, position) => {
          const count = countItems(lesson.items, document, solves);
          return (
            <Paper
              key={lesson.id}
              variant="outlined"
              component="li"
              sx={{ p: 1.5, listStyle: "none" }}
            >
              <Typography variant="body1">
                {position + 1}.{" "}
                <Link
                  component={RouterLink}
                  to={`/training?lesson=${lesson.id}`}
                >
                  {lesson.title}
                </Link>
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {lesson.items.length} items,{" "}
                {formatMinutes(lessonMinutes(lesson))}
                {owner
                  ? lessonDone(lesson, document, solves)
                    ? ". Done."
                    : `. ${count.done} of ${count.total} done.`
                  : "."}
              </Typography>
            </Paper>
          );
        })}
      </Stack>
    </Stack>
  );
}
