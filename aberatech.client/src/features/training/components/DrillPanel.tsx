import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { useState } from "react";
import { passDrill, setCode, type TrainingDocument } from "../core/progress";
import type { Drill, Item } from "../core/syllabus";

interface TestResult {
  passed: boolean;
  actual: string;
  error?: string | null;
}

interface RunResult {
  compiled: boolean;
  error?: string | null;
  tests: TestResult[];
}

const mono = { fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace" };

/**
 * A short C++ drill: write it here, run it against its tests. The server
 * sends it to Compiler Explorer, which compiles and runs it, and checks
 * each output. No code runs on this site. Passing every test marks the
 * item done. A visitor sees the task and its tests.
 */
export default function DrillPanel({
  item,
  drill,
  owner,
  document,
  change,
}: {
  item: Item;
  drill: Drill;
  owner: boolean;
  document: TrainingDocument;
  change: (fn: (current: TrainingDocument) => TrainingDocument) => void;
}) {
  const source = document.code[item.id] ?? drill.starter;
  const [running, setRunning] = useState(false);
  const [result, setResult] = useState<RunResult | null>(null);
  const [failure, setFailure] = useState<string | null>(null);

  async function run() {
    setRunning(true);
    setFailure(null);
    setResult(null);
    try {
      const response = await fetch("/api/training/drills/run", {
        method: "POST",
        credentials: "same-origin",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ source, tests: drill.tests }),
      });
      if (response.status === 429) {
        setFailure(
          "Too many runs this minute. Wait a moment and run it again.",
        );
        return;
      }
      if (!response.ok) {
        setFailure(
          response.status === 400
            ? await response.text()
            : `The run failed: ${response.status}.`,
        );
        return;
      }
      const body = (await response.json()) as RunResult;
      setResult(body);
      if (
        body.compiled &&
        body.tests.length === drill.tests.length &&
        body.tests.every((test) => test.passed)
      )
        change((current) => passDrill(current, item.id, new Date()));
    } catch {
      setFailure("The run did not reach the server.");
    } finally {
      setRunning(false);
    }
  }

  const passed = result?.tests.filter((test) => test.passed).length ?? 0;

  return (
    <Stack
      spacing={1.5}
      component="section"
      aria-label={`Drill: ${item.title}`}
    >
      {owner ? (
        <TextField
          label="Your C++"
          value={source}
          multiline
          minRows={10}
          fullWidth
          slotProps={{
            htmlInput: { spellCheck: false, style: mono, maxLength: 16384 },
          }}
          onChange={(event) =>
            change((current) => setCode(current, item.id, event.target.value))
          }
        />
      ) : null}
      {owner && (
        <Box>
          <Button variant="contained" onClick={run} disabled={running}>
            {running ? "Running" : `Run ${drill.tests.length} tests`}
          </Button>
        </Box>
      )}
      {failure && <Alert severity="warning">{failure}</Alert>}
      {result && !result.compiled && (
        <Alert severity="error">
          <Typography variant="body2">No test ran.</Typography>
          <Box component="pre" sx={{ ...mono, whiteSpace: "pre-wrap", m: 0 }}>
            {result.error}
          </Box>
        </Alert>
      )}
      {result?.compiled && (
        <Alert severity={passed === drill.tests.length ? "success" : "warning"}>
          {passed} of {drill.tests.length} tests passed.
        </Alert>
      )}
      <Stack component="ol" spacing={1} sx={{ p: 0, m: 0 }}>
        {drill.tests.map((test, index) => {
          const outcome = result?.tests[index];
          return (
            <Box
              component="li"
              key={test.stdin}
              aria-label={`Test ${index + 1}`}
              sx={{ listStyle: "none" }}
            >
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                Test {index + 1}
                {outcome ? (outcome.passed ? ": passed" : ": failed") : ""}
              </Typography>
              <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                <Box>
                  <Typography variant="caption">Input</Typography>
                  <Box component="pre" sx={{ ...mono, m: 0, fontSize: 13 }}>
                    {test.stdin}
                  </Box>
                </Box>
                <Box>
                  <Typography variant="caption">Expected</Typography>
                  <Box component="pre" sx={{ ...mono, m: 0, fontSize: 13 }}>
                    {test.stdout}
                  </Box>
                </Box>
                {outcome && !outcome.passed && (
                  <Box>
                    <Typography variant="caption">Yours</Typography>
                    <Box component="pre" sx={{ ...mono, m: 0, fontSize: 13 }}>
                      {outcome.error ? `${outcome.error}\n` : ""}
                      {outcome.actual}
                    </Box>
                  </Box>
                )}
              </Stack>
            </Box>
          );
        })}
      </Stack>
    </Stack>
  );
}
