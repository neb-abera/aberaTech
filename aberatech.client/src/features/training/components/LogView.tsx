import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Checkbox from "@mui/material/Checkbox";
import FormControlLabel from "@mui/material/FormControlLabel";
import Link from "@mui/material/Link";
import Paper from "@mui/material/Paper";
import Radio from "@mui/material/Radio";
import RadioGroup from "@mui/material/RadioGroup";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { useEffect, useState } from "react";
import { clock, localDate, localDateOf, shortDate } from "../core/dates";
import {
  addSolve,
  type Drill,
  drillTarget,
  type Help,
  helpLabel,
  removeSolve,
  setChecklist,
  startDrill,
  stopDrill,
  totalOf,
  weakSpots,
  weeklyAverages,
  weekStart,
} from "../core/log";
import type { TrainingDocument } from "../core/progress";
import {
  checklistSource,
  juniorSheet,
  letterRatings,
  type PoolProblem,
  weeklyChecklist,
} from "../core/saad";

type Change = (fn: (current: TrainingDocument) => TrainingDocument) => void;

const fields = [
  ["read", "Reading"],
  ["think", "Thinking"],
  ["code", "Coding"],
  ["debug", "Debugging"],
] as const;

/** A whole number of minutes, or nothing. */
const asMinutes = (text: string): number | null => {
  if (text.trim() === "") return 0;
  const value = Number(text);
  return Number.isInteger(value) && value >= 0 && value <= 1440 ? value : null;
};

function SolveForm({
  change,
  initial,
  pool,
}: {
  change: Change;
  initial: string;
  pool: PoolProblem[];
}) {
  const [problem, setProblem] = useState(initial);
  const [minutes, setMinutes] = useState<Record<string, string>>({
    read: "",
    think: "",
    code: "",
    debug: "",
  });
  const [submissions, setSubmissions] = useState("1");
  const [help, setHelp] = useState<Help>("alone");
  const [logged, setLogged] = useState<string | null>(null);
  useEffect(() => setProblem(initial), [initial]);

  const parsed = Object.fromEntries(
    fields.map(([key]) => [key, asMinutes(minutes[key] ?? "")]),
  ) as Record<(typeof fields)[number][0], number | null>;
  const subs = asMinutes(submissions);
  const valid =
    problem.trim() !== "" &&
    Object.values(parsed).every((value) => value !== null) &&
    subs !== null;
  const fromPool = pool.find((each) => each.code === problem.trim());

  return (
    <Paper
      variant="outlined"
      component="section"
      aria-label="Log a solve"
      sx={{ p: 2 }}
    >
      <Stack spacing={1.5}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          Log a solve
        </Typography>
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          Mostafa Saad's columns. Finish thinking before you code, so the
          minutes split cleanly.
        </Typography>
        <TextField
          size="small"
          label="Problem"
          value={problem}
          onChange={(event) => setProblem(event.target.value)}
          helperText={
            fromPool
              ? `From his pool: level ${fromPool.level}, P${fromPool.importance}.`
              : "A pool code, a judge's id like 2263B, or the problem's address."
          }
          slotProps={{ htmlInput: { maxLength: 300 } }}
        />
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
          {fields.map(([key, label]) => (
            <TextField
              key={key}
              size="small"
              type="number"
              label={`${label}, min`}
              value={minutes[key]}
              error={parsed[key] === null}
              onChange={(event) =>
                setMinutes((current) => ({
                  ...current,
                  [key]: event.target.value,
                }))
              }
              sx={{ width: { sm: 140 } }}
            />
          ))}
          <TextField
            size="small"
            type="number"
            label="Submissions"
            value={submissions}
            error={subs === null}
            onChange={(event) => setSubmissions(event.target.value)}
            sx={{ width: { sm: 130 } }}
          />
        </Stack>
        <RadioGroup
          row
          aria-label="Solved"
          value={help}
          onChange={(event) => setHelp(event.target.value as Help)}
        >
          {(Object.keys(helpLabel) as Help[]).map((key) => (
            <FormControlLabel
              key={key}
              value={key}
              control={<Radio />}
              label={helpLabel[key]}
            />
          ))}
        </RadioGroup>
        <Box>
          <Button
            variant="contained"
            disabled={!valid}
            onClick={() => {
              const name = problem.trim();
              change((current) =>
                addSolve(
                  current,
                  {
                    problem: name,
                    url:
                      fromPool?.url ??
                      (name.startsWith("https://") ? name : undefined),
                    ...(fromPool ? { level: fromPool.level } : {}),
                    read: parsed.read ?? 0,
                    think: parsed.think ?? 0,
                    code: parsed.code ?? 0,
                    debug: parsed.debug ?? 0,
                    submissions: subs ?? 0,
                    help,
                  },
                  new Date(),
                ),
              );
              setLogged(name);
              setProblem("");
              setMinutes({ read: "", think: "", code: "", debug: "" });
              setSubmissions("1");
              setHelp("alone");
            }}
          >
            Log it
          </Button>
        </Box>
        {logged && <Alert severity="success">Logged {logged}.</Alert>}
      </Stack>
    </Paper>
  );
}

function SpeedDrills({
  document,
  change,
}: {
  document: TrainingDocument;
  change: Change;
}) {
  const [drill, setDrill] = useState<Drill>("A");
  const [problem, setProblem] = useState("");
  const [submissions, setSubmissions] = useState("1");
  const [, setTick] = useState(0);
  const running = document.speed;
  // Re-render each second while the clock runs.
  useEffect(() => {
    if (!running) return;
    const timer = setInterval(() => setTick((value) => value + 1), 1000);
    return () => clearInterval(timer);
  }, [running]);
  const shown = running
    ? Math.max(0, Math.floor((Date.now() - Date.parse(running.started)) / 1000))
    : 0;
  return (
    <Paper
      variant="outlined"
      component="section"
      aria-label="Speed drills"
      sx={{ p: 2 }}
    >
      <Stack spacing={1.5}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          Speed drills
        </Typography>
        <Typography variant="body2">
          His targets: a Div. 2 A in {drillTarget.A} minutes and a B in{" "}
          {drillTarget.B}. Pick an unsolved one at{" "}
          <Link
            href={`https://codeforces.com/problemset?tags=${letterRatings.A}-${letterRatings.A}`}
            target="_blank"
            rel="noopener"
          >
            rating {letterRatings.A}
          </Link>{" "}
          for A or{" "}
          <Link
            href={`https://codeforces.com/problemset?tags=${letterRatings.B}-${letterRatings.B}`}
            target="_blank"
            rel="noopener"
          >
            {letterRatings.B}
          </Link>{" "}
          for B, the median rating of each letter in Div. 2 rounds (Codeforces
          API, 9 Nov 2026). Start the clock as you open the statement.
        </Typography>
        {running ? (
          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={1.5}
            sx={{ alignItems: { sm: "center" } }}
          >
            <Typography
              variant="h3"
              sx={{ fontSize: "1.5rem" }}
              aria-label="Elapsed"
            >
              {Math.floor(shown / 60)}:{String(shown % 60).padStart(2, "0")}
            </Typography>
            <Typography variant="body2">
              Div. 2 {running.drill}: {running.problem}. Target{" "}
              {drillTarget[running.drill]} min.
            </Typography>
            <TextField
              size="small"
              type="number"
              label="Submissions"
              value={submissions}
              onChange={(event) => setSubmissions(event.target.value)}
              sx={{ width: 130 }}
            />
            <Button
              variant="contained"
              onClick={() =>
                change((current) =>
                  stopDrill(current, new Date(), asMinutes(submissions) ?? 1),
                )
              }
            >
              Accepted: stop and log
            </Button>
          </Stack>
        ) : (
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
            <RadioGroup
              row
              aria-label="Drill"
              value={drill}
              onChange={(event) => setDrill(event.target.value as Drill)}
            >
              <FormControlLabel
                value="A"
                control={<Radio />}
                label="Div. 2 A"
              />
              <FormControlLabel
                value="B"
                control={<Radio />}
                label="Div. 2 B"
              />
            </RadioGroup>
            <TextField
              size="small"
              label="Drill problem"
              value={problem}
              onChange={(event) => setProblem(event.target.value)}
              slotProps={{ htmlInput: { maxLength: 300 } }}
            />
            <Button
              variant="outlined"
              disabled={!problem.trim()}
              onClick={() =>
                change((current) =>
                  startDrill(current, drill, problem.trim(), new Date()),
                )
              }
            >
              Start the clock
            </Button>
          </Stack>
        )}
      </Stack>
    </Paper>
  );
}

function Averages({
  document,
  today,
}: {
  document: TrainingDocument;
  today: string;
}) {
  const weeks = weeklyAverages(document.log, today);
  const spots = weakSpots(weeks[0]);
  return (
    <Paper
      variant="outlined"
      component="section"
      aria-label="Weekly averages"
      sx={{ p: 2 }}
    >
      <Stack spacing={1}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          Weekly averages
        </Typography>
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          Minutes per solve, averaged over the week's logged solves, Monday to
          Sunday. Speed drills are counted apart. Where the most time goes is
          the weak skill, in his words.
        </Typography>
        <Box sx={{ overflowX: "auto" }}>
          <Table size="small" aria-label="Averages by week">
            <TableHead>
              <TableRow>
                <TableCell>Week of</TableCell>
                <TableCell align="right">Solves</TableCell>
                <TableCell align="right">Read</TableCell>
                <TableCell align="right">Think</TableCell>
                <TableCell align="right">Code</TableCell>
                <TableCell align="right">Debug</TableCell>
                <TableCell align="right">Total</TableCell>
                <TableCell align="right">Submissions</TableCell>
                <TableCell>Alone, hint, editorial</TableCell>
                <TableCell>Drills on target</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {weeks.map((week) => (
                <TableRow key={week.week}>
                  <TableCell>{shortDate(week.week)}</TableCell>
                  <TableCell align="right">{week.solves}</TableCell>
                  <TableCell align="right">{week.read}</TableCell>
                  <TableCell align="right">{week.think}</TableCell>
                  <TableCell align="right">{week.code}</TableCell>
                  <TableCell align="right">{week.debug}</TableCell>
                  <TableCell align="right">{week.total}</TableCell>
                  <TableCell align="right">{week.submissions}</TableCell>
                  <TableCell>
                    {week.help.alone}, {week.help.hint}, {week.help.editorial}
                  </TableCell>
                  <TableCell>
                    A {week.drills.A.onTarget} of {week.drills.A.count}, B{" "}
                    {week.drills.B.onTarget} of {week.drills.B.count}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
        {spots.map((spot) => (
          <Alert key={spot} severity="info">
            {spot}
          </Alert>
        ))}
      </Stack>
    </Paper>
  );
}

function WeeklyReview({
  document,
  change,
  today,
}: {
  document: TrainingDocument;
  change: Change;
  today: string;
}) {
  const week = weekStart(today);
  const answers = document.weekly[week] ?? [];
  let area = "";
  return (
    <Paper
      variant="outlined"
      component="section"
      aria-label="Weekly review"
      sx={{ p: 2 }}
    >
      <Stack spacing={0.5}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          Weekly review, week of {shortDate(week)}
        </Typography>
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          <Link href={juniorSheet} target="_blank" rel="noopener">
            {checklistSource}
          </Link>{" "}
          Tick each one that held this week. An unticked line is the work for
          next week.
        </Typography>
        {weeklyChecklist.map((line, index) => {
          const heading = line.area !== area ? line.area : null;
          area = line.area;
          return (
            <Box key={line.question}>
              {heading && (
                <Typography variant="body2" sx={{ fontWeight: 600, mt: 1 }}>
                  {heading}
                </Typography>
              )}
              <FormControlLabel
                control={
                  <Checkbox
                    checked={answers[index] ?? false}
                    onChange={(event) =>
                      change((current) =>
                        setChecklist(
                          current,
                          week,
                          index,
                          event.target.checked,
                          weeklyChecklist.length,
                        ),
                      )
                    }
                  />
                }
                label={line.question}
              />
            </Box>
          );
        })}
        <Typography variant="body2">
          {answers.filter(Boolean).length} of {weeklyChecklist.length} held.
        </Typography>
      </Stack>
    </Paper>
  );
}

/** The solve log, its weekly averages, speed drills, and the weekly review. */
export default function LogView({
  document,
  change,
  now,
  initial,
  pool,
}: {
  document: TrainingDocument;
  change: Change;
  now: Date;
  /** A problem to log, from the pool panel's link. */
  initial: string;
  pool: PoolProblem[];
}) {
  const today = localDate(now);
  const recent = [...document.log].reverse().slice(0, 20);
  return (
    <Stack spacing={2.5} component="section" aria-label="Solve log">
      <SolveForm change={change} initial={initial} pool={pool} />
      <SpeedDrills document={document} change={change} />
      <Averages document={document} today={today} />
      <WeeklyReview document={document} change={change} today={today} />
      <Paper
        variant="outlined"
        component="section"
        aria-label="Recent solves"
        sx={{ p: 2 }}
      >
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          Recent solves
        </Typography>
        {recent.length === 0 ? (
          <Typography variant="body2">None logged yet.</Typography>
        ) : (
          <Stack component="ul" spacing={0.5} sx={{ pl: 2, m: 0 }}>
            {recent.map((solve) => (
              <Typography component="li" variant="body2" key={solve.id}>
                {shortDate(localDateOf(solve.at))} {clock(solve.at)}:{" "}
                {solve.url ? (
                  <Link href={solve.url} target="_blank" rel="noopener">
                    {solve.problem}
                  </Link>
                ) : (
                  solve.problem
                )}
                {solve.drill
                  ? `, Div. 2 ${solve.drill} drill in ${totalOf(solve)} min (target ${drillTarget[solve.drill]})`
                  : `, ${solve.read} + ${solve.think} + ${solve.code} + ${solve.debug} = ${totalOf(solve)} min, ${solve.submissions} submissions, ${helpLabel[solve.help].toLowerCase()}`}
                .{" "}
                <Button
                  size="small"
                  onClick={() =>
                    change((current) => removeSolve(current, solve.id))
                  }
                  aria-label={`Remove ${solve.problem}`}
                >
                  Remove
                </Button>
              </Typography>
            ))}
          </Stack>
        )}
      </Paper>
    </Stack>
  );
}
