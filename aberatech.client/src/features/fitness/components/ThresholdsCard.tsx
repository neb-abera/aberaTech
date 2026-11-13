import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlineOutlined";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import Divider from "@mui/material/Divider";
import IconButton from "@mui/material/IconButton";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import {
  deleteThreshold,
  type FieldTest,
  fetchThresholds,
  logThreshold,
  type SettingsDto,
  type ThresholdEntry,
  type ThresholdKind,
  type ThresholdSuggestion,
} from "../core/api";
import { formatPace } from "../core/format";

/** "6:40" → 400; blank → null; nonsense → NaN. */
export function parsePace(text: string): number | null {
  const trimmed = text.trim();
  if (trimmed === "") return null;
  const match = /^(\d{1,2}):(\d{2})$/.exec(trimmed);
  if (!match) return Number.NaN;
  return Number(match[1]) * 60 + Number(match[2]);
}

/** Today in the reader's own zone, as yyyy-mm-dd. */
function today(): string {
  return new Date().toLocaleDateString("en-CA");
}

interface KindInfo {
  kind: ThresholdKind;
  name: string;
  field: string;
  test: string;
  testKind: FieldTest["kind"];
  suggested: (s: ThresholdSuggestion) => number | null;
  show: (value: number) => string;
  /** Text in the box → the value to log, or an error to show. */
  parse: (text: string) => number | string;
}

const heartRate = (text: string): number | string => {
  const value = Number(text.trim());
  return Number.isInteger(value) && value >= 80 && value <= 220
    ? value
    : "A heart rate is a whole number from 80 to 220 bpm.";
};

export const KINDS: KindInfo[] = [
  {
    kind: "aet-hr",
    name: "AeT heart rate",
    field: "AeT heart rate (bpm)",
    test: "HR drift test",
    testKind: "aet",
    suggested: (s) => s.aetHr,
    show: (v) => `${Math.round(v)} bpm`,
    parse: heartRate,
  },
  {
    kind: "lt-hr",
    name: "LT heart rate",
    field: "LT heart rate (bpm)",
    test: "5 mile time trial",
    testKind: "threshold",
    suggested: (s) => s.ltHr,
    show: (v) => `${Math.round(v)} bpm`,
    parse: heartRate,
  },
  {
    kind: "lt-pace",
    name: "LT pace",
    field: "LT pace (m:ss per km)",
    test: "5 mile time trial",
    testKind: "threshold",
    suggested: (s) => s.ltSecPerKm,
    show: (v) => formatPace(v),
    parse: (text) => {
      const pace = parsePace(text);
      return pace === null || Number.isNaN(pace) || pace < 120 || pace > 900
        ? "LT pace is m:ss per km, from 2:00 to 15:00."
        : pace;
    },
  },
];

/**
 * The three numbers every heart-rate judgment runs on, each logged on its
 * own with the date of its test, and each kept. The newest test date of each
 * kind is the one in use. Until 2026-11-13 one form saved all three at once:
 * a blank box cleared a value, and an old value was gone for good. Neb asked
 * for the redesign and for instructions that day.
 */
export default function ThresholdsCard({
  settings,
  tests,
  suggestion,
  onSaved,
}: {
  settings: SettingsDto;
  tests: FieldTest[];
  suggestion: ThresholdSuggestion | null;
  onSaved: () => void;
}) {
  const [entries, setEntries] = React.useState<ThresholdEntry[] | null>(null);
  const [status, setStatus] = React.useState<{
    ok: boolean;
    text: string;
  } | null>(null);

  const reload = React.useCallback(() => {
    fetchThresholds()
      .then((list) => setEntries(Array.isArray(list) ? list : []))
      .catch(() => setEntries([]));
  }, []);

  React.useEffect(reload, [reload]);

  const log = async (
    info: KindInfo,
    value: number,
    measuredOn: string,
    source?: string,
    activityId?: string,
  ) => {
    try {
      const { current } = await logThreshold({
        kind: info.kind,
        value,
        measuredOn,
        source,
        activityId,
      });
      setStatus({
        ok: true,
        text: current
          ? `${info.name} ${info.show(value)} from ${measuredOn} logged and in use.`
          : `${info.name} ${info.show(value)} from ${measuredOn} logged for the record. A newer test stays in use.`,
      });
      reload();
      onSaved();
      return true;
    } catch (error) {
      setStatus({ ok: false, text: (error as Error).message });
      return false;
    }
  };

  const remove = async (info: KindInfo, entry: ThresholdEntry) => {
    try {
      await deleteThreshold(entry.id);
      setStatus({
        ok: true,
        text: `Removed ${info.name} ${info.show(entry.value)}${entry.measuredOn ? ` from ${entry.measuredOn}` : ""}.`,
      });
      reload();
      onSaved();
    } catch (error) {
      setStatus({ ok: false, text: (error as Error).message });
    }
  };

  const recent = tests.slice(-6).reverse();

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">Thresholds</Typography>
        <Typography variant="body2" sx={{ mb: 1 }}>
          Your aerobic threshold (AeT) is the top of zone 2. Easy runs stay at
          or below it, the Aerobic base trend is scaled to it, and the Goals tab
          measures your AeT pace with it. Your lactate threshold (LT) is where
          threshold work ends. A training run never changes them.
        </Typography>

        <Typography variant="subtitle2" component="h3">
          How to log a threshold
        </Typography>
        <Typography component="ol" variant="body2" sx={{ pl: 2.5, mt: 0.5 }}>
          <li>
            Run the test. The HR drift test sets your AeT heart rate. The 5 mile
            time trial sets your LT heart rate and LT pace. The Plan tab has the
            steps and the names to give each run.
          </li>
          <li>
            Once the run syncs, the value it gives shows under its threshold
            below. Press <strong>Log it</strong>.
          </li>
          <li>
            To log a value by hand, type it and the test date under its
            threshold and press <strong>Log</strong>. Each threshold is logged
            on its own. Logging one never changes the other two.
          </li>
          <li>
            Nothing is overwritten. The newest test date is in use, and every
            earlier value stays in the list. To take back a wrong entry, press
            the bin beside it. The one before it comes back into use.
          </li>
        </Typography>

        {KINDS.map((info) => (
          <KindSection
            key={info.kind}
            info={info}
            settings={settings}
            entries={entries?.filter((e) => e.kind === info.kind) ?? null}
            suggestion={suggestion}
            tests={tests}
            onLog={log}
            onRemove={remove}
          />
        ))}

        {recent.length > 0 && (
          <Stack spacing={1} sx={{ mt: 2 }}>
            <Divider />
            <Typography variant="subtitle2">Tests found in the log</Typography>
            {recent.map((test) => (
              <Stack
                key={test.activityId}
                direction="row"
                spacing={1}
                sx={{ alignItems: "baseline", flexWrap: "wrap" }}
              >
                <Chip
                  size="small"
                  label={test.kind === "aet" ? "AeT" : "LT"}
                  color={test.kind === "aet" ? "primary" : "secondary"}
                />
                <Typography variant="body2">
                  {test.date}: {formatPace(test.secPerKm)} at {test.averageHr}{" "}
                  bpm
                  {test.driftPercent !== null &&
                    `, drift ${(test.driftPercent * 100).toFixed(1)}%`}
                  {test.indoor && " (treadmill)"}
                </Typography>
                <Typography variant="caption" sx={{ color: "text.secondary" }}>
                  {test.evidence}
                </Typography>
              </Stack>
            ))}
          </Stack>
        )}

        {status && (
          <Alert
            severity={status.ok ? "success" : "error"}
            onClose={() => setStatus(null)}
            sx={{ mt: 2 }}
          >
            {status.text}
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}

function KindSection({
  info,
  settings,
  entries,
  suggestion,
  tests,
  onLog,
  onRemove,
}: {
  info: KindInfo;
  settings: SettingsDto;
  entries: ThresholdEntry[] | null;
  suggestion: ThresholdSuggestion | null;
  tests: FieldTest[];
  onLog: (
    info: KindInfo,
    value: number,
    measuredOn: string,
    source?: string,
    activityId?: string,
  ) => Promise<boolean>;
  onRemove: (info: KindInfo, entry: ThresholdEntry) => Promise<void>;
}) {
  const [text, setText] = React.useState("");
  const [date, setDate] = React.useState(today);
  const [error, setError] = React.useState<string | null>(null);

  const current = entries?.find((e) => e.current) ?? null;
  const suggested = suggestion ? info.suggested(suggestion) : null;
  const matching = tests.filter((t) => t.kind === info.testKind);
  const test = matching.length > 0 ? matching[matching.length - 1] : null;
  // Offered only when it differs from the value in use.
  const offer =
    suggested !== null &&
    test !== null &&
    (current === null || Math.abs(current.value - suggested) >= 0.5)
      ? { value: suggested, test }
      : null;

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    const value = info.parse(text);
    if (typeof value === "string") {
      setError(value);
      return;
    }
    if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) {
      setError("Give the date of the test.");
      return;
    }
    setError(null);
    if (await onLog(info, value, date)) setText("");
  };

  return (
    <Box sx={{ mt: 2 }}>
      <Divider sx={{ mb: 1.5 }} />
      <Typography variant="subtitle1" component="h3">
        {info.name}
      </Typography>
      <Typography variant="body2" sx={{ mb: 1 }}>
        {current ? (
          <>
            In use: <strong>{info.show(current.value)}</strong>,{" "}
            {current.measuredOn
              ? `from ${current.source ?? "a test"} on ${current.measuredOn}`
              : "carried over from before the log was kept, with no test date"}
            .
          </>
        ) : entries === null ? (
          "Loading…"
        ) : info.kind === "lt-hr" ? (
          `Not measured. Assumed ${Math.round(settings.referenceHr * 1.1)} bpm, your AeT plus 10%, until a ${info.test} sets it.`
        ) : (
          `Not measured. Set by the ${info.test}.`
        )}
      </Typography>

      {offer && (
        <Alert
          severity="info"
          sx={{ mb: 1 }}
          action={
            <Button
              color="inherit"
              size="small"
              onClick={() =>
                void onLog(
                  info,
                  offer.value,
                  offer.test.date,
                  `${info.test}`,
                  offer.test.activityId,
                )
              }
            >
              Log it
            </Button>
          }
        >
          Your {info.test} on {offer.test.date} gives{" "}
          <strong>{info.show(offer.value)}</strong>.
        </Alert>
      )}

      <form onSubmit={submit}>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={1.5}
          sx={{ alignItems: { sm: "flex-start" } }}
        >
          <TextField
            label={info.field}
            value={text}
            onChange={(e) => setText(e.target.value)}
            placeholder={info.kind === "lt-pace" ? "5:40" : undefined}
            slotProps={{
              htmlInput: {
                inputMode: info.kind === "lt-pace" ? "text" : "numeric",
              },
            }}
            size="small"
          />
          <TextField
            label={`${info.name} test date`}
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
            size="small"
          />
          <Button
            type="submit"
            variant="outlined"
            aria-label={`Log ${info.name}`}
          >
            Log
          </Button>
        </Stack>
      </form>
      {error && (
        <Typography variant="caption" sx={{ color: "error.main" }}>
          {error}
        </Typography>
      )}

      {entries !== null && entries.length > 0 && (
        <Stack spacing={0.5} sx={{ mt: 1 }}>
          {entries.map((entry) => (
            <Stack
              key={entry.id}
              direction="row"
              spacing={1}
              sx={{ alignItems: "center" }}
            >
              <Typography variant="body2" sx={{ minWidth: 88 }}>
                {info.show(entry.value)}
              </Typography>
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                {entry.measuredOn ?? "no date"} · {entry.source ?? "entered"}
              </Typography>
              {entry.current && (
                <Chip size="small" color="primary" label="in use" />
              )}
              <IconButton
                size="small"
                aria-label={`Remove ${info.name} ${info.show(entry.value)}${entry.measuredOn ? ` from ${entry.measuredOn}` : ""}`}
                onClick={() => void onRemove(info, entry)}
              >
                <DeleteOutlineIcon fontSize="small" />
              </IconButton>
            </Stack>
          ))}
        </Stack>
      )}
    </Box>
  );
}
