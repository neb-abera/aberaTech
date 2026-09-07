import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import MenuItem from "@mui/material/MenuItem";
import Slider from "@mui/material/Slider";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import {
  fetchOutlook,
  type Outlook,
  type OutlookGate,
  type OutlookLine,
} from "../core/api";
import { formatRequirement } from "../core/format";

/** A probability as the word a coach would use, and the colour to match. */
function verdict(p: number | null): {
  label: string;
  color: "success" | "warning" | "error" | "default";
} {
  if (p === null) return { label: "not forecast", color: "default" };
  if (p >= 0.8) return { label: `${Math.round(p * 100)}%`, color: "success" };
  if (p >= 0.5) return { label: `${Math.round(p * 100)}%`, color: "warning" };
  return { label: `${Math.round(p * 100)}%`, color: "error" };
}

function months(m: number | null): string {
  if (m === null) return "—";
  if (m < 0.5) return "now";
  return `${m.toFixed(1)} mo`;
}

const methodLabel: Record<OutlookLine["method"], string> = {
  trajectory: "trajectory",
  trend: "trend",
  held: "held",
  none: "no data",
};

/**
 * The gates as a forecast rather than a scoreboard: for every line, the
 * chance of clearing it by the date it is due, under the week the slider
 * names. This is the panel a decision gets made on — whether the selection
 * date holds, and what the week would have to be for it to.
 */
export default function DecisionPanel({
  selectionDate,
}: {
  selectionDate: string | null;
}) {
  const [outlook, setOutlook] = React.useState<Outlook | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [hours, setHours] = React.useState<number | null>(null);
  const [compliance, setCompliance] = React.useState(1);
  const [loading, setLoading] = React.useState(true);

  // The first fetch lets the server pick the week the log shows; every later
  // one is the slider's, debounced so a drag is one request, not thirty.
  React.useEffect(() => {
    let cancelled = false;
    setLoading(true);
    const handle = window.setTimeout(
      () => {
        fetchOutlook(hours, compliance)
          .then((result) => {
            if (cancelled) return;
            setOutlook(result);
            setError(null);
            if (hours === null) setHours(result.weeklyHours);
          })
          .catch((e: Error) => {
            if (!cancelled) setError(e.message);
          })
          .finally(() => {
            if (!cancelled) setLoading(false);
          });
      },
      hours === null ? 0 : 300,
    );
    return () => {
      cancelled = true;
      window.clearTimeout(handle);
    };
  }, [hours, compliance]);

  if (error !== null && outlook === null) {
    return (
      <Alert severity="warning">The forecast did not answer: {error}</Alert>
    );
  }

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack
          direction="row"
          spacing={1}
          sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 1 }}
        >
          <Typography variant="h6">
            Decision: will the gates be clear in time?
          </Typography>
          {loading && <CircularProgress size={16} />}
        </Stack>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          Each line&apos;s chance of being clear by its due date, under the week
          below. Running lines come from the fitted trajectory with its error
          bars; everything else from a straight line through its own dated
          readings. A gate&apos;s chance is the product of its lines&apos;.
        </Typography>

        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={3}
          sx={{ alignItems: { sm: "center" }, mb: 2 }}
        >
          <Box sx={{ flex: 1, minWidth: 220 }}>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              Running hours a week: {hours === null ? "—" : hours.toFixed(1)} h
              {outlook !== null &&
                ` (log shows ${outlook.measuredWeeklyHours.toFixed(1)} h)`}
            </Typography>
            <Slider
              aria-label="Running hours a week"
              value={hours ?? 0}
              min={0}
              max={15}
              step={0.5}
              disabled={hours === null}
              onChange={(_, value) => setHours(Number(value))}
              valueLabelDisplay="auto"
            />
          </Box>
          <TextField
            select
            size="small"
            label="Weeks kept"
            value={compliance}
            onChange={(e) => setCompliance(Number(e.target.value))}
            sx={{ minWidth: 140 }}
          >
            <MenuItem value={1}>100%</MenuItem>
            <MenuItem value={0.9}>90%</MenuItem>
            <MenuItem value={0.75}>75%</MenuItem>
          </TextField>
        </Stack>

        {outlook !== null && (
          <>
            {outlook.earliestSelectionDate !== null ? (
              <Alert severity="info" sx={{ mb: 2 }}>
                Every gate that can be forecast reaches an 80% chance in time
                for a selection on{" "}
                <strong>{outlook.earliestSelectionDate}</strong> (set by{" "}
                {outlook.gates.find((g) => g.id === outlook.bindingGate)
                  ?.name ?? outlook.bindingGate}
                ) at this week.
                {selectionDate !== null &&
                  selectionDate < outlook.earliestSelectionDate &&
                  ` Your ${selectionDate} date is earlier than that.`}
                {selectionDate !== null &&
                  selectionDate >= outlook.earliestSelectionDate &&
                  ` Your ${selectionDate} date holds.`}
              </Alert>
            ) : (
              <Alert severity="warning" sx={{ mb: 2 }}>
                At this week some forecast line never reaches an 80% chance
                inside the horizon, so no selection date can be named from it.
              </Alert>
            )}

            <Stack spacing={2}>
              {outlook.gates.map((gate) => (
                <GateOutlookCard key={gate.id} gate={gate} />
              ))}
            </Stack>

            <Typography
              variant="caption"
              component="ul"
              sx={{ color: "text.secondary", mt: 2, pl: 2 }}
            >
              {outlook.assumptions.map((a) => (
                <li key={a}>{a}</li>
              ))}
            </Typography>
          </>
        )}
      </CardContent>
    </Card>
  );
}

function GateOutlookCard({ gate }: { gate: OutlookGate }) {
  const v = verdict(gate.probability);
  return (
    <Box>
      <Stack
        direction="row"
        spacing={1}
        sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 1, mb: 0.5 }}
      >
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          {gate.name}
        </Typography>
        <Chip size="small" label={v.label} color={v.color} />
        <Chip
          size="small"
          variant="outlined"
          label={`${gate.forecast} of ${gate.total} lines forecast`}
        />
        <Chip
          size="small"
          variant="outlined"
          label={
            gate.dueOn === null
              ? `${gate.monthsAway.toFixed(0)} months out (no date set)`
              : `due ${gate.dueOn}`
          }
        />
        <Chip
          size="small"
          variant="outlined"
          label={`ready in ${months(gate.readyInMonths)}`}
        />
      </Stack>
      <Box sx={{ overflowX: "auto" }}>
        <Table size="small" sx={{ minWidth: 640 }}>
          <TableHead>
            <TableRow>
              <TableCell>Line</TableCell>
              <TableCell align="right">Standard</TableCell>
              <TableCell align="right">Chance by date</TableCell>
              <TableCell align="right">Projected</TableCell>
              <TableCell align="right">Ready in</TableCell>
              <TableCell align="right">h/week to hit</TableCell>
              <TableCell>How</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {gate.lines.map((line) => {
              const lv = verdict(line.probability);
              return (
                <TableRow key={line.metric}>
                  <TableCell>{line.label}</TableCell>
                  <TableCell align="right">
                    {line.comparison === "AtLeast" ? "≥ " : "≤ "}
                    {formatRequirement(line.unit, line.target)}
                  </TableCell>
                  <TableCell align="right">
                    <Chip size="small" label={lv.label} color={lv.color} />
                  </TableCell>
                  <TableCell align="right">
                    {line.projected === null
                      ? "—"
                      : formatRequirement(line.unit, line.projected)}
                  </TableCell>
                  <TableCell align="right">
                    {months(line.readyInMonths)}
                  </TableCell>
                  <TableCell align="right">
                    {line.hoursToReach === null
                      ? "—"
                      : line.hoursToReach === 0
                        ? "any"
                        : `${line.hoursToReach.toFixed(1)} h`}
                  </TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      variant="outlined"
                      label={methodLabel[line.method]}
                    />{" "}
                    <Typography
                      component="span"
                      variant="caption"
                      sx={{ color: "text.secondary" }}
                    >
                      {line.evidence}
                    </Typography>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </Box>
    </Box>
  );
}
