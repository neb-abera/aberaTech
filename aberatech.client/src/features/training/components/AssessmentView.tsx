import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Link from "@mui/material/Link";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { useState } from "react";
import { Link as RouterLink } from "react-router";
import {
  type Answer,
  type CheckKind,
  checkKey,
  checkQuestions,
  masteryState,
  passMark,
  placementState,
  questionMinutes,
  questionMinutesBasis,
  recordAttempt,
  retestDays,
  score,
} from "../core/assessment";
import { addDays, localDate, longDate } from "../core/dates";
import type { TrainingDocument } from "../core/progress";
import { formatMinutes, syllabus } from "../core/syllabus";
import QuestionCard from "./QuestionCard";

const what: Record<CheckKind, string> = {
  quiz: "Quiz",
  pretest: "Pretest",
  mastery: "Mastery check",
};

const rule: Record<CheckKind, string> = {
  quiz: "Answer each question, then read the feedback. A wrong answer comes back in review until it is right on two later days.",
  pretest: `Try each question before the unit, whatever you know. ${passMark * 100}% or more places you out of the unit. Less is fine: the unit starts, and each wrong answer shows where it is taught.`,
  mastery: `${passMark * 100}% or more closes the unit. Below that, each wrong answer shows where to learn it, and the check opens again ${retestDays} days later.`,
};

/**
 * One check, a question at a time on one page: a lesson's quiz, a unit's
 * pretest, or its mastery check. The attempt is saved once every question
 * is answered.
 */
export default function AssessmentView({
  kind,
  id,
  document,
  change,
  now,
}: {
  kind: CheckKind;
  id: string;
  document: TrainingDocument;
  change: (fn: (current: TrainingDocument) => TrainingDocument) => void;
  now: Date;
}) {
  const questions = checkQuestions(kind, id);
  const unit =
    kind === "quiz"
      ? syllabus.units.find((each) =>
          each.lessons.some((lesson) => lesson.id === id),
        )
      : syllabus.units.find((each) => each.id === id);
  const lesson =
    kind === "quiz" ? unit?.lessons.find((each) => each.id === id) : undefined;
  const [answers, setAnswers] = useState<Record<string, Answer>>({});
  const [saved, setSaved] = useState<string | null>(null);
  const today = localDate(now);

  if (!unit || questions.length === 0)
    return <Alert severity="info">This check has no questions yet.</Alert>;

  const minutes = questions.reduce((sum, q) => sum + questionMinutes(q), 0);
  const mastery = kind === "mastery" ? masteryState(unit, document) : null;
  const waiting =
    !saved && mastery?.state === "failed" && mastery.opens > today
      ? mastery.opens
      : null;
  const placement = kind === "pretest" ? placementState(unit, document) : null;

  function answer(questionId: string, given: Answer) {
    const next = { ...answers, [questionId]: given };
    setAnswers(next);
    if (questions.every((question) => next[question.id])) {
      const at = new Date().toISOString();
      setSaved(at);
      change((current) =>
        recordAttempt(current, checkKey(kind, id), { at, answers: next }),
      );
    }
  }

  const result = saved ? score({ at: saved, answers }, questions) : null;
  const title = `${what[kind]}: ${lesson?.title ?? unit.title}`;

  return (
    <Stack spacing={2.5} component="section" aria-label={title}>
      <Box>
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          <Link component={RouterLink} to={`/training?unit=${unit.id}`}>
            {unit.title}
          </Link>
          {lesson && (
            <>
              {" "}
              ›{" "}
              <Link component={RouterLink} to={`/training?lesson=${lesson.id}`}>
                {lesson.title}
              </Link>
            </>
          )}
        </Typography>
        <Typography variant="h2" sx={{ fontSize: "1.75rem" }}>
          {title}
        </Typography>
        <Typography variant="body2">{rule[kind]}</Typography>
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          {questions.length} questions, about {formatMinutes(minutes)}.{" "}
          {questionMinutesBasis}
        </Typography>
      </Box>
      {mastery?.state === "passed" && !saved && (
        <Alert severity="success">
          Passed {longDate(mastery.at.slice(0, 10))}. The unit is closed.
        </Alert>
      )}
      {placement?.state === "passed" && !saved && (
        <Alert severity="success">Passed. The unit is placed out.</Alert>
      )}
      {waiting ? (
        <Alert severity="info">
          The last attempt scored{" "}
          {mastery?.state === "failed"
            ? `${mastery.score.right} of ${mastery.score.total}`
            : ""}
          . The retest opens {longDate(waiting)}. Until then, review what you
          missed.
        </Alert>
      ) : (
        <Stack spacing={2}>
          {questions.map((question, i) => (
            <QuestionCard
              key={question.id}
              question={question}
              index={i + 1}
              answer={answers[question.id]}
              onAnswer={(given) => answer(question.id, given)}
            />
          ))}
        </Stack>
      )}
      {result && (
        <Alert
          severity={kind === "quiz" || result.passed ? "success" : "warning"}
          aria-label="Result"
        >
          {result.right} of {result.total} right (
          {Math.round(result.share * 100)}%).{" "}
          {kind === "quiz"
            ? "The lesson's quiz is done."
            : kind === "pretest"
              ? result.passed
                ? "Placed out of the unit. Its lessons stay open for reference."
                : "Not placed out. The unit starts now."
              : result.passed
                ? "Mastered. The unit is closed."
                : `Not mastered. The retest opens ${longDate(addDays(today, retestDays))}.`}
        </Alert>
      )}
    </Stack>
  );
}
