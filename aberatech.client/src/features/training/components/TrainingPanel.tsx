import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import LinearProgress from "@mui/material/LinearProgress";
import Link from "@mui/material/Link";
import Stack from "@mui/material/Stack";
import Tab from "@mui/material/Tab";
import Tabs from "@mui/material/Tabs";
import Typography from "@mui/material/Typography";
import { useMemo, useState } from "react";
import { Link as RouterLink, useLocation, useSearchParams } from "react-router";
import SignInToSee from "../../progress/components/SignInToSee";
import type { CheckKind } from "../core/assessment";
import { clock, localDate } from "../core/dates";
import { applyFixture } from "../core/fixture";
import { pace as measurePace } from "../core/pace";
import { itemStatus, overall, type Solves, unitStates } from "../core/progress";
import { asDue, dueOn, upcoming } from "../core/reviews";
import { formatMinutes, locate, syllabus } from "../core/syllabus";
import { nextUp } from "../core/today";
import { type JudgeLine, useSolves } from "../hooks/useSolves";
import { useTraining } from "../hooks/useTraining";
import AssessmentView from "./AssessmentView";
import LessonView from "./LessonView";
import MapView from "./MapView";
import NoteField from "./NoteField";
import NotesView from "./NotesView";
import ReviewView from "./ReviewView";
import TodayView, { type Contest } from "./TodayView";
import ToolsView from "./ToolsView";
import UnitView from "./UnitView";

type View = "today" | "review" | "map" | "notes" | "tools";
const views: View[] = ["today", "review", "map", "notes", "tools"];
/** Views only the owner has: his work, his reviews, his notes. */
const ownerViews: View[] = ["today", "review", "notes"];

/** The rating line, with its source and the time it was read. */
function judgeLine(
  name: string,
  status: string,
  line: JudgeLine | null,
): string {
  if (status === "loading" || status === "idle") return `Reading ${name}.`;
  if (!line) return `No ${name} handle set. Set one under Tools.`;
  if (!line.ok) return `${name} did not answer. Manual ticks for now.`;
  const rating =
    line.rating == null ? "unrated" : `rated ${line.rating}, ${line.rank}`;
  return `${name} ${line.handle}: ${rating}, ${line.ratedContests} rated contests, ${Object.keys(line.solved).length} problems solved (${name} API, read ${clock(line.readAt)}).`;
}

/**
 * /training: the syllabus as a course player. The address carries the
 * view (`?view=`), a unit (`?unit=`) or a lesson (`?lesson=`), so a reload
 * or a shared link lands on the same place.
 *
 * The first render is the same for everyone and for the build-time render:
 * the map, read-only. The owner's progress loads in an effect.
 */
export default function TrainingPanel() {
  const training = useTraining();
  const { status, document, change } = training;
  const [params] = useSearchParams();
  const location = useLocation();
  const owner = status === "owner";
  const answer = useSolves(
    owner,
    document.settings.codeforces,
    document.settings.atcoder,
  );
  // Sample questions arrive under `make e2e` only (core/fixture.ts).
  applyFixture(answer.fixture);
  const solves: Solves = answer.solves;
  const now = new Date();
  const states = useMemo(
    () => unitStates(document, solves, new Date()),
    [document, solves],
  );

  const checkParam = params.get("check");
  const checkMatch = checkParam
    ? /^(quiz|pretest|mastery):([\w-]+)$/.exec(checkParam)
    : null;
  const lessonId = params.get("lesson");
  const unitId = params.get("unit");
  const asked = params.get("view") as View | null;
  // The owner opens on Today. Everyone else, and the build-time render, on the map.
  const view: View =
    asked && views.includes(asked) && (!ownerViews.includes(asked) || owner)
      ? asked
      : owner
        ? "today"
        : "map";
  const unitById = unitId
    ? syllabus.units.find((unit) => unit.id === unitId && !unit.locked)
    : undefined;
  const place = lessonId
    ? locate(lessonId)
    : unitById
      ? { unit: unitById }
      : null;
  const unitIndex = place
    ? syllabus.units.findIndex((unit) => unit.id === place.unit.id) + 1
    : 0;

  const total = overall(document, solves, states);
  const contests: Contest[] = answer.contests;
  const rating = judgeLine("Codeforces", answer.status, answer.codeforces);
  const [more, setMore] = useState(0);
  const date = localDate(now);
  const due = owner ? dueOn(date, document, solves) : [];
  const next = nextUp(date, document, solves, now, {
    session: document.settings.session,
    more,
    reviews: due.map(asDue),
  });
  const firstRated = syllabus.units
    .find((unit) => unit.slot === "contest")
    ?.lessons.flatMap((lesson) => lesson.items)
    .find((item) => item.record === "codeforces-rated");
  const firstContest = firstRated ? (
    <Typography variant="body2">
      First rated contest, a Phase 0 target:{" "}
      {itemStatus(firstRated, document, solves).done
        ? "entered."
        : "not entered yet. "}
      {!itemStatus(firstRated, document, solves).done && (
        <Link component={RouterLink} to="/training?unit=first-contest">
          What to do
        </Link>
      )}
    </Typography>
  ) : null;

  return (
    <Stack spacing={3}>
      {status === "visitor" && (
        <SignInToSee
          message="The syllabus is public. My progress and notes are mine, so they need my sign-in."
          returnUrl={encodeURIComponent(location.pathname + location.search)}
        />
      )}
      {status === "error" && (
        <Alert severity="error">
          The progress could not be loaded. The syllabus below is read-only.
        </Alert>
      )}
      {training.failed && (
        <Alert severity="warning">
          The last change was not saved. It is kept here and sent with the next
          one.
        </Alert>
      )}
      {owner &&
        answer.status === "ready" &&
        (
          [
            ["Codeforces", answer.codeforces],
            ["AtCoder Problems", answer.atcoder],
          ] as const
        ).map(([name, line]) =>
          line && !line.ok ? (
            <Alert key={name} severity="warning">
              {name} did not answer for {line.handle}: {line.error}. Its
              problems take manual ticks until it does.
            </Alert>
          ) : null,
        )}
      {owner && answer.status === "error" && (
        <Alert severity="warning">
          The judges could not be read. Their problems take manual ticks until
          they can.
        </Alert>
      )}
      {owner && (
        <Box>
          <Typography variant="body2">
            {total.done} of {total.total} items,{" "}
            {formatMinutes(total.minutesDone)} of{" "}
            {formatMinutes(total.minutesTotal)} written so far. Placed-out units
            count whole.
            {training.saving ? " Saving." : ""}
          </Typography>
          <LinearProgress
            variant="determinate"
            value={total.total === 0 ? 0 : (total.done / total.total) * 100}
            aria-label="Overall progress"
          />
        </Box>
      )}
      <Tabs value={place ? false : view} aria-label="Training views">
        {owner && (
          <Tab
            value="today"
            label="Next up"
            component={RouterLink}
            to="/training?view=today"
          />
        )}
        {owner && (
          <Tab
            value="review"
            label={due.length ? `Review (${due.length})` : "Review"}
            component={RouterLink}
            to="/training?view=review"
          />
        )}
        <Tab
          value="map"
          label="Map"
          component={RouterLink}
          to="/training?view=map"
        />
        {owner && (
          <Tab
            value="notes"
            label="Notes"
            component={RouterLink}
            to="/training?view=notes"
          />
        )}
        <Tab
          value="tools"
          label="Tools"
          component={RouterLink}
          to="/training?view=tools"
        />
      </Tabs>
      {checkMatch && owner ? (
        <AssessmentView
          key={checkParam}
          kind={checkMatch[1] as CheckKind}
          id={checkMatch[2]}
          document={document}
          change={change}
          now={now}
        />
      ) : place?.lesson && lessonId ? (
        <LessonView
          unit={place.unit}
          lesson={place.lesson}
          owner={owner}
          document={document}
          solves={solves}
          change={change}
          noteFor={(id, label) => (
            <NoteField
              id={id}
              label={label}
              document={document}
              change={change}
            />
          )}
        />
      ) : place && unitId ? (
        <UnitView
          unit={place.unit}
          index={unitIndex}
          total={syllabus.units.length}
          owner={owner}
          document={document}
          solves={solves}
          state={states.get(place.unit.id)}
          change={change}
          now={now}
        />
      ) : view === "today" && owner ? (
        <TodayView
          next={next}
          pace={measurePace(date, next.phase, document, solves, states)}
          contests={contests}
          rating={rating}
          firstContest={firstContest}
          session={document.settings.session}
          setSession={(minutes) =>
            change((current) => {
              const settings = { ...current.settings };
              if (minutes === null) delete settings.session;
              else settings.session = minutes;
              return { ...current, settings };
            })
          }
          keepGoing={() => setMore((count) => count + 1)}
        />
      ) : view === "review" && owner ? (
        <ReviewView
          due={due}
          document={document}
          change={change}
          now={now}
          tomorrow={upcoming(date, document, solves, 1)}
          week={upcoming(date, document, solves, 7)}
        />
      ) : view === "notes" && owner ? (
        <NotesView document={document} />
      ) : view === "tools" ? (
        <ToolsView
          settings={owner ? document.settings : undefined}
          change={change}
        />
      ) : (
        <MapView
          owner={owner}
          document={document}
          solves={solves}
          states={states}
        />
      )}
    </Stack>
  );
}
