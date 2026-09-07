import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { type FormEvent, useState } from "react";
import {
  type DrillKind,
  isCorrect,
  kindLabel,
  kinds,
  makeSession,
  type Result,
  type Session,
  score,
  seedForDate,
} from "../core/drills";
import { useDrillHistory } from "../hooks/useDrillHistory";

interface Props {
  /** Fixed for tests; the page uses today's date. */
  seed?: number;
  count?: number;
  /** Fixed for tests; the page uses the wall clock. */
  now?: () => Date;
}

type Phase =
  | { name: "idle" }
  | {
      name: "asking";
      session: Session;
      index: number;
      answers: string[];
      startedAt: number;
      /** Set once the current problem has been answered and is being shown. */
      revealed: boolean;
    }
  | { name: "done"; result: Result };

const formatSeconds = (seconds: number): string =>
  `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`;

/**
 * The daily drill. Twenty problems across the plan's arithmetic, a clock
 * from the first problem to the last, a score at the end, and a history of
 * the last sessions. The session is seeded from the date, so a second run
 * on the same day is the same problems: a retake, not a new test.
 */
export default function DrillPanel({ seed, count = 20, now }: Props) {
  const clock = now ?? (() => new Date());
  const { history, record } = useDrillHistory();
  const [phase, setPhase] = useState<Phase>({ name: "idle" });
  const [input, setInput] = useState("");

  const start = () => {
    const session = makeSession(seed ?? seedForDate(clock()), count);
    setPhase({
      name: "asking",
      session,
      index: 0,
      answers: [],
      startedAt: clock().getTime(),
      revealed: false,
    });
    setInput("");
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (phase.name !== "asking" || phase.revealed) return;
    setPhase({
      ...phase,
      answers: [...phase.answers, input],
      revealed: true,
    });
  };

  const next = () => {
    if (phase.name !== "asking") return;
    const index = phase.index + 1;
    setInput("");
    if (index < phase.session.problems.length) {
      setPhase({ ...phase, index, revealed: false });
      return;
    }
    const finished = clock();
    const seconds = Math.round((finished.getTime() - phase.startedAt) / 1000);
    const result = score(phase.session, phase.answers, seconds, finished);
    record(result);
    setPhase({ name: "done", result });
  };

  const best = history.reduce<Result | null>(
    (top, entry) =>
      top === null ||
      entry.correct > top.correct ||
      (entry.correct === top.correct && entry.seconds < top.seconds)
        ? entry
        : top,
    null,
  );

  return (
    <Paper variant="outlined" sx={{ p: 2.5 }}>
      <Stack spacing={2}>
        <Box>
          <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
            Daily drill
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {count} problems: decibels, wavelength, dipole length, Ohm's law and
            subnetting, four of each. The clock runs from the first problem to
            the last. Same problems all day; new ones tomorrow.
          </Typography>
        </Box>

        {phase.name === "idle" && (
          <Button
            variant="contained"
            onClick={start}
            sx={{ alignSelf: "flex-start" }}
          >
            Start today's drill
          </Button>
        )}

        {phase.name === "asking" && (
          <Box component="form" onSubmit={submit}>
            <Stack spacing={1.5}>
              <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                <Chip
                  size="small"
                  label={kindLabel[phase.session.problems[phase.index].kind]}
                />
                <Typography variant="caption" sx={{ color: "text.secondary" }}>
                  {phase.index + 1} of {phase.session.problems.length}
                </Typography>
              </Stack>
              <Typography variant="body1" component="p">
                {phase.session.problems[phase.index].prompt}
              </Typography>
              <Stack
                direction="row"
                spacing={1}
                sx={{ alignItems: "flex-start" }}
              >
                <TextField
                  size="small"
                  label="Answer"
                  value={input}
                  onChange={(event) => setInput(event.target.value)}
                  disabled={phase.revealed}
                  autoComplete="off"
                  slotProps={{ htmlInput: { inputMode: "decimal" } }}
                />
                {phase.revealed ? (
                  <Button variant="contained" onClick={next}>
                    {phase.index + 1 < phase.session.problems.length
                      ? "Next"
                      : "Finish"}
                  </Button>
                ) : (
                  <Button variant="contained" type="submit">
                    Check
                  </Button>
                )}
              </Stack>
              {phase.revealed && (
                <Verdict
                  correct={isCorrect(
                    phase.session.problems[phase.index],
                    phase.answers[phase.index] ?? "",
                  )}
                  answer={phase.session.problems[phase.index].answer}
                  unit={phase.session.problems[phase.index].unit}
                  explanation={phase.session.problems[phase.index].explanation}
                />
              )}
            </Stack>
          </Box>
        )}

        {phase.name === "done" && (
          <Stack spacing={1.5}>
            <Typography variant="h6" component="p">
              {phase.result.correct} of {phase.result.total} in{" "}
              {formatSeconds(phase.result.seconds)}
            </Typography>
            <Stack
              direction="row"
              spacing={1}
              useFlexGap
              sx={{ flexWrap: "wrap" }}
            >
              {kinds.map((kind) => (
                <Chip
                  key={kind}
                  size="small"
                  variant="outlined"
                  label={`${kindLabel[kind]} ${phase.result.byKind[kind].correct}/${phase.result.byKind[kind].total}`}
                />
              ))}
            </Stack>
            <Button
              variant="outlined"
              onClick={start}
              sx={{ alignSelf: "flex-start" }}
            >
              Run it again
            </Button>
          </Stack>
        )}

        {history.length > 0 && (
          <Box>
            <Typography
              variant="overline"
              component="p"
              sx={{ lineHeight: 1.5 }}
            >
              Last sessions
            </Typography>
            <TableContainer>
              <Table size="small" aria-label="Drill history">
                <TableHead>
                  <TableRow>
                    <TableCell>Date</TableCell>
                    <TableCell align="right">Score</TableCell>
                    <TableCell align="right">Time</TableCell>
                    <TableCell>Weakest</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {[...history]
                    .reverse()
                    .slice(0, 7)
                    .map((entry) => (
                      <TableRow key={entry.at}>
                        <TableCell>{entry.at.slice(0, 10)}</TableCell>
                        <TableCell align="right">
                          {entry.correct}/{entry.total}
                        </TableCell>
                        <TableCell align="right">
                          {formatSeconds(entry.seconds)}
                        </TableCell>
                        <TableCell>{weakest(entry)}</TableCell>
                      </TableRow>
                    ))}
                </TableBody>
              </Table>
            </TableContainer>
            {best && (
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Best: {best.correct}/{best.total} in{" "}
                {formatSeconds(best.seconds)} on {best.at.slice(0, 10)}. Aim:
                every one right, under ten minutes.
              </Typography>
            )}
          </Box>
        )}
      </Stack>
    </Paper>
  );
}

/** The kind with the lowest share right, or a dash when nothing was missed. */
function weakest(result: Result): string {
  let worst: DrillKind | null = null;
  let worstShare = 1;
  for (const kind of kinds) {
    const { total, correct } = result.byKind[kind];
    if (total === 0) continue;
    const share = correct / total;
    if (share < worstShare) {
      worstShare = share;
      worst = kind;
    }
  }
  return worst ? kindLabel[worst] : "none";
}

function Verdict({
  correct,
  answer,
  unit,
  explanation,
}: {
  correct: boolean;
  answer: string;
  unit?: string;
  explanation: string;
}) {
  return (
    <Box role="status">
      <Typography
        variant="body2"
        sx={{ fontWeight: 600, color: correct ? "success.main" : "error.main" }}
      >
        {correct
          ? "Right."
          : `Wrong. The answer is ${answer}${unit ? ` ${unit}` : ""}.`}
      </Typography>
      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        {explanation}
      </Typography>
    </Box>
  );
}
