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
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { useState } from "react";
import {
  type Answer,
  gradeCard,
  gradeChoice,
  gradeCode,
  gradeNumber,
  gradeProof,
  referenceUrl,
  rightAnswer,
  timeLabel,
} from "../core/assessment";
import { readError } from "../core/grade";
import type { Question } from "../core/syllabus";

const mono = { fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace" };

const typeLabel: Record<Question["type"], string> = {
  choice: "Multiple choice",
  number: "Short answer",
  proof: "Proof",
  card: "Card",
  code: "Code",
};

/** What a wrong answer gets: the right answer, why, and where to learn it. */
export function Feedback({
  question,
  answer,
}: {
  question: Question;
  answer: Answer;
}) {
  const ref = question.ref;
  return (
    <Alert
      severity={answer.correct ? "success" : "warning"}
      aria-label={answer.correct ? "Right" : "Wrong"}
    >
      <Stack spacing={0.5}>
        <Typography variant="body2" sx={{ fontWeight: 600 }}>
          {answer.correct ? "Right." : "Not right."}
        </Typography>
        {!answer.correct && question.type !== "code" && (
          <Typography variant="body2">
            The right answer: {rightAnswer(question)}
          </Typography>
        )}
        <Typography variant="body2">Why: {question.why}</Typography>
        <Typography variant="body2">
          {answer.correct ? "Reference: " : "Learn it here: "}
          <Link href={referenceUrl(ref)} target="_blank" rel="noopener">
            {ref.label}
          </Link>
          {ref.at !== undefined && !referenceUrl(ref).includes("t=")
            ? `, at ${timeLabel(ref.at)}`
            : ""}
        </Typography>
      </Stack>
    </Alert>
  );
}

interface RunResult {
  compiled: boolean;
  error?: string | null;
  tests: { passed: boolean }[];
}

/**
 * One question, answered here. `onAnswer` gets the graded answer once.
 * After it, the card shows the feedback.
 */
export default function QuestionCard({
  question,
  index,
  answer,
  onAnswer,
}: {
  question: Question;
  index?: number;
  /** Set once answered: the card then shows its feedback. */
  answer?: Answer;
  onAnswer: (answer: Answer) => void;
}) {
  return (
    <Paper
      variant="outlined"
      component="article"
      aria-label={`Question${index ? ` ${index}` : ""}: ${question.prompt}`}
      sx={{ p: 2 }}
    >
      <Stack spacing={1.5}>
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          {index ? `${index}. ` : ""}
          {typeLabel[question.type]}
        </Typography>
        <Typography variant="body1" sx={{ whiteSpace: "pre-wrap" }}>
          {question.prompt}
        </Typography>
        <Body question={question} answer={answer} onAnswer={onAnswer} />
        {answer && <Feedback question={question} answer={answer} />}
      </Stack>
    </Paper>
  );
}

function Body({
  question,
  answer,
  onAnswer,
}: {
  question: Question;
  answer?: Answer;
  onAnswer: (answer: Answer) => void;
}) {
  switch (question.type) {
    case "choice":
      return <Choice question={question} answer={answer} onAnswer={onAnswer} />;
    case "number":
      return (
        <NumberAnswer question={question} answer={answer} onAnswer={onAnswer} />
      );
    case "proof":
      return <Proof question={question} answer={answer} onAnswer={onAnswer} />;
    case "card":
      return <Card question={question} answer={answer} onAnswer={onAnswer} />;
    case "code":
      return <Code question={question} answer={answer} onAnswer={onAnswer} />;
  }
}

function Choice({
  question,
  answer,
  onAnswer,
}: {
  question: Question & { type: "choice" };
  answer?: Answer;
  onAnswer: (answer: Answer) => void;
}) {
  const [picked, setPicked] = useState<number | null>(
    answer?.given !== undefined ? Number(answer.given) : null,
  );
  return (
    <Box>
      <RadioGroup
        aria-label="Choices"
        value={picked === null ? "" : String(picked)}
        onChange={(event) => setPicked(Number(event.target.value))}
      >
        {question.choices.map((choice, i) => (
          <FormControlLabel
            // biome-ignore lint/suspicious/noArrayIndexKey: choices are fixed and may repeat
            key={i}
            value={String(i)}
            control={<Radio disabled={!!answer} />}
            label={choice}
          />
        ))}
      </RadioGroup>
      {!answer && (
        <Button
          variant="contained"
          size="small"
          disabled={picked === null}
          onClick={() =>
            picked !== null && onAnswer(gradeChoice(question, picked))
          }
        >
          Check
        </Button>
      )}
    </Box>
  );
}

function NumberAnswer({
  question,
  answer,
  onAnswer,
}: {
  question: Question & { type: "number" };
  answer?: Answer;
  onAnswer: (answer: Answer) => void;
}) {
  const [text, setText] = useState(answer?.given ?? "");
  const problem = text.trim() ? readError(text) : null;
  return (
    <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
      <TextField
        size="small"
        label="Your answer"
        value={text}
        disabled={!!answer}
        onChange={(event) => setText(event.target.value)}
        error={!!problem}
        helperText={
          problem ??
          "A number, a fraction like 3/4, or an expression like 2^10 or sqrt(2)/2."
        }
        slotProps={{ htmlInput: { maxLength: 200, style: mono } }}
        sx={{ minWidth: 260 }}
      />
      {!answer && (
        <Box>
          <Button
            variant="contained"
            size="small"
            disabled={!text.trim() || !!problem}
            onClick={() => onAnswer(gradeNumber(question, text.trim()))}
          >
            Check
          </Button>
        </Box>
      )}
    </Stack>
  );
}

/**
 * Write the proof, then grade it yourself against the rubric. It is right
 * when every correctness line is met. Elegance and clarity are noted, as
 * MIT 18.A34 grants or deducts for them beyond correctness.
 */
function Proof({
  question,
  answer,
  onAnswer,
}: {
  question: Question & { type: "proof" };
  answer?: Answer;
  onAnswer: (answer: Answer) => void;
}) {
  const [text, setText] = useState(answer?.given ?? "");
  const [grading, setGrading] = useState(false);
  const [met, setMet] = useState<boolean[]>(
    answer?.proof?.met ?? question.rubric.map(() => false),
  );
  const [elegant, setElegant] = useState(answer?.proof?.elegant ?? false);
  const [clear, setClear] = useState(answer?.proof?.clear ?? false);
  const locked = !!answer;
  return (
    <Stack spacing={1.5}>
      <TextField
        label="Your proof"
        multiline
        minRows={4}
        value={text}
        disabled={locked || grading}
        onChange={(event) => setText(event.target.value)}
        slotProps={{ htmlInput: { maxLength: 8000 } }}
      />
      {!grading && !locked ? (
        <Box>
          <Button
            variant="contained"
            size="small"
            disabled={!text.trim()}
            onClick={() => setGrading(true)}
          >
            Grade it against the rubric
          </Button>
        </Box>
      ) : (
        <Stack spacing={0.5} role="group" aria-label="Rubric">
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            Correctness: tick each point your proof makes.
          </Typography>
          {question.rubric.map((line, i) => (
            <FormControlLabel
              // biome-ignore lint/suspicious/noArrayIndexKey: rubric lines are fixed
              key={i}
              control={
                <Checkbox
                  checked={met[i] ?? false}
                  disabled={locked}
                  onChange={(event) =>
                    setMet((current) =>
                      current.map((value, j) =>
                        j === i ? event.target.checked : value,
                      ),
                    )
                  }
                />
              }
              label={line}
            />
          ))}
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            Beyond correctness, as MIT 18.A34 grades:
          </Typography>
          <FormControlLabel
            control={
              <Checkbox
                checked={elegant}
                disabled={locked}
                onChange={(event) => setElegant(event.target.checked)}
              />
            }
            label="Elegance: no needless cases or steps."
          />
          <FormControlLabel
            control={
              <Checkbox
                checked={clear}
                disabled={locked}
                onChange={(event) => setClear(event.target.checked)}
              />
            }
            label="Clarity: a reader follows every step without filling gaps."
          />
          {!locked && (
            <Box>
              <Button
                variant="contained"
                size="small"
                onClick={() =>
                  onAnswer(gradeProof(text, { met, elegant, clear }))
                }
              >
                Record the grade
              </Button>
            </Box>
          )}
        </Stack>
      )}
    </Stack>
  );
}

function Card({
  question,
  answer,
  onAnswer,
}: {
  question: Question & { type: "card" };
  answer?: Answer;
  onAnswer: (answer: Answer) => void;
}) {
  const [shown, setShown] = useState(!!answer);
  const ratings: { rating: 1 | 2 | 3 | 4; label: string }[] = [
    { rating: 1, label: "Again" },
    { rating: 2, label: "Hard" },
    { rating: 3, label: "Good" },
    { rating: 4, label: "Easy" },
  ];
  if (!shown)
    return (
      <Box>
        <Button variant="contained" size="small" onClick={() => setShown(true)}>
          Show the answer
        </Button>
      </Box>
    );
  return (
    <Stack spacing={1}>
      <Typography variant="body1" sx={{ whiteSpace: "pre-wrap" }}>
        {question.back}
      </Typography>
      {!answer && (
        <Stack
          direction="row"
          spacing={1}
          role="group"
          aria-label="How well did you recall it?"
        >
          {ratings.map((each) => (
            <Button
              key={each.rating}
              size="small"
              variant={each.rating === 1 ? "outlined" : "contained"}
              color={each.rating === 1 ? "warning" : "primary"}
              onClick={() => onAnswer(gradeCard(each.rating))}
            >
              {each.label}
            </Button>
          ))}
        </Stack>
      )}
    </Stack>
  );
}

/** Code checked by the drill endpoint: Compiler Explorer runs it, the server compares. */
function Code({
  question,
  answer,
  onAnswer,
}: {
  question: Question & { type: "code" };
  answer?: Answer;
  onAnswer: (answer: Answer) => void;
}) {
  const [source, setSource] = useState(answer?.given ?? question.starter ?? "");
  const [running, setRunning] = useState(false);
  const [result, setResult] = useState<RunResult | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const passed =
    !!result &&
    result.compiled &&
    result.tests.length === question.tests.length &&
    result.tests.every((test) => test.passed);

  async function run() {
    setRunning(true);
    setFailure(null);
    try {
      const response = await fetch("/api/training/drills/run", {
        method: "POST",
        credentials: "same-origin",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ source, tests: question.tests }),
      });
      if (!response.ok) {
        setFailure(
          response.status === 429
            ? "Too many runs this minute. Wait a moment and run it again."
            : `The run failed: ${response.status}.`,
        );
        return;
      }
      setResult((await response.json()) as RunResult);
    } catch {
      setFailure("The run did not reach the server.");
    } finally {
      setRunning(false);
    }
  }

  return (
    <Stack spacing={1}>
      <TextField
        label="Your C++"
        multiline
        minRows={6}
        value={source}
        disabled={!!answer}
        onChange={(event) => setSource(event.target.value)}
        slotProps={{ htmlInput: { spellCheck: false, style: mono } }}
      />
      {!answer && (
        <Stack direction="row" spacing={1}>
          <Button
            variant="outlined"
            size="small"
            disabled={running}
            onClick={run}
          >
            {running ? "Running" : `Run ${question.tests.length} tests`}
          </Button>
          <Button
            variant="contained"
            size="small"
            disabled={!result}
            onClick={() => onAnswer(gradeCode(source, passed))}
          >
            Submit
          </Button>
        </Stack>
      )}
      {failure && <Alert severity="error">{failure}</Alert>}
      {result && (
        <Typography variant="body2">
          {result.compiled
            ? `${result.tests.filter((test) => test.passed).length} of ${question.tests.length} tests passed.`
            : `No test ran. ${result.error ?? ""}`}
        </Typography>
      )}
    </Stack>
  );
}
