import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import LinearProgress from "@mui/material/LinearProgress";
import Stack from "@mui/material/Stack";
import Tab from "@mui/material/Tab";
import Tabs from "@mui/material/Tabs";
import Typography from "@mui/material/Typography";
import { useMemo } from "react";
import { Link as RouterLink, useSearchParams } from "react-router";
import SignInToSee from "../../progress/components/SignInToSee";
import { localDate } from "../core/dates";
import { noSolves, overall, type Solves, unitStates } from "../core/progress";
import { formatMinutes, locate, syllabus } from "../core/syllabus";
import { today as planToday } from "../core/today";
import { useTraining } from "../hooks/useTraining";
import LessonView from "./LessonView";
import MapView from "./MapView";
import TodayView, { type Contest } from "./TodayView";
import ToolsView from "./ToolsView";
import UnitView from "./UnitView";

type View = "today" | "map" | "tools";
const views: View[] = ["today", "map", "tools"];

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
  const owner = status === "owner";
  const solves: Solves = noSolves;
  const now = new Date();
  const states = useMemo(
    () => unitStates(document, solves, new Date()),
    [document],
  );

  const lessonId = params.get("lesson");
  const unitId = params.get("unit");
  const asked = params.get("view") as View | null;
  // The owner opens on Today. Everyone else, and the build-time render, on the map.
  const view: View =
    asked && views.includes(asked) && (asked !== "today" || owner)
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
  const contests: Contest[] = [];
  const rating = "Codeforces rating is read live once verification lands.";

  return (
    <Stack spacing={3}>
      {status === "visitor" && (
        <SignInToSee
          message="The syllabus is public. My progress and notes are mine, so they need my sign-in."
          returnUrl="/training"
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
            label="Today"
            component={RouterLink}
            to="/training?view=today"
          />
        )}
        <Tab
          value="map"
          label="Map"
          component={RouterLink}
          to="/training?view=map"
        />
        <Tab
          value="tools"
          label="Tools"
          component={RouterLink}
          to="/training?view=tools"
        />
      </Tabs>
      {place?.lesson && lessonId ? (
        <LessonView
          unit={place.unit}
          lesson={place.lesson}
          owner={owner}
          document={document}
          solves={solves}
          change={change}
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
          today={planToday(localDate(now), document, solves, now)}
          contests={contests}
          rating={rating}
        />
      ) : view === "tools" ? (
        <ToolsView />
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
