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
import {
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
import Workbench from "./Workbench";

/**
 * The athlete's console: highlights and trends, the prediction calculator, the
 * data pipes, and the sources behind every number. Health data, so the whole
 * surface sits behind the owner sign-in.
 */
export default function FitnessPanel() {
  const [me, setMe] = React.useState<FitnessMe | null>(null);
  const [summary, setSummary] = React.useState<Summary | null>(null);
  const [failed, setFailed] = React.useState(false);
  const [tab, setTab] = React.useState(0);

  const reloadSummary = React.useCallback(() => {
    fetchSummary()
      .then(setSummary)
      .catch(() => setFailed(true));
  }, []);

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
      <Tabs
        value={tab}
        onChange={(_, next) => setTab(next)}
        aria-label="Fitness sections"
      >
        <Tab label="Dashboard" />
        <Tab label="Solve" />
        <Tab label="Plan" />
        <Tab label="Data" />
        <Tab label="Sources" />
      </Tabs>

      {tab === 0 && <Dashboard summary={summary} />}
      {tab === 1 && <Workbench summary={summary} />}
      {tab === 2 && (
        <ProjectionPanel summary={summary} onGoalsChanged={reloadSummary} />
      )}
      {tab === 3 && (
        <DataPanel
          hevyApi={me.hevyApi}
          settings={summary.settings}
          onProfileSaved={reloadSummary}
        />
      )}
      {tab === 4 && <SourcesPanel />}
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
