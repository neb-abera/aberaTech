import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Link from "@mui/material/Link";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { useState } from "react";
import { Link as RouterLink } from "react-router";
import {
  type CheckState,
  checkQuestions,
  masteryState,
  passMark,
  placementState,
  setExternal,
} from "../core/assessment";
import { localDate, localDateOf, longDate } from "../core/dates";
import type { TrainingDocument } from "../core/progress";
import { countOf, type Unit } from "../core/syllabus";

function stateLine(state: CheckState): string {
  switch (state.state) {
    case "none":
      return "Not taken.";
    case "passed":
      return `Passed ${longDate(localDateOf(state.at))}${state.by === "outside" ? ", recorded by hand" : ""}${state.score ? `, ${state.score.right} of ${state.score.total}` : ""}.`;
    case "failed":
      return `Last taken ${longDate(localDateOf(state.at))}, ${state.score.right} of ${state.score.total}.`;
  }
}

/** A date at noon local time, as an ISO date and time. */
const noonOf = (date: string): string =>
  new Date(`${date}T12:00:00`).toISOString();

/**
 * A unit's tests besides its checkpoint: the pretest and the mastery check
 * on this page, and the ones taken elsewhere (an AoPS diagnostic, Alcumus
 * topics) that are recorded here by hand.
 */
export default function ChecksPanel({
  unit,
  owner,
  document,
  change,
  now,
}: {
  unit: Unit;
  owner: boolean;
  document: TrainingDocument;
  change: (fn: (current: TrainingDocument) => TrainingDocument) => void;
  now: Date;
}) {
  const pretest = checkQuestions("pretest", unit.id);
  const mastery = checkQuestions("mastery", unit.id);
  if (
    pretest.length === 0 &&
    mastery.length === 0 &&
    !unit.placement &&
    !unit.externalMastery
  )
    return null;
  const placement = placementState(unit, document);
  const mastered = masteryState(unit, document);
  const today = localDate(now);
  return (
    <Paper
      variant="outlined"
      component="section"
      aria-label="Tests"
      sx={{ p: 2.5 }}
    >
      <Stack spacing={2}>
        <Typography variant="h2" sx={{ fontSize: "1.25rem" }}>
          Tests
        </Typography>
        {pretest.length > 0 && (
          <Box>
            <Typography
              variant="subtitle1"
              component="h3"
              sx={{ fontWeight: 600 }}
            >
              Pretest, {countOf(pretest.length, "question")}
            </Typography>
            <Typography variant="body2">
              Taken before the unit. {passMark * 100}% or more places you out.
            </Typography>
            {owner && (
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                {stateLine(placement)}{" "}
                <Link
                  component={RouterLink}
                  to={`/training?check=pretest:${unit.id}`}
                >
                  {placement.state === "none"
                    ? "Take the pretest"
                    : "Take it again"}
                </Link>
              </Typography>
            )}
          </Box>
        )}
        {unit.placement && (
          <OutsidePlacement
            unit={unit}
            owner={owner}
            document={document}
            change={change}
            today={today}
          />
        )}
        {mastery.length > 0 && (
          <Box>
            <Typography
              variant="subtitle1"
              component="h3"
              sx={{ fontWeight: 600 }}
            >
              Mastery check, {countOf(mastery.length, "question")}
            </Typography>
            <Typography variant="body2">
              {passMark * 100}% or more closes the unit. Below that, the check
              opens again 2 days later.
            </Typography>
            {owner && (
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                {stateLine(mastered)}{" "}
                {mastered.state === "failed" && mastered.opens > today ? (
                  `The retest opens ${longDate(mastered.opens)}.`
                ) : (
                  <Link
                    component={RouterLink}
                    to={`/training?check=mastery:${unit.id}`}
                  >
                    {mastered.state === "none"
                      ? "Take the mastery check"
                      : "Take it again"}
                  </Link>
                )}
              </Typography>
            )}
          </Box>
        )}
        {unit.externalMastery && (
          <OutsideMastery
            unit={unit}
            owner={owner}
            document={document}
            change={change}
            today={today}
          />
        )}
      </Stack>
    </Paper>
  );
}

function OutsidePlacement({
  unit,
  owner,
  document,
  change,
  today,
}: {
  unit: Unit;
  owner: boolean;
  document: TrainingDocument;
  change: (fn: (current: TrainingDocument) => TrainingDocument) => void;
  today: string;
}) {
  const check = unit.placement;
  const key = `placement:${unit.id}`;
  const record = document.external[key];
  const [score, setScore] = useState(record?.score?.toString() ?? "");
  const [date, setDate] = useState(record ? localDateOf(record.at) : today);
  if (!check) return null;
  const value = Number(score);
  const valid =
    score.trim() !== "" &&
    Number.isFinite(value) &&
    value >= 0 &&
    (check.outOf === undefined || value <= check.outOf);
  return (
    <Box>
      <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 600 }}>
        Placement:{" "}
        <Link href={check.url} target="_blank" rel="noopener">
          {check.label}
        </Link>
      </Typography>
      <Typography variant="body2">
        {check.what} A score of {check.pass} out of {check.outOf} or more places
        you out. The page cannot read the score, so enter it here.
      </Typography>
      {owner && (
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={1.5}
          sx={{ mt: 1 }}
        >
          <TextField
            size="small"
            type="number"
            label={`Score out of ${check.outOf}`}
            value={score}
            onChange={(event) => setScore(event.target.value)}
            sx={{ width: 170 }}
          />
          <TextField
            size="small"
            type="date"
            label="Taken on"
            value={date}
            onChange={(event) => setDate(event.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <Button
            variant="contained"
            size="small"
            disabled={!valid || !date}
            onClick={() =>
              change((current) =>
                setExternal(current, key, { at: noonOf(date), score: value }),
              )
            }
          >
            Record the score
          </Button>
          {record && (
            <Button
              size="small"
              onClick={() =>
                change((current) => setExternal(current, key, null))
              }
            >
              Clear
            </Button>
          )}
        </Stack>
      )}
      {owner && record && (
        <Alert
          severity={
            record.score !== undefined &&
            check.pass !== undefined &&
            record.score >= check.pass
              ? "success"
              : "info"
          }
          sx={{ mt: 1 }}
        >
          {record.score} of {check.outOf} on {longDate(localDateOf(record.at))},
          by hand.{" "}
          {record.score !== undefined &&
          check.pass !== undefined &&
          record.score >= check.pass
            ? "Placed out."
            : `Under the pass mark of ${check.pass}. The unit starts.`}
        </Alert>
      )}
    </Box>
  );
}

function OutsideMastery({
  unit,
  owner,
  document,
  change,
  today,
}: {
  unit: Unit;
  owner: boolean;
  document: TrainingDocument;
  change: (fn: (current: TrainingDocument) => TrainingDocument) => void;
  today: string;
}) {
  const check = unit.externalMastery;
  const key = `mastery:${unit.id}`;
  const record = document.external[key];
  const [date, setDate] = useState(record ? localDateOf(record.at) : today);
  if (!check) return null;
  return (
    <Box>
      <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 600 }}>
        Mastery:{" "}
        <Link href={check.url} target="_blank" rel="noopener">
          {check.label}
        </Link>
      </Typography>
      <Typography variant="body2">{check.what}</Typography>
      {check.topics && check.topics.length > 0 && (
        <Stack component="ul" spacing={0.25} sx={{ pl: 2, my: 1 }}>
          {check.topics.map((topic) => (
            <Typography component="li" variant="body2" key={topic}>
              {topic}
            </Typography>
          ))}
        </Stack>
      )}
      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        The page cannot read your Alcumus report and never asks for your AoPS
        sign-in. Tick it here once every topic shows mastered. It closes the
        unit as a passed mastery check does, and its lessons enter review.
      </Typography>
      {owner &&
        (record ? (
          <Alert
            severity="success"
            sx={{ mt: 1 }}
            action={
              <Button
                color="inherit"
                size="small"
                onClick={() =>
                  change((current) => setExternal(current, key, null))
                }
              >
                Clear
              </Button>
            }
          >
            Mastered {longDate(localDateOf(record.at))}, by hand.
          </Alert>
        ) : (
          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={1.5}
            sx={{ mt: 1 }}
          >
            <TextField
              size="small"
              type="date"
              label="Mastered on"
              value={date}
              onChange={(event) => setDate(event.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <Button
              variant="contained"
              size="small"
              disabled={!date}
              onClick={() =>
                change((current) =>
                  setExternal(current, key, { at: noonOf(date) }),
                )
              }
            >
              Every topic is mastered
            </Button>
          </Stack>
        ))}
    </Box>
  );
}
