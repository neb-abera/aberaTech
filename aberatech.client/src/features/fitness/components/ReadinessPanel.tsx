import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import Link from "@mui/material/Link";
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
  Gate,
  GateStatus,
  Readiness,
  RuckReport,
} from "../core/api";
import {
  formatDistance,
  formatLoad,
  formatRequirement,
  formatSeconds,
  kgToLb,
  metricLabel,
} from "../core/format";
import MathPanel from "./MathPanel";

/**
 * The selection gates, scored from the log: every published standard between
 * the athlete and a slot, with a date, and the rucking, calisthenics, body
 * composition and fitness-test readouts the gates are scored from.
 */
export default function ReadinessPanel({
  readiness,
}: {
  readiness: Readiness;
}) {
  return (
    <Stack spacing={3}>
      {readiness.selectionDate === null && (
        <Alert severity="info">
          No selection date set. The gates below are scored but undated; name
          the SFRE date in the athlete profile on the Data tab and each gate
          gets its due date.
        </Alert>
      )}

      {readiness.gates.map((gate) => (
        <GateCard key={gate.id} gate={gate} />
      ))}

      <RuckCard ruck={readiness.ruck} />
      <CalisthenicsCard calisthenics={readiness.calisthenics} />
      <BodyCard body={readiness.body} />
      <AftCard results={readiness.aftResults} />
    </Stack>
  );
}

function statusColor(status: GateStatus): "success" | "error" | "default" {
  if (status === "Pass") return "success";
  if (status === "Fail") return "error";
  return "default";
}

function statusLabel(status: GateStatus): string {
  if (status === "Pass") return "pass";
  if (status === "Fail") return "short";
  return "unknown";
}

function GateCard({ gate }: { gate: Gate }) {
  return (
    <Card variant="outlined">
      <CardContent>
        <Stack
          direction="row"
          spacing={1}
          sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 1, mb: 0.5 }}
        >
          <Typography variant="h6">{gate.name}</Typography>
          <Chip
            size="small"
            label={statusLabel(gate.status)}
            color={statusColor(gate.status)}
          />
          <Chip
            size="small"
            variant="outlined"
            label={`${gate.passed} of ${gate.requirements.length} clear`}
          />
          {gate.dueOn !== null && (
            <Chip size="small" variant="outlined" label={`due ${gate.dueOn}`} />
          )}
        </Stack>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          {gate.purpose}
          {gate.dueOn === null &&
            gate.weeksBeforeSelection > 0 &&
            ` Cleared ${gate.weeksBeforeSelection} weeks before selection.`}
        </Typography>

        <Box sx={{ overflowX: "auto" }}>
          <Table size="small" sx={{ minWidth: 640 }}>
            <TableHead>
              <TableRow>
                <TableCell>Requirement</TableCell>
                <TableCell align="right">Standard</TableCell>
                <TableCell align="right">You</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Basis</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {gate.requirements.map((r) => (
                <TableRow key={r.metric}>
                  <TableCell>
                    {r.label}{" "}
                    <Link
                      href={`#source-${r.citationId}`}
                      underline="hover"
                      variant="caption"
                    >
                      [{r.citationId}]
                    </Link>
                  </TableCell>
                  <TableCell align="right">
                    {r.comparison === "AtLeast" ? "≥ " : "≤ "}
                    {formatRequirement(r.unit, r.target)}
                  </TableCell>
                  <TableCell align="right">
                    {r.current === null
                      ? "—"
                      : formatRequirement(r.unit, r.current.value)}
                  </TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      label={statusLabel(r.status)}
                      color={statusColor(r.status)}
                    />{" "}
                    <Typography
                      component="span"
                      variant="caption"
                      sx={{ color: "text.secondary" }}
                    >
                      {r.gap}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    {r.current === null ? (
                      <Typography
                        variant="caption"
                        sx={{ color: "text.secondary" }}
                      >
                        not in the log
                      </Typography>
                    ) : (
                      <Typography
                        variant="caption"
                        sx={{ color: "text.secondary" }}
                      >
                        {r.current.basis === "Measured"
                          ? "measured"
                          : "modelled"}
                        {" — "}
                        {r.current.evidence}
                      </Typography>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>

        {gate.untracked.length > 0 && (
          <Typography
            variant="caption"
            sx={{ color: "text.secondary", display: "block", mt: 1 }}
          >
            Not scored from the log: {gate.untracked.join(", ")}.
          </Typography>
        )}
      </CardContent>
    </Card>
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
              — at 35 lb, {formatSeconds(ruck.predictedTwelveMileAt35Seconds)}
            </>
          )}
          . A timed ruck of your own outranks this; record the load on each ruck
          on the Data tab and the gates use the clock instead.
        </Typography>

        {ruck.rucksWithoutLoad > 0 && (
          <Alert severity="warning" sx={{ mb: 1 }}>
            {ruck.rucksWithoutLoad} ruck
            {ruck.rucksWithoutLoad === 1 ? " has" : "s have"} no load recorded,
            so the models read {ruck.rucksWithoutLoad === 1 ? "it" : "them"} as
            walks. Name the weight on the Data tab.
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
                    <TableCell align="right">{m.averageHr ?? "—"}</TableCell>
                    <TableCell align="right">
                      {m.impliedVdot === null ? "—" : m.impliedVdot.toFixed(1)}
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
          Best recent set of each movement the gates count, read out of the
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
          14%; the most muscular quarter at 59%, the least at 20%. Cohort rates,
          not your odds — they say nothing about how you run or ruck.
        </Typography>
        {body.latestBodyFatPercent === null ? (
          <Typography variant="body2">
            Record body fat with a weigh-in on the Data tab to place yourself.
          </Typography>
        ) : (
          <Stack spacing={0.5}>
            <Typography variant="body2">
              Body fat{" "}
              <strong>{Number(body.latestBodyFatPercent.toFixed(1))}%</strong>
              {body.cohortRateByBodyFat !== null &&
                ` — cohort selection rate ${Math.round(body.cohortRateByBodyFat * 100)}%`}
            </Typography>
            {body.latestLeanMassKg !== null && (
              <Typography variant="body2">
                Lean mass{" "}
                <strong>
                  {Math.round(kgToLb(body.latestLeanMassKg))} lb (
                  {body.latestLeanMassKg.toFixed(1)} kg)
                </strong>
                {body.cohortRateByLeanMass !== null &&
                  ` — cohort selection rate ${Math.round(body.cohortRateByLeanMass * 100)}%`}
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
          in every event and 350 in total; record a test on the Data tab.
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
