import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import IconButton from "@mui/material/IconButton";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { type FormEvent, useState } from "react";
import { type Attempt, isoDate, newAttemptId, summarize } from "../core/gates";

interface Props {
  blockId: string;
  gate: string;
  attempts: Attempt[];
  add: (blockId: string, attempt: Attempt) => void;
  remove: (blockId: string, id: string) => void;
  /** Fixed for tests; the page uses the wall clock. */
  now?: () => Date;
}

/**
 * A block's gate with its attempt log underneath.
 *
 * Two buttons rather than a pass/fail switch, so an attempt is one click
 * with the result in it. The note is for anything scored elsewhere: a
 * practice exam percentage, a subnetting drill time, where you were heard.
 */
export default function GateLog({
  blockId,
  gate,
  attempts,
  add,
  remove,
  now,
}: Props) {
  const clock = now ?? (() => new Date());
  const [on, setOn] = useState<string>("");
  const [minutes, setMinutes] = useState("");
  const [note, setNote] = useState("");
  const summary = summarize(attempts);

  const log = (passed: boolean) => (event: FormEvent) => {
    event.preventDefault();
    const parsed = Number.parseFloat(minutes);
    add(blockId, {
      id: newAttemptId(),
      on: on || isoDate(clock()),
      passed,
      ...(Number.isFinite(parsed) && parsed > 0 ? { minutes: parsed } : {}),
      ...(note.trim() ? { note: note.trim() } : {}),
    });
    setMinutes("");
    setNote("");
  };

  return (
    <Box
      component="section"
      aria-label={`Gate for ${blockId}`}
      sx={{
        p: 1.5,
        mt: 1.5,
        border: 1,
        borderColor: "primary.main",
        borderRadius: 1,
      }}
    >
      <Typography
        variant="overline"
        component="p"
        sx={{ color: "primary.main", lineHeight: 1.5 }}
      >
        Gate
      </Typography>
      <Typography variant="body2">{gate}</Typography>

      <Typography variant="body2" sx={{ color: "text.secondary", mt: 1 }}>
        {summary.attempts === 0
          ? "No attempts yet."
          : `${summary.passes} of ${summary.attempts} passed.` +
            (summary.best?.minutes
              ? ` Best ${summary.best.minutes} min on ${summary.best.on}.`
              : "") +
            (summary.latest
              ? ` Latest: ${summary.latest.passed ? "pass" : "fail"} on ${summary.latest.on}.`
              : "")}
      </Typography>

      {attempts.length > 0 && (
        <Box component="ul" sx={{ pl: 2, my: 1 }}>
          {attempts.map((attempt) => (
            <Box
              component="li"
              key={attempt.id}
              sx={{ display: "flex", alignItems: "center", gap: 1 }}
            >
              <Typography variant="body2" sx={{ flexGrow: 1 }}>
                {attempt.on}: {attempt.passed ? "pass" : "fail"}
                {attempt.minutes ? `, ${attempt.minutes} min` : ""}
                {attempt.note ? ` (${attempt.note})` : ""}
              </Typography>
              <IconButton
                size="small"
                aria-label={`Remove attempt on ${attempt.on}`}
                onClick={() => remove(blockId, attempt.id)}
              >
                ×
              </IconButton>
            </Box>
          ))}
        </Box>
      )}

      <Stack
        component="form"
        direction={{ xs: "column", sm: "row" }}
        spacing={1}
        useFlexGap
        sx={{ mt: 1.5, alignItems: { sm: "flex-start" }, flexWrap: "wrap" }}
        onSubmit={log(true)}
      >
        <TextField
          size="small"
          type="date"
          label="Date"
          value={on}
          onChange={(event) => setOn(event.target.value)}
          slotProps={{ inputLabel: { shrink: true } }}
        />
        <TextField
          size="small"
          label="Minutes"
          value={minutes}
          onChange={(event) => setMinutes(event.target.value)}
          slotProps={{ htmlInput: { inputMode: "decimal" } }}
          sx={{ width: 110 }}
        />
        <TextField
          size="small"
          label="Note"
          placeholder="score, where heard, what failed"
          value={note}
          onChange={(event) => setNote(event.target.value)}
          sx={{ flexGrow: 1, minWidth: 200 }}
        />
        <Button variant="contained" size="small" type="submit">
          Log a pass
        </Button>
        <Button variant="outlined" size="small" onClick={log(false)}>
          Log a fail
        </Button>
      </Stack>
    </Box>
  );
}
