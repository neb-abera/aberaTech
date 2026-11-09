import Box from "@mui/material/Box";
import Chip from "@mui/material/Chip";
import LinearProgress from "@mui/material/LinearProgress";
import Link from "@mui/material/Link";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { Link as RouterLink } from "react-router";
import { localDateOf, longDate } from "../core/dates";
import {
  countItems,
  type Solves,
  type TrainingDocument,
  type UnitState,
} from "../core/progress";
import {
  checkpointLabel,
  countOf,
  formatMinutes,
  type Phase,
  type Syllabus,
  syllabus,
  trackLabel,
  type Unit,
  unitMinutes,
} from "../core/syllabus";

export function stateLabel(state: UnitState | undefined): string {
  switch (state?.state) {
    case "locked":
      return "Locked";
    case "waiting":
      return `Opens after ${state.after.title}`;
    case "open":
      return "Open";
    case "done":
      return "Done";
    case "placed":
      return `Placed out ${longDate(localDateOf(state.at))}`;
    default:
      return "";
  }
}

function UnitRow({
  unit,
  index,
  owner,
  document,
  solves,
  state,
}: {
  unit: Unit;
  index: number;
  owner: boolean;
  document: TrainingDocument;
  solves: Solves;
  state?: UnitState;
}) {
  const items = unit.lessons.flatMap((lesson) => lesson.items);
  const count = countItems(items, document, solves);
  const locked = !!unit.locked;
  return (
    <Paper
      variant="outlined"
      component="li"
      aria-label={unit.title}
      sx={{ p: 2, listStyle: "none", opacity: locked ? 0.6 : 1 }}
    >
      <Stack spacing={0.75}>
        <Stack
          direction="row"
          spacing={1}
          sx={{ alignItems: "baseline", flexWrap: "wrap", rowGap: 0.5 }}
        >
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {index}. {trackLabel[unit.track]}
          </Typography>
          <Typography
            variant="subtitle1"
            component="h3"
            sx={{ fontWeight: 600 }}
          >
            {locked ? (
              unit.title
            ) : (
              <Link component={RouterLink} to={`/training?unit=${unit.id}`}>
                {unit.title}
              </Link>
            )}
          </Typography>
          {owner && state && (
            <Chip
              size="small"
              label={stateLabel(state)}
              color={
                state.state === "done" || state.state === "placed"
                  ? "success"
                  : state.state === "open"
                    ? "primary"
                    : "default"
              }
            />
          )}
        </Stack>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {locked
            ? unit.locked
            : `${countOf(unit.lessons.length, "lesson")}, ${formatMinutes(unitMinutes(unit))}${unit.checkpoint ? `. Checkpoint: ${checkpointLabel(unit)}` : ""}.`}
        </Typography>
        {owner && !locked && (
          <Box>
            <LinearProgress
              variant="determinate"
              value={
                state?.state === "placed"
                  ? 100
                  : count.total === 0
                    ? 0
                    : (count.done / count.total) * 100
              }
              aria-label={`${unit.title} progress`}
            />
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {count.done} of {count.total} items
            </Typography>
          </Box>
        )}
      </Stack>
    </Paper>
  );
}

function PhaseSection({
  phase,
  units,
  start,
  owner,
  document,
  solves,
  states,
}: {
  phase: Phase;
  units: Unit[];
  start: number;
  owner: boolean;
  document: TrainingDocument;
  solves: Solves;
  states: Map<string, UnitState>;
}) {
  return (
    <Box component="section" aria-label={phase.title}>
      <Typography variant="h2" sx={{ fontSize: "1.4rem", mb: 0.5 }}>
        {phase.title}
      </Typography>
      <Typography variant="body2" sx={{ color: "text.secondary", mb: 1.5 }}>
        Target dates {longDate(phase.start)} to {longDate(phase.end)}.{" "}
        {phase.hoursPerWeek} hours a week, from the plan. A unit opens when the
        one before it is finished. {phase.goals.join(" ")}
      </Typography>
      <Stack component="ol" spacing={1.5} sx={{ p: 0, m: 0 }}>
        {units.map((unit, offset) => (
          <UnitRow
            key={unit.id}
            unit={unit}
            index={start + offset}
            owner={owner}
            document={document}
            solves={solves}
            state={states.get(unit.id)}
          />
        ))}
      </Stack>
    </Box>
  );
}

/**
 * Every phase and unit in order. A visitor sees the syllabus. The owner
 * sees each unit's state and progress besides.
 */
export default function MapView({
  owner,
  document,
  solves,
  states,
  from = syllabus,
}: {
  owner: boolean;
  document: TrainingDocument;
  solves: Solves;
  states: Map<string, UnitState>;
  from?: Syllabus;
}) {
  let start = 1;
  return (
    <Stack spacing={4}>
      {from.phases.map((phase) => {
        const units = from.units.filter((unit) => unit.phase === phase.phase);
        const first = start;
        start += units.length;
        return (
          <PhaseSection
            key={phase.phase}
            phase={phase}
            units={units}
            start={first}
            owner={owner}
            document={document}
            solves={solves}
            states={states}
          />
        );
      })}
    </Stack>
  );
}
