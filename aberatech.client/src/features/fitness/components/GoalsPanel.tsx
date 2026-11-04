import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Collapse from "@mui/material/Collapse";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Tooltip from "@mui/material/Tooltip";
import Typography from "@mui/material/Typography";
import * as React from "react";
import {
  fetchGoals,
  type GoalRow,
  type GoalSection,
  type GoalStandard,
  type Goals,
  oneDriveConnectUrl,
} from "../core/api";
import { formatChance, formatSeconds } from "../core/format";

/** A goal's number in the unit the athlete wrote it in. */
export function formatGoalValue(unit: string, value: number): string {
  switch (unit) {
    case "s": {
      // A sprint is timed to the tenth; everything else to the second.
      if (!Number.isInteger(Math.round(value * 10) / 10) && value < 600) {
        const minutes = Math.floor(value / 60);
        const seconds = (value - minutes * 60).toFixed(1).padStart(4, "0");
        return `${minutes}:${seconds}`;
      }
      return formatSeconds(value);
    }
    case "s/mi":
      return `${formatSeconds(value)}/mi`;
    case "reps":
      return String(Math.round(value));
    case "lb":
      return `${Number(value.toFixed(1))} lb`;
    case "%":
      return `${Number(value.toFixed(1))}%`;
    case "m":
      return `${Math.round(value)} m`;
    default:
      return String(Number(value.toFixed(2)));
  }
}

/**
 * An outside standard as a tag beside the goal: who sets it, at what level,
 * and the number when it differs from the goal ("FBI max 20"). When the goal
 * is that standard, the tag names it without repeating the number.
 */
export function standardLabel(row: GoalRow, standard: GoalStandard): string {
  const level = standard.level.replace(/\s*\(.*\)$/, "");
  const same = row.goal !== null && Math.abs(row.goal - standard.value) < 0.05;
  return same
    ? `${standard.source} ${level}`
    : `${standard.source} ${level} ${formatGoalValue(row.unit, standard.value)}`;
}

/** The tag a goal earns from the athlete's own note in column I. */
export function goalTag(row: GoalRow): string | null {
  const note = row.note ?? "";
  if (/inferred/i.test(note)) return "inferred";
  if (/FBI PFT maximum/i.test(note)) return "FBI max";
  if (/Evoke/i.test(note)) return "Evoke";
  if (row.goalColumn === "D") return "ideal";
  return null;
}

const DAY_MS = 86_400_000;

/** Whole days from an ISO date to today, in the viewer's calendar. */
export function daysSince(on: string, today: Date = new Date()): number {
  const [y, m, d] = on.split("-").map(Number);
  const then = Date.UTC(y, m - 1, d);
  const now = Date.UTC(today.getFullYear(), today.getMonth(), today.getDate());
  return Math.max(0, Math.round((now - then) / DAY_MS));
}

/** How old a result is, short enough to sit beside it: 3d, 5wk, 7mo, 2y. */
export function ageLabel(days: number): string {
  if (days < 14) return `${days}d`;
  if (days < 70) return `${Math.round(days / 7)}wk`;
  if (days < 730) return `${Math.round(days / 30.4375)}mo`;
  return `${Math.round(days / 365.25)}y`;
}

/** The hover text: what the number is, the date it is from, and its source. */
export function latestDetail(
  current: NonNullable<GoalRow["current"]>,
  today: Date = new Date(),
): string {
  const kind =
    current.basis === "Modeled"
      ? "Modelled, not done as this event"
      : current.basis === "Training"
        ? "From training, a floor for the test and not a test result"
        : "Measured";
  if (!current.on) return `${kind}: ${current.evidence}.`;
  const date = new Date(`${current.on}T12:00:00`).toLocaleDateString("en-US", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
  const days = daysSince(current.on, today);
  const ago =
    days === 0 ? "today" : days === 1 ? "1 day ago" : `${days} days ago`;
  return `${kind} ${date} (${ago}): ${current.evidence}.`;
}

/**
 * One line a row on a laptop, where the tab's height is budgeted; free to
 * wrap on a phone, where four columns on one line ran past a 390 px screen.
 */
const WRAP_ON_PHONE = { xs: "normal", sm: "nowrap" } as const;

function Points({ value }: { value: number | null }) {
  if (value === null) return null;
  return (
    <Typography component="span" variant="body2" color="text.secondary">
      {" "}
      · {value}
    </Typography>
  );
}

function GoalLine({ row, test }: { row: GoalRow; test: GoalSection["test"] }) {
  const [open, setOpen] = React.useState(false);
  const tag = goalTag(row);
  const detailId = `goal-detail-${row.row}`;
  const forecast = row.forecast;

  return (
    <>
      <TableRow sx={{ "& > td": { borderBottom: open ? 0 : undefined } }}>
        <TableCell component="th" scope="row" sx={{ pl: 0 }}>
          {row.label}
        </TableCell>
        <TableCell align="right" sx={{ whiteSpace: WRAP_ON_PHONE }}>
          {row.current ? (
            <Tooltip title={latestDetail(row.current)} placement="top" arrow>
              <Box
                component="span"
                tabIndex={0}
                aria-label={`${formatGoalValue(row.unit, row.current.value)}, ${latestDetail(row.current)}`}
                sx={{ cursor: "help" }}
              >
                {formatGoalValue(row.unit, row.current.value)}
                {row.current.basis === "Modeled" ||
                row.current.basis === "Training" ? (
                  <Chip
                    size="small"
                    label={
                      row.current.basis === "Modeled" ? "modelled" : "training"
                    }
                    variant="outlined"
                    sx={{ ml: 0.5, height: 20 }}
                  />
                ) : null}
                {row.current.on ? (
                  <Typography
                    component="span"
                    variant="caption"
                    color="text.secondary"
                    data-testid="latest-age"
                  >
                    {" "}
                    {ageLabel(daysSince(row.current.on))}
                  </Typography>
                ) : null}
              </Box>
            </Tooltip>
          ) : row.metric ? (
            "untested"
          ) : (
            "not measured"
          )}
          {test ? <Points value={row.pointsNow} /> : null}
        </TableCell>
        <TableCell align="right" sx={{ whiteSpace: WRAP_ON_PHONE }}>
          {row.goal !== null
            ? formatGoalValue(row.unit, row.goal)
            : "blank in the sheet"}
          {test ? <Points value={row.pointsGoal} /> : null}
          {tag ? (
            <Chip
              size="small"
              label={tag}
              variant="outlined"
              sx={{ ml: 0.75, height: 20 }}
            />
          ) : null}
          {(row.standards ?? [])
            .filter((standard) => standardLabel(row, standard) !== tag)
            .map((standard) => (
              <Tooltip
                key={standard.source}
                title={standard.note}
                placement="top"
                arrow
              >
                <Chip
                  size="small"
                  label={standardLabel(row, standard)}
                  variant="outlined"
                  tabIndex={0}
                  sx={{ ml: 0.75, height: 20 }}
                />
              </Tooltip>
            ))}
        </TableCell>
        <TableCell align="right" sx={{ pr: 0, whiteSpace: WRAP_ON_PHONE }}>
          {forecast ? (
            <Button
              size="small"
              onClick={() => setOpen((v) => !v)}
              aria-expanded={open}
              aria-controls={detailId}
              aria-label={`Forecast for ${row.label}`}
            >
              {forecast.probability !== null
                ? formatChance(forecast.probability)
                : forecast.projected !== null
                  ? formatGoalValue(row.unit, forecast.projected)
                  : "how"}
            </Button>
          ) : null}
        </TableCell>
      </TableRow>
      {/* The drawer row exists only while open: a closed one still took the
          table's cell padding, 9 px a goal and 171 px across nineteen. */}
      {forecast && open ? (
        <TableRow>
          <TableCell colSpan={4} sx={{ py: 0, px: 0 }}>
            <Collapse in={open} appear>
              <Box id={detailId} sx={{ py: 1.5 }}>
                <Typography variant="body2">
                  {forecast.projected !== null
                    ? `On the due date: ${formatGoalValue(row.unit, forecast.projected)}. `
                    : ""}
                  {forecast.probability !== null
                    ? `Chance of the goal by then: ${formatChance(forecast.probability)}. `
                    : ""}
                  {forecast.hoursToReach !== null
                    ? `Running hours a week that reach it: ${forecast.hoursToReach.toFixed(1)}.`
                    : ""}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {forecast.evidence}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {row.current
                    ? `Latest: ${latestDetail(row.current)}${row.current.basis === "Modeled" ? " Modelled means worked out from other results, not timed or lifted as this event." : ""}`
                    : "Latest: nothing in the log measures this yet."}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Sheet row {row.row}
                  {row.baseline !== null
                    ? `, baseline ${formatGoalValue(row.unit, row.baseline)}`
                    : row.baselineText
                      ? `, baseline ${row.baselineText}`
                      : ""}
                  {row.goalColumn === "D"
                    ? ", goal from the Ideal column because the 2027 column is blank"
                    : ""}
                  .{row.note ? ` Your note: ${row.note}` : ""}
                </Typography>
              </Box>
            </Collapse>
          </TableCell>
        </TableRow>
      ) : null}
    </>
  );
}

function SectionCard({ section }: { section: GoalSection }) {
  const headingId = `goal-section-${section.name.replace(/\W+/g, "-").toLowerCase()}`;
  return (
    <Card variant="outlined" component="section" aria-labelledby={headingId}>
      <CardContent sx={{ "&:last-child": { pb: 1.5 } }}>
        <Stack
          direction="row"
          sx={{
            justifyContent: "space-between",
            alignItems: "baseline",
            mb: 1,
          }}
        >
          <Typography variant="h6" component="h2" id={headingId}>
            {section.name}
          </Typography>
          {section.test ? (
            <Typography variant="body2" color="text.secondary">
              <Typography
                component="span"
                variant="h6"
                color="text.primary"
                data-testid="points-now"
              >
                {section.pointsNow ?? "—"}
              </Typography>{" "}
              latest · goal {section.pointsGoal ?? "—"} of {section.pointsMax}{" "}
              pts
            </Typography>
          ) : null}
        </Stack>
        <Box sx={{ overflowX: "auto" }}>
          <Table
            size="small"
            aria-labelledby={headingId}
            sx={{
              "& td, & th": { py: 0.5, px: { xs: 0.5, sm: 2 } },
              "& .MuiButton-root": { minWidth: 0, px: { xs: 0.5, sm: 1 } },
              // The tags are the widest thing in a cell on a phone.
              "& .MuiChip-label": { px: { xs: 0.5, sm: 1 } },
              "& .MuiChip-root": {
                fontSize: { xs: "0.7rem", sm: "0.8125rem" },
              },
            }}
          >
            <TableHead>
              <TableRow>
                <TableCell sx={{ pl: 0 }}>Event</TableCell>
                <TableCell align="right">Latest</TableCell>
                <TableCell align="right">Goal</TableCell>
                <TableCell align="right" sx={{ pr: 0 }}>
                  Forecast
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {section.rows.map((row) => (
                <GoalLine key={row.row} row={row} test={section.test} />
              ))}
            </TableBody>
          </Table>
        </Box>
        {section.scoringNote ? (
          <Typography
            variant="caption"
            color="text.secondary"
            component="p"
            sx={{ mt: 1 }}
          >
            {section.scoringNote}
          </Typography>
        ) : null}
      </CardContent>
    </Card>
  );
}

/** The sections in order, as two columns of about the same number of rows. */
export function splitByRows(sections: GoalSection[]): GoalSection[][] {
  if (sections.length < 2) return [sections];
  const sizes = sections.map((s) => s.rows.length + 1);
  const total = sizes.reduce((a, b) => a + b, 0);
  // The cut that leaves the two columns closest to equal.
  let best = 1;
  let bestGap = Number.POSITIVE_INFINITY;
  let left = 0;
  for (let cut = 1; cut < sections.length; cut++) {
    left += sizes[cut - 1];
    const gap = Math.abs(total - 2 * left);
    if (gap < bestGap) {
      bestGap = gap;
      best = cut;
    }
  }
  return [sections.slice(0, best), sections.slice(best)];
}

function SourceLine({ goals }: { goals: Goals }) {
  const { source } = goals;
  if (!source.configured) {
    return (
      <Alert severity="info">
        This deployment has no OneDrive connection set up, so the goals workbook
        cannot be read.
      </Alert>
    );
  }
  if (!source.connected) {
    return (
      <Alert
        severity="info"
        action={
          <Button color="inherit" size="small" href={oneDriveConnectUrl}>
            Connect OneDrive
          </Button>
        }
      >
        Your goals come from physical fitness goals.xlsx in OneDrive. Connect it
        once to read them.
      </Alert>
    );
  }
  const read = source.readAt ? new Date(source.readAt).toLocaleString() : null;
  return (
    <>
      {source.error ? (
        <Alert severity="warning">
          {source.error}
          {read ? ` Showing the goals as read ${read}.` : ""}
        </Alert>
      ) : null}
      <Typography variant="body2" color="text.secondary">
        Goals for {goals.dueOn ?? "the sheet's year"}, read from{" "}
        {source.origin === "OneDrive"
          ? "physical fitness goals.xlsx in OneDrive"
          : "the local workbook"}
        {read ? ` at ${read}` : ""}. Forecasts assume{" "}
        {goals.weeklyHours.toFixed(1)} h of running a week, {goals.weekBasis}.
      </Typography>
    </>
  );
}

/**
 * The Goals tab: the athlete's own workbook, its groups in its order, each
 * test scored the way its graders score it, every number one click from how
 * it was made.
 */
export default function GoalsPanel() {
  const [goals, setGoals] = React.useState<Goals | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    const controller = new AbortController();
    fetchGoals(controller.signal)
      .then(setGoals)
      .catch((e: unknown) => {
        if (controller.signal.aborted) return;
        setError(e instanceof Error ? e.message : String(e));
      });
    return () => controller.abort();
  }, []);

  if (error)
    return <Alert severity="error">The goals did not load: {error}</Alert>;
  if (!goals) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
        <CircularProgress aria-label="Loading the goals" />
      </Box>
    );
  }

  return (
    <Stack spacing={2}>
      <SourceLine goals={goals} />
      {/* Two columns split by row count, in the sheet's order: the first
          sections down the left until it holds half the rows, the rest down
          the right. Every engine lays this out the same way, which CSS
          columns did not. */}
      <Box
        sx={{
          display: "grid",
          gap: 2,
          // minmax(0, …): a plain 1fr track grows to its widest table, which
          // pushed the page past a phone's edge.
          gridTemplateColumns: {
            xs: "minmax(0, 1fr)",
            md: "minmax(0, 1fr) minmax(0, 1fr)",
          },
          alignItems: "start",
        }}
      >
        {splitByRows(goals.sections).map((column) => (
          <Stack key={column[0]?.name ?? "empty"} spacing={2}>
            {column.map((section) => (
              <SectionCard key={section.name} section={section} />
            ))}
          </Stack>
        ))}
      </Box>
    </Stack>
  );
}
