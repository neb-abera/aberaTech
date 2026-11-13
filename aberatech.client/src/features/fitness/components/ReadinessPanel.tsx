import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import type {
  AftResult,
  BodyReport,
  CalisthenicsReport,
  Readiness,
  RuckReport,
} from "../core/api";
import {
  formatDistance,
  formatLoad,
  formatSeconds,
  kgToLb,
  metricLabel,
} from "../core/format";
import MathPanel from "./MathPanel";

/**
 * The readouts behind the goals: rucking, calisthenics, body composition and
 * the fitness tests, as the log has them.
 */
export default function ReadinessPanel({
  readiness,
}: {
  readiness: Readiness;
}) {
  return (
    <Stack spacing={3}>
      <RuckCard ruck={readiness.ruck} />
      <CalisthenicsCard calisthenics={readiness.calisthenics} />
      <BodyCard body={readiness.body} />
      <AftCard results={readiness.aftResults} />
    </Stack>
  );
}

function RuckCard({ ruck }: { ruck: RuckReport }) {
  const referenceLb = Math.round(kgToLb(ruck.referenceLoadKg));
  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">Rucking</Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          Road-march speed is the strongest physical predictor of selection
          there is. Twelve miles at {referenceLb} lb (
          {ruck.referenceLoadKg.toFixed(1)} kg), from your run engine through
          the load-carriage model:{" "}
          <strong>
            {ruck.predictedTwelveMileAt45Seconds === null
              ? "needs a weigh-in"
              : formatSeconds(ruck.predictedTwelveMileAt45Seconds)}
          </strong>
          {ruck.predictedTwelveMileAt35Seconds !== null && (
            <>
              {" "}
              (at 35 lb, {formatSeconds(ruck.predictedTwelveMileAt35Seconds)})
            </>
          )}
          . A timed ruck of your own outranks this; record the load on each ruck
          below and the goals use the clock instead.
        </Typography>

        {ruck.rucksWithoutLoad > 0 && (
          <Alert severity="warning" sx={{ mb: 1 }}>
            {ruck.rucksWithoutLoad} ruck
            {ruck.rucksWithoutLoad === 1 ? " has" : "s have"} no load recorded,
            so the models read {ruck.rucksWithoutLoad === 1 ? "it" : "them"} as
            walks. Name the weight on the activity below.
          </Alert>
        )}

        {ruck.trend.length > 0 && (
          <Typography variant="body2" sx={{ mb: 1 }}>
            Monthly median pace at the reference heart rate and {referenceLb}{" "}
            lb:{" "}
            {ruck.trend
              .map(
                (p) =>
                  `${p.month} ${formatSeconds(p.medianSecPerKm)}/km (${p.rucks})`,
              )
              .join(" · ")}
            . Falling is fitness.
          </Typography>
        )}

        {ruck.marches.length > 0 && (
          <Box sx={{ overflowX: "auto" }}>
            <Table size="small" sx={{ minWidth: 520 }}>
              <TableHead>
                <TableRow>
                  <TableCell>Date</TableCell>
                  <TableCell align="right">Distance</TableCell>
                  <TableCell align="right">Time</TableCell>
                  <TableCell align="right">Load</TableCell>
                  <TableCell align="right">Avg HR</TableCell>
                  <TableCell align="right">Implies VDOT</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {ruck.marches.map((m) => (
                  <TableRow key={`${m.date}-${m.seconds}`}>
                    <TableCell>{m.date}</TableCell>
                    <TableCell align="right">
                      {formatDistance(m.distanceMeters)}
                    </TableCell>
                    <TableCell align="right">
                      {formatSeconds(m.seconds)}
                    </TableCell>
                    <TableCell align="right">{formatLoad(m.loadKg)}</TableCell>
                    <TableCell align="right">{m.averageHr ?? "n/a"}</TableCell>
                    <TableCell align="right">
                      {m.impliedVdot === null
                        ? "n/a"
                        : m.impliedVdot.toFixed(1)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        )}

        <MathPanel
          steps={ruck.steps}
          title="How the twelve-mile is estimated"
        />
      </CardContent>
    </Card>
  );
}

function CalisthenicsCard({
  calisthenics,
}: {
  calisthenics: CalisthenicsReport;
}) {
  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">Calisthenics</Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          Best recent set of each movement the goals count, read out of the
          strength log by name. Log pull-ups, push-ups, sit-ups and the plank as
          their own exercises and they land here.
        </Typography>
        {calisthenics.latest.length === 0 ? (
          <Typography variant="body2">
            Nothing logged in the last four months.
          </Typography>
        ) : (
          <Stack spacing={0.5}>
            {calisthenics.latest.map((set) => (
              <Typography key={set.metric} variant="body2">
                {metricLabel(set.metric)}:{" "}
                <strong>
                  {set.metric === "plank"
                    ? formatSeconds(set.value)
                    : `${Math.round(set.value)} reps`}
                </strong>{" "}
                <Typography
                  component="span"
                  variant="caption"
                  sx={{ color: "text.secondary" }}
                >
                  ({set.date})
                </Typography>
              </Typography>
            ))}
          </Stack>
        )}
      </CardContent>
    </Card>
  );
}

function BodyCard({ body }: { body: BodyReport }) {
  const withFat = body.points.filter((p) => p.bodyFatPercent !== null);
  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">Body composition</Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          What selection made of body composition in the one cohort it was
          measured in: the leanest quarter was selected at 52%, the fattest at
          14%; the most muscular quarter at 59%, the least at 20%. These are
          cohort rates, not your odds. They say nothing about how you run or
          ruck.
        </Typography>
        {body.latestBodyFatPercent === null ? (
          <Typography variant="body2">
            Record body fat with a weigh-in below to place yourself.
          </Typography>
        ) : (
          <Stack spacing={0.5}>
            <Typography variant="body2">
              Body fat{" "}
              <strong>{Number(body.latestBodyFatPercent.toFixed(1))}%</strong>
              {body.cohortRateByBodyFat !== null &&
                `, cohort selection rate ${Math.round(body.cohortRateByBodyFat * 100)}%`}
            </Typography>
            {body.latestLeanMassKg !== null && (
              <Typography variant="body2">
                Lean mass{" "}
                <strong>
                  {Math.round(kgToLb(body.latestLeanMassKg))} lb (
                  {body.latestLeanMassKg.toFixed(1)} kg)
                </strong>
                {body.cohortRateByLeanMass !== null &&
                  `, cohort selection rate ${Math.round(body.cohortRateByLeanMass * 100)}%`}
              </Typography>
            )}
            {withFat.length > 1 && (
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {withFat
                  .slice(-6)
                  .map(
                    (p) =>
                      `${p.date}: ${Number((p.bodyFatPercent ?? 0).toFixed(1))}%, ${p.leanMassKg === null ? "" : `${p.leanMassKg.toFixed(1)} kg lean`}`,
                  )
                  .join(" · ")}
              </Typography>
            )}
          </Stack>
        )}
      </CardContent>
    </Card>
  );
}

function AftCard({ results }: { results: AftResult[] }) {
  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">Army Fitness Test</Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          Scored on the published tables as of today. The combat standard is 60
          in every event and 350 in total; record a test below.
        </Typography>
        {results.length === 0 ? (
          <Typography variant="body2">No test recorded.</Typography>
        ) : (
          <Stack spacing={2}>
            {results.map((result) => (
              <Box key={result.id}>
                <Stack
                  direction="row"
                  spacing={1}
                  sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 1 }}
                >
                  <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                    {result.date}: {result.total} points
                  </Typography>
                  <Chip
                    size="small"
                    label={
                      result.meetsCombatStandard
                        ? "combat standard met"
                        : `combat standard not met (lowest event ${result.lowestEvent})`
                    }
                    color={result.meetsCombatStandard ? "success" : "warning"}
                  />
                  <Chip
                    size="small"
                    variant="outlined"
                    label={`${result.ageBand} band${result.ageAssumed ? ", birth year unset" : ""}`}
                  />
                </Stack>
                <Typography variant="body2" sx={{ color: "text.secondary" }}>
                  {result.events
                    .map((e) => `${e.name} ${e.points}`)
                    .join(" · ")}
                </Typography>
                <MathPanel steps={result.steps} title="Show the scoring" />
              </Box>
            ))}
          </Stack>
        )}
      </CardContent>
    </Card>
  );
}
