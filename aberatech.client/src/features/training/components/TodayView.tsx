import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import Link from "@mui/material/Link";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { ReactNode } from "react";
import { Link as RouterLink } from "react-router";
import { clock, localDateOf, longDate } from "../core/dates";
import { formatMinutes, kindLabel, systemOf } from "../core/syllabus";
import type { Day, Entry, Today } from "../core/today";

export interface Contest {
  name: string;
  start: string;
  durationMinutes: number;
  url: string;
}

const blockLabel = {
  contest: "Contest",
  upsolve: "Upsolve",
  math: "Math",
  programming: "Programming",
} as const;

function EntryRow({
  entry,
  index,
  contests,
}: {
  entry: Entry;
  index: number;
  contests: Contest[];
}) {
  let kind = "";
  let title: ReactNode = null;
  let detail: ReactNode = null;
  let open: { to: string; label: string; external?: boolean } | null = null;

  switch (entry.kind) {
    case "item": {
      kind = kindLabel[entry.item.kind];
      title = entry.item.title;
      detail = `${systemOf(entry.item).name} · ${entry.unit.title} › ${entry.lesson.title}`;
      open = { to: `/training?lesson=${entry.lesson.id}`, label: "Open" };
      break;
    }
    case "checkpoint": {
      kind = "Checkpoint";
      title = `${entry.unit.title}: ${entry.unit.checkpoint?.items.length} problems in ${entry.minutes} minutes`;
      detail = entry.running
        ? `Running. Time is up at ${clock(entry.running.ends)}.`
        : "Pass it to place out of the unit. Its lessons stay open for reference.";
      open = {
        to: `/training?unit=${entry.unit.id}`,
        label: entry.running ? "Continue" : "Start",
      };
      break;
    }
    case "review": {
      kind = "Review";
      title = entry.review.title;
      detail = `From ${longDate(entry.review.from)}`;
      open = entry.review.lesson
        ? { to: `/training?lesson=${entry.review.lesson.id}`, label: "Open" }
        : null;
      break;
    }
    case "contest": {
      kind = "Contest";
      const next = contests[0];
      title = next
        ? `${next.name}, ${longDate(localDateOf(next.start))} ${clock(next.start)}, ${formatMinutes(next.durationMinutes)}`
        : "A rated Codeforces round or an AtCoder Beginner Contest";
      detail = next
        ? "Next Codeforces round, from the Codeforces API. An AtCoder Beginner Contest counts too."
        : "Codeforces rounds usually start 9:35 ET. AtCoder Beginner Contests start 7:00 ET in winter.";
      open = {
        to: next?.url ?? "https://codeforces.com/contests",
        label: "Register",
        external: true,
      };
      break;
    }
    case "upsolve": {
      kind = "Upsolve";
      title = "The first problem you did not solve in your last contest";
      detail =
        "Read the editorial after 30 minutes stuck, then code it yourself.";
      break;
    }
    case "mixed": {
      kind = "Mixed set";
      title = "Problems from every chapter so far";
      detail = `Every fourth week of the plan, this is week ${entry.week}. In Alcumus, pick topics from all the chapters you have done.`;
      open = {
        to: "https://artofproblemsolving.com/alcumus",
        label: "Alcumus",
        external: true,
      };
      break;
    }
  }

  return (
    <Paper
      variant="outlined"
      component="li"
      sx={{ p: 1.5, listStyle: "none" }}
      aria-label={`${index}. ${kind}`}
    >
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={1.5}
        sx={{ alignItems: { sm: "center" } }}
      >
        <Typography variant="body2" sx={{ minWidth: 24 }}>
          {index}.
        </Typography>
        <Chip
          size="small"
          label={kind}
          sx={{ alignSelf: { xs: "start", sm: "center" } }}
        />
        <Typography variant="body2" sx={{ minWidth: 56 }}>
          {formatMinutes(entry.minutes)}
        </Typography>
        <Box sx={{ flex: 1 }}>
          <Typography variant="body1">{title}</Typography>
          {detail && (
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {detail}
            </Typography>
          )}
        </Box>
        {open &&
          (open.external ? (
            <Button
              size="small"
              variant="outlined"
              href={open.to}
              target="_blank"
              rel="noopener"
            >
              {open.label}
            </Button>
          ) : (
            <Button
              size="small"
              variant="contained"
              component={RouterLink}
              to={open.to}
            >
              {open.label}
            </Button>
          ))}
      </Stack>
    </Paper>
  );
}

function entryKey(entry: Entry): string {
  switch (entry.kind) {
    case "item":
      return entry.item.id;
    case "checkpoint":
      return `checkpoint-${entry.unit.id}`;
    case "review":
      return entry.review.key;
    default:
      return entry.kind;
  }
}

function DayList({ day, contests }: { day: Day; contests: Contest[] }) {
  if (!day.block) return null;
  const over = day.planned > day.block.minutes;
  return (
    <Stack spacing={1}>
      <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
        {blockLabel[day.block.block]}: {formatMinutes(day.block.minutes)}
      </Typography>
      {day.entries.length === 0 ? (
        <Typography variant="body2">
          Nothing is open in this track. Every unit so far is done or placed
          out.
        </Typography>
      ) : (
        <Stack component="ol" spacing={1} sx={{ p: 0, m: 0 }}>
          {day.entries.map((entry, index) => (
            <EntryRow
              key={entryKey(entry)}
              entry={entry}
              index={index + 1}
              contests={contests}
            />
          ))}
        </Stack>
      )}
      <Typography variant="caption" sx={{ color: "text.secondary" }}>
        {formatMinutes(day.planned)} planned
        {over
          ? `, longer than the block because the first item does not split`
          : ""}
        {day.later > 0
          ? `. ${day.later} more in this unit move to the next ${blockLabel[day.block.block].toLowerCase()} block.`
          : "."}
      </Typography>
    </Stack>
  );
}

/**
 * The first screen for the owner: today's block, in order, with minutes,
 * and where each number comes from.
 */
export default function TodayView({
  today,
  contests,
  rating,
}: {
  today: Today;
  contests: Contest[];
  /** The Codeforces line, with its source and time. */
  rating: ReactNode;
}) {
  const block = today.block;
  return (
    <Stack spacing={2.5} component="section" aria-label="Today">
      <Box>
        <Typography variant="h2" sx={{ fontSize: "1.5rem" }}>
          {longDate(today.date)} · Phase {today.phase.phase} · week {today.week}{" "}
          of {today.weeks}
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {today.phase.hoursPerWeek} h this week, from the plan.{" "}
          {today.phase.weekBasis ?? ""}
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {rating}
        </Typography>
      </Box>
      {block ? (
        <DayList day={today} contests={contests} />
      ) : (
        <Stack spacing={1.5}>
          <Typography variant="body1">
            No block today.
            {today.next ? ` Next: ${longDate(today.next.date)}.` : ""}
          </Typography>
          {today.next && <DayList day={today.next} contests={contests} />}
        </Stack>
      )}
      <Typography variant="caption" sx={{ color: "text.secondary" }}>
        The week:{" "}
        {today.phase.week
          .map(
            (entry) =>
              `${["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"][entry.day]} ${blockLabel[entry.block].toLowerCase()} ${formatMinutes(entry.minutes)}`,
          )
          .join(", ")}
        .{" "}
        <Link component={RouterLink} to="/training?view=map">
          The map
        </Link>{" "}
        has every unit.
      </Typography>
    </Stack>
  );
}
