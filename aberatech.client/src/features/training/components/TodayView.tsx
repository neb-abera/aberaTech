import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import Link from "@mui/material/Link";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { type ReactNode, useEffect, useState } from "react";
import { Link as RouterLink } from "react-router";
import { clock, localDateOf, longDate } from "../core/dates";
import type { Pace } from "../core/pace";
import {
  checkpointLabel,
  formatMinutes,
  kindLabel,
  systemOf,
  trackLabel,
} from "../core/syllabus";
import type { Entry, NextUp } from "../core/today";

export interface Contest {
  name: string;
  start: string;
  durationMinutes: number;
  url: string;
  judge?: string;
}

function entryKey(entry: Entry): string {
  switch (entry.kind) {
    case "item":
      return entry.item.id;
    case "checkpoint":
      return `checkpoint-${entry.unit.id}`;
    case "review":
      return entry.review.key;
    case "check":
      return `${entry.check}-${entry.lesson?.id ?? entry.unit.id}`;
  }
}

const checkLabel: Record<string, string> = {
  pretest: "Pretest",
  placement: "Placement",
  quiz: "Quiz",
  mastery: "Mastery check",
  "outside-mastery": "Mastery",
};

function EntryRow({ entry, index }: { entry: Entry; index: number }) {
  let kind: string;
  let title: ReactNode;
  let detail: ReactNode;
  let open: { to: string; label: string } | null;

  switch (entry.kind) {
    case "item":
      kind = kindLabel[entry.item.kind];
      title = entry.item.title;
      detail = `${trackLabel[entry.unit.track]} · ${systemOf(entry.item).name} · ${entry.unit.title} › ${entry.lesson.title}`;
      open = { to: `/training?lesson=${entry.lesson.id}`, label: "Open" };
      break;
    case "checkpoint":
      kind = "Checkpoint";
      title = `${entry.unit.title}: ${checkpointLabel(entry.unit)}`;
      detail = entry.running
        ? `Running. Time is up at ${clock(entry.running.ends)}.`
        : `${trackLabel[entry.unit.track]}. Pass it to place out of the unit. Its lessons stay open for reference.`;
      open = {
        to: `/training?unit=${entry.unit.id}`,
        label: entry.running ? "Continue" : "Start",
      };
      break;
    case "check": {
      kind = checkLabel[entry.check];
      const of = entry.lesson?.title ?? entry.unit.title;
      title = `${kind}: ${of}`;
      const page =
        entry.check === "quiz"
          ? `/training?check=quiz:${entry.lesson?.id}`
          : entry.check === "pretest" || entry.check === "mastery"
            ? `/training?check=${entry.check}:${entry.unit.id}`
            : `/training?unit=${entry.unit.id}`;
      detail =
        entry.check === "pretest"
          ? `${trackLabel[entry.unit.track]}. Before the unit. 90% or more places you out of it.`
          : entry.check === "placement"
            ? `${trackLabel[entry.unit.track]}. ${entry.unit.placement?.label}, scored by hand.`
            : entry.check === "quiz"
              ? `${trackLabel[entry.unit.track]} · ${entry.unit.title}. Ends the lesson.`
              : entry.check === "mastery"
                ? `${trackLabel[entry.unit.track]}. 90% or more closes the unit.`
                : `${trackLabel[entry.unit.track]}. ${entry.unit.externalMastery?.label}, ticked by hand.`;
      open = { to: page, label: "Start" };
      break;
    }
    case "review":
      kind = "Review";
      title = entry.review.title;
      detail = `${trackLabel[entry.review.track]} · ${entry.review.unit.title} · in review since ${longDate(entry.review.from)}, due ${longDate(entry.review.due)}`;
      open = { to: "/training?view=review", label: "Review" };
      break;
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
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {detail}
          </Typography>
        </Box>
        {open && (
          <Button
            size="small"
            variant="contained"
            component={RouterLink}
            to={open.to}
          >
            {open.label}
          </Button>
        )}
      </Stack>
    </Paper>
  );
}

function SessionLength({
  value,
  fallback,
  save,
}: {
  value: number | undefined;
  fallback: number;
  save: (minutes: number | null) => void;
}) {
  const [draft, setDraft] = useState(String(value ?? fallback));
  useEffect(() => setDraft(String(value ?? fallback)), [value, fallback]);
  const minutes = Number(draft);
  const valid = Number.isInteger(minutes) && minutes >= 10 && minutes <= 600;
  return (
    <TextField
      size="small"
      type="number"
      label="Session, minutes"
      value={draft}
      error={!valid}
      helperText={
        value === undefined
          ? `The plan's default: ${fallback}`
          : `Yours. The plan's is ${fallback}.`
      }
      onChange={(event) => setDraft(event.target.value)}
      onBlur={() => {
        if (!valid) return;
        save(minutes === fallback ? null : minutes);
      }}
      sx={{ width: 180 }}
    />
  );
}

function PacePanel({ pace, next }: { pace: Pace; next: NextUp }) {
  const behind = pace.thisWeek < pace.planWeek;
  return (
    <Paper
      variant="outlined"
      component="section"
      aria-label="Pace"
      sx={{ p: 2 }}
    >
      <Stack spacing={1}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          Pace
        </Typography>
        <Typography variant="body2">
          This week, since {longDate(pace.weekFrom)}:{" "}
          {formatMinutes(pace.thisWeek)} of {formatMinutes(pace.planWeek)}.{" "}
          {behind
            ? `${formatMinutes(pace.planWeek - pace.thisWeek)} behind the plan's week.`
            : `${formatMinutes(pace.thisWeek - pace.planWeek)} ahead of the plan's week.`}
        </Typography>
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          Done: the minutes of each item finished this week, by its tick or its
          judge's time. Plan: Phase {next.phase.phase}'s{" "}
          {next.phase.hoursPerWeek} h a week, less contests and upsolves. Pace
          only informs. It opens and holds back nothing.
        </Typography>
        <Typography variant="body2">
          Last 28 days: {formatMinutes(pace.window)}, so{" "}
          {formatMinutes(pace.perWeek)} a week ({pace.window} min ÷ 28 days ×
          7).
        </Typography>
        <Stack component="ul" spacing={0.5} sx={{ pl: 2, m: 0 }}>
          {pace.projections.map((projection) => (
            <Typography
              component="li"
              variant="body2"
              key={projection.phase.phase}
            >
              Phase {projection.phase.phase} written work:{" "}
              {formatMinutes(projection.remaining)} left.{" "}
              {projection.done
                ? `At ${formatMinutes(pace.perWeek)} a week, done about ${longDate(projection.done)}. Target ${longDate(projection.phase.end)}, ${projection.onTarget ? "on time" : "late"}.`
                : `No pace yet, so no date. Target ${longDate(projection.phase.end)}.`}
            </Typography>
          ))}
        </Stack>
        {pace.projections.length > 1 && (
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Each phase's date counts the work of the phases before it.
          </Typography>
        )}
      </Stack>
    </Paper>
  );
}

function ScheduledPanel({
  contests,
  firstContest,
}: {
  contests: Contest[];
  firstContest: ReactNode;
}) {
  return (
    <Paper
      variant="outlined"
      component="section"
      aria-label="Scheduled"
      sx={{ p: 2 }}
    >
      <Stack spacing={1}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          Scheduled
        </Typography>
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          Contests run at set times. They are optional here: none holds back a
          unit, and none counts toward one. After one, upsolve the first problem
          you did not solve.
        </Typography>
        {firstContest}
        {contests.length === 0 ? (
          <Typography variant="body2">
            No round listed right now. See{" "}
            <Link href="https://codeforces.com/contests" target="_blank">
              Codeforces
            </Link>{" "}
            and{" "}
            <Link href="https://atcoder.jp/contests/" target="_blank">
              AtCoder
            </Link>
            .
          </Typography>
        ) : (
          <Stack component="ul" spacing={0.5} sx={{ pl: 2, m: 0 }}>
            {contests.map((contest) => (
              <Typography component="li" variant="body2" key={contest.url}>
                <Link href={contest.url} target="_blank" rel="noopener">
                  {contest.name}
                </Link>
                , {longDate(localDateOf(contest.start))} {clock(contest.start)},{" "}
                {formatMinutes(contest.durationMinutes)}
              </Typography>
            ))}
          </Stack>
        )}
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          Times are in this browser's zone, from the Codeforces API and
          AtCoder's contest list.
        </Typography>
      </Stack>
    </Paper>
  );
}

/**
 * The owner's first screen: the next session of self-paced work, a way to
 * keep going, the pace against the plan, and the scheduled contests.
 */
export default function TodayView({
  next,
  pace,
  contests,
  rating,
  firstContest,
  session,
  setSession,
  keepGoing,
  pool,
}: {
  next: NextUp;
  pace: Pace;
  contests: Contest[];
  /** The Codeforces line, with its source and time. */
  rating: ReactNode;
  firstContest: ReactNode;
  session: number | undefined;
  setSession: (minutes: number | null) => void;
  keepGoing: () => void;
  /** This week's problems from Mostafa Saad's pool. */
  pool?: ReactNode;
}) {
  const fallback = next.session;
  return (
    <Stack spacing={2.5} component="section" aria-label="Next up">
      <Box>
        <Typography variant="h2" sx={{ fontSize: "1.5rem" }}>
          Next up
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {longDate(next.date)}. Phase {next.phase.phase}, week {next.week} of{" "}
          {next.weeks} by its target dates. Work ahead whenever you have time.
          Nothing waits for a date.
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {rating}
        </Typography>
      </Box>
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={2}
        sx={{ alignItems: { sm: "center" } }}
      >
        <SessionLength value={session} fallback={fallback} save={setSession} />
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          The plan's default is its self-paced hours a week divided by its
          sessions.
        </Typography>
      </Stack>
      {next.lastWritten.map((track) => (
        <Alert key={track} severity="info">
          {trackLabel[track]}: this is the last unit written so far. The next
          lessons are written before you reach them.
        </Alert>
      ))}
      {next.retests.map((retest) => (
        <Alert key={retest.unit.id} severity="info">
          {retest.unit.title}: the mastery check retest opens{" "}
          {longDate(retest.opens)}. Review what you missed until then.
        </Alert>
      ))}
      {next.newWaits && (
        <Alert severity="warning">
          New material waits. {formatMinutes(next.overdue)} of review is
          overdue, more than one session of {formatMinutes(next.session)}. Clear
          the overdue reviews first.
        </Alert>
      )}
      {next.entries.length === 0 ? (
        <Typography variant="body1">
          Nothing open. Every written unit is done or placed out.
        </Typography>
      ) : (
        <Stack component="ol" spacing={1} sx={{ p: 0, m: 0 }}>
          {next.entries.map((entry, index) => (
            <EntryRow key={entryKey(entry)} entry={entry} index={index + 1} />
          ))}
        </Stack>
      )}
      <Stack
        direction="row"
        spacing={2}
        sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 1 }}
      >
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          {formatMinutes(next.planned)} listed. {next.later} more after these.
        </Typography>
        {next.later > 0 && (
          <Button size="small" variant="outlined" onClick={keepGoing}>
            Keep going
          </Button>
        )}
      </Stack>
      {pool}
      <PacePanel pace={pace} next={next} />
      <ScheduledPanel contests={contests} firstContest={firstContest} />
    </Stack>
  );
}
