import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Grid from "@mui/material/Grid";
import Stack from "@mui/material/Stack";
import Tab from "@mui/material/Tab";
import Tabs from "@mui/material/Tabs";
import Typography from "@mui/material/Typography";
import * as React from "react";
import { useSearchParams } from "react-router";
import {
  ApiError,
  type FitnessMe,
  fetchMe,
  fetchSummary,
  type Summary,
} from "../core/api";
import { formatPace, kgToLb } from "../core/format";
import { AerobicTrendChart, VolumeChart } from "./charts";
import DataPanel from "./DataPanel";
import ProjectionPanel from "./ProjectionPanel";
import SourcesPanel from "./SourcesPanel";

/** The tabs, in order. The slug is what goes in the URL. */
const TAB_SLUGS: readonly string[] = [
  "dashboard",
  "predictions",
  "data",
  "sources",
];

/**
 * One tab's contents, mounted the first time it is opened and kept mounted
 * afterwards.
 *
 * Switching tabs used to unmount the panel, which threw away anything typed
 * and not yet saved: fill in an anchor race, glance at Predictions to check it,
 * come back to an empty field. Mounting lazily rather than all at once keeps
 * the first paint as cheap as it was, and mounting while visible means the
 * charts measure a real width.
 */
function Section({
  index,
  tab,
  visited,
  children,
}: {
  index: number;
  tab: number;
  visited: ReadonlySet<number>;
  children: React.ReactNode;
}) {
  if (!visited.has(index)) return null;
  return (
    <Box sx={{ display: tab === index ? "block" : "none" }}>{children}</Box>
  );
}

/**
 * The athlete's console: highlights and trends, the prediction calculator, the
 * data pipes, and the sources behind every number. Health data, so the whole
 * surface sits behind the owner sign-in.
 */
export default function FitnessPanel() {
  const [me, setMe] = React.useState<FitnessMe | null>(null);
  const [summary, setSummary] = React.useState<Summary | null>(null);
  const [failed, setFailed] = React.useState(false);
  const [staleError, setStaleError] = React.useState<string | null>(null);
  const [visited, setVisited] = React.useState<ReadonlySet<number>>(
    () => new Set<number>(),
  );
  const [params, setParams] = useSearchParams();

  // The open tab lives in the URL, so a reload comes back to where you were,
  // Back steps between sections, and a tab can be linked to at all.
  const tab = Math.max(0, TAB_SLUGS.indexOf(params.get("tab") ?? TAB_SLUGS[0]));

  const showTab = (next: number) => {
    const updated = new URLSearchParams(params);
    updated.set("tab", TAB_SLUGS[next]);
    setParams(updated, { replace: false });
  };

  /**
   * Refetch the numbers behind the dashboard.
   *
   * A failure here is only fatal before there is anything to show. Once the
   * console is on screen a failed refresh says so and leaves the page standing:
   * this used to set one latch that every render checked first, so a single
   * blip — or a session quietly expiring — replaced the whole console, tabs and
   * all, with "the service did not answer".
   */
  const reloadSummary = React.useCallback(() => {
    fetchSummary()
      .then((next) => {
        setSummary(next);
        setStaleError(null);
      })
      .catch((error: unknown) => {
        if (error instanceof ApiError && error.needsSignIn) {
          // Not an outage. Say the true thing and offer the way back.
          setMe((current) =>
            current ? { ...current, signedIn: false } : current,
          );
          return;
        }

        setSummary((current) => {
          if (current === null) setFailed(true);
          else
            setStaleError(
              "Could not refresh — showing the last numbers loaded.",
            );
          return current;
        });
      });
  }, []);

  React.useEffect(() => {
    setVisited((seen) => (seen.has(tab) ? seen : new Set(seen).add(tab)));
  }, [tab]);

  React.useEffect(() => {
    fetchMe()
      .then((state) => {
        setMe(state);
        if (state.signedIn) {
          reloadSummary();
        }
      })
      .catch(() => setFailed(true));
  }, [reloadSummary]);

  if (failed) {
    return (
      <Alert severity="error">
        The fitness service did not answer. Try a reload.
      </Alert>
    );
  }

  if (me === null) {
    return <CircularProgress size={28} aria-label="Loading" />;
  }

  if (!me.configured) {
    return (
      <Alert severity="info">
        Fitness tracking is not set up on this deployment yet. It needs a
        database, Google credentials and an allowed address.
      </Alert>
    );
  }

  if (!me.signedIn) {
    return (
      <Stack spacing={2} sx={{ maxWidth: 420 }}>
        <Typography variant="body1" sx={{ color: "text.secondary" }}>
          Training data is personal. Sign in to see it.
        </Typography>
        <Box>
          <Button
            variant="contained"
            href="/api/scheduling/admin/sign-in?returnUrl=/fitness"
          >
            Sign in with Google
          </Button>
        </Box>
      </Stack>
    );
  }

  if (summary === null) {
    return <CircularProgress size={28} aria-label="Loading data" />;
  }

  return (
    <Stack spacing={3}>
      {staleError && (
        <Alert severity="warning" onClose={() => setStaleError(null)}>
          {staleError}
        </Alert>
      )}

      <Tabs
        value={tab}
        onChange={(_, next) => showTab(next)}
        aria-label="Fitness sections"
        variant="scrollable"
        allowScrollButtonsMobile
      >
        <Tab label="Dashboard" />
        <Tab label="Predictions" />
        <Tab label="Data" />
        <Tab label="Sources" />
      </Tabs>

      <Section index={0} tab={tab} visited={visited}>
        <Dashboard summary={summary} />
      </Section>
      <Section index={1} tab={tab} visited={visited}>
        <ProjectionPanel summary={summary} onGoalsChanged={reloadSummary} />
      </Section>
      <Section index={2} tab={tab} visited={visited}>
        <DataPanel
          hevyApi={me.hevyApi}
          settings={summary.settings}
          onDataChanged={reloadSummary}
        />
      </Section>
      <Section index={3} tab={tab} visited={visited}>
        <SourcesPanel />
      </Section>
    </Stack>
  );
}

function Dashboard({ summary }: { summary: Summary }) {
  if (summary.activityCount === 0) {
    return (
      <Alert severity="info">
        No activities yet. Head to the Data tab and upload a Garmin or Hevy
        export to light this page up.
      </Alert>
    );
  }

  return (
    <Stack spacing={3}>
      {summary.highlights.length > 0 && (
        <Grid container spacing={2}>
          {summary.highlights.map((highlight) => (
            <Grid
              key={highlight.kind + highlight.headline}
              size={{ xs: 12, sm: 6 }}
            >
              <Card variant="outlined" sx={{ height: "100%" }}>
                <CardContent>
                  <Chip
                    size="small"
                    label={highlight.positive ? "progress" : "attention"}
                    color={highlight.positive ? "success" : "warning"}
                    sx={{ mb: 1 }}
                  />
                  <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                    {highlight.headline}
                  </Typography>
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    {highlight.evidence}
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
          ))}
        </Grid>
      )}

      <Card variant="outlined">
        <CardContent>
          <Typography variant="h6">Aerobic base</Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
            Monthly median pace, HR-normalized to {summary.settings.referenceHr}{" "}
            bpm. Falling is fitness.
            {summary.deficiencySpread !== null && (
              <>
                {" "}
                Aerobic-to-lactate-threshold spread:{" "}
                <strong>{(summary.deficiencySpread * 100).toFixed(0)}%</strong>
                {summary.deficiencySpread > 0.1
                  ? " — over the 10% deficiency line; base volume fixes it."
                  : " — inside the 10% line."}
              </>
            )}
          </Typography>
          <AerobicTrendChart points={summary.aerobicTrend} />
        </CardContent>
      </Card>

      {summary.trainingPaces.length > 0 && (
        <Card variant="outlined">
          <CardContent>
            <Typography variant="h6">
              Today&apos;s training paces (Daniels)
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
              Prescriptions, not predictions: the pace each kind of session
              should happen at, from your current VDOT{" "}
              {summary.settings.startVdot.toFixed(0)}. Bands, not laps to hit to
              the second.
            </Typography>
            <Stack spacing={0.5}>
              {summary.trainingPaces.map((pace) => (
                <Typography key={pace.zone} variant="body2">
                  <strong>
                    {pace.zone} · {pace.name}:
                  </strong>{" "}
                  {formatPace(pace.slowSecPerKm)} –{" "}
                  {formatPace(pace.fastSecPerKm)}{" "}
                  <Typography
                    component="span"
                    variant="caption"
                    sx={{ color: "text.secondary" }}
                  >
                    {pace.purpose}
                  </Typography>
                </Typography>
              ))}
            </Stack>
          </CardContent>
        </Card>
      )}

      <Card variant="outlined">
        <CardContent>
          <Typography variant="h6">Training dose</Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
            Weekly endurance minutes against the plan&apos;s{" "}
            {Math.round(summary.settings.planMinutesPerWeek)}.
          </Typography>
          <VolumeChart
            weeks={summary.weeklyVolume}
            planMinutes={summary.settings.planMinutesPerWeek}
          />
        </CardContent>
      </Card>

      {summary.strengthTrend.length > 0 && (
        <Card variant="outlined">
          <CardContent>
            <Typography variant="h6" sx={{ mb: 1 }}>
              Strength — estimated 1RM (Epley)
            </Typography>
            <Stack spacing={0.5}>
              {latestPerExercise(summary).map((point) => (
                <Typography key={point.exercise} variant="body2">
                  {point.exercise}:{" "}
                  <strong>{Math.round(kgToLb(point.e1RmKg))} lb</strong>{" "}
                  <Typography
                    component="span"
                    variant="caption"
                    sx={{ color: "text.secondary" }}
                  >
                    (as of {point.date})
                  </Typography>
                </Typography>
              ))}
            </Stack>
          </CardContent>
        </Card>
      )}

      {summary.aerobicTrend.length > 0 && (
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          Latest month:{" "}
          {formatPace(
            summary.aerobicTrend[summary.aerobicTrend.length - 1]
              .medianSecPerKm,
          )}{" "}
          at {summary.settings.referenceHr} bpm across{" "}
          {summary.aerobicTrend[summary.aerobicTrend.length - 1].runs} runs.
        </Typography>
      )}
    </Stack>
  );
}

function latestPerExercise(summary: Summary) {
  const byExercise = new Map<
    string,
    { exercise: string; e1RmKg: number; date: string }
  >();
  for (const point of summary.strengthTrend) {
    const existing = byExercise.get(point.exercise);
    if (!existing || existing.date < point.date) {
      byExercise.set(point.exercise, point);
    }
  }
  return [...byExercise.values()].sort((a, b) => b.e1RmKg - a.e1RmKg);
}
