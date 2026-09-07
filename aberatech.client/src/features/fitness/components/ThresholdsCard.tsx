import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import {
  type FieldTest,
  type SettingsDto,
  saveSettings,
  type ThresholdSuggestion,
} from "../core/api";
import { formatPace, formatSeconds } from "../core/format";

/** "6:40" → 400; blank → null; nonsense → NaN. */
function parsePace(text: string): number | null {
  const trimmed = text.trim();
  if (trimmed === "") return null;
  const match = /^(\d{1,2}):(\d{2})$/.exec(trimmed);
  if (!match) return Number.NaN;
  return Number(match[1]) * 60 + Number(match[2]);
}

/** The full update the endpoint wants, from the settings as they stand. */
function updateFrom(settings: SettingsDto) {
  return {
    referenceHr: settings.referenceHr,
    ltSecondsPerKm: settings.ltSecondsPerKm,
    ltHr: settings.ltHr,
    planMinutesPerWeek: settings.planMinutesPerWeek,
    startVdot: settings.startVdot,
    vdotMeasuredOn: settings.vdotMeasuredOn,
    birthYear: settings.birthYear,
    female: settings.female,
    availableHoursPerWeek: settings.availableHoursPerWeek,
    sustainedWeeklyHours: settings.sustainedWeeklyHours,
    pastPeakDistanceMeters: settings.pastPeakDistanceMeters,
    pastPeakSeconds: settings.pastPeakSeconds,
    pastPeakYear: settings.pastPeakYear,
    pastPeakWeightKg: settings.pastPeakWeightKg,
    goalWeightKg: settings.goalWeightKg,
    homeAltitudeMeters: settings.homeAltitudeMeters,
    anchorDistanceMeters: null,
    anchorSeconds: null,
    selectionDate: settings.selectionDate,
  };
}

/**
 * The three numbers every heart-rate judgement runs on — aerobic-threshold
 * HR, lactate-threshold HR, lactate-threshold pace — with the field tests the
 * log contains and what they say those numbers should be. Typed once and
 * forgotten is how thresholds go stale; a test in the log is how they stay
 * honest.
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
  const [aetHr, setAetHr] = React.useState(String(settings.referenceHr));
  const [ltHr, setLtHr] = React.useState(
    settings.ltHr === null ? "" : String(settings.ltHr),
  );
  const [ltPace, setLtPace] = React.useState(
    settings.ltSecondsPerKm === null
      ? ""
      : formatSeconds(settings.ltSecondsPerKm),
  );
  const [status, setStatus] = React.useState<{
    ok: boolean;
    text: string;
  } | null>(null);

  React.useEffect(() => {
    setAetHr(String(settings.referenceHr));
    setLtHr(settings.ltHr === null ? "" : String(settings.ltHr));
    setLtPace(
      settings.ltSecondsPerKm === null
        ? ""
        : formatSeconds(settings.ltSecondsPerKm),
    );
  }, [settings.referenceHr, settings.ltHr, settings.ltSecondsPerKm]);

  const save = async (next: {
    referenceHr: number;
    ltHr: number | null;
    ltSecondsPerKm: number | null;
  }) => {
    try {
      await saveSettings({ ...updateFrom(settings), ...next });
      setStatus({
        ok: true,
        text: "Thresholds saved — every zone moves with them.",
      });
      onSaved();
    } catch (error) {
      setStatus({ ok: false, text: (error as Error).message });
    }
  };

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    const aet = Number(aetHr);
    if (!Number.isInteger(aet) || aet < 80 || aet > 220) {
      setStatus({ ok: false, text: "AeT heart rate must be 80-220 bpm." });
      return;
    }
    const lt = ltHr.trim() === "" ? null : Number(ltHr);
    if (lt !== null && (!Number.isInteger(lt) || lt <= aet || lt > 220)) {
      setStatus({
        ok: false,
        text: "LT heart rate must sit above the AeT and below 220 bpm.",
      });
      return;
    }
    const pace = parsePace(ltPace);
    if (pace !== null && Number.isNaN(pace)) {
      setStatus({ ok: false, text: "LT pace is m:ss per km." });
      return;
    }
    await save({ referenceHr: aet, ltHr: lt, ltSecondsPerKm: pace });
  };

  const apply = async () => {
    if (suggestion === null) return;
    await save({
      referenceHr: suggestion.aetHr ?? settings.referenceHr,
      ltHr: suggestion.ltHr ?? settings.ltHr,
      ltSecondsPerKm: suggestion.ltSecPerKm ?? settings.ltSecondsPerKm,
    });
  };

  const recent = tests.slice(-6).reverse();

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">Thresholds</Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          Aerobic threshold (AeT) is the heart rate the trend normalizes to and
          the ceiling of easy; lactate threshold (LT) is where threshold ends.
          Treadmill sessions are placed by these, since the belt&apos;s pace
          cannot be trusted. Set them by test: an hour at AeT with the drift
          read from laps, a 30-minute time trial for LT.
        </Typography>

        <form onSubmit={submit}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={2}
            sx={{ alignItems: { sm: "flex-start" } }}
          >
            <TextField
              label="AeT heart rate (bpm)"
              value={aetHr}
              onChange={(e) => setAetHr(e.target.value)}
              slotProps={{ htmlInput: { inputMode: "numeric" } }}
              size="small"
            />
            <TextField
              label="LT heart rate (bpm)"
              value={ltHr}
              onChange={(e) => setLtHr(e.target.value)}
              slotProps={{ htmlInput: { inputMode: "numeric" } }}
              helperText={
                settings.ltHr === null
                  ? `Assumed ${Math.round(settings.referenceHr * 1.1)} until measured`
                  : undefined
              }
              size="small"
            />
            <TextField
              label="LT pace (m:ss per km)"
              value={ltPace}
              onChange={(e) => setLtPace(e.target.value)}
              placeholder="5:40"
              size="small"
            />
            <Button type="submit" variant="outlined">
              Save thresholds
            </Button>
          </Stack>
        </form>

        {suggestion !== null && (
          <Alert
            severity="info"
            sx={{ mt: 2 }}
            action={
              <Button color="inherit" size="small" onClick={() => void apply()}>
                Apply
              </Button>
            }
          >
            <strong>From your latest test:</strong>{" "}
            {[
              suggestion.aetHr !== null && `AeT ${suggestion.aetHr} bpm`,
              suggestion.ltHr !== null && `LT ${suggestion.ltHr} bpm`,
              suggestion.ltSecPerKm !== null &&
                `LT pace ${formatPace(suggestion.ltSecPerKm)}`,
            ]
              .filter(Boolean)
              .join(", ")}
            . {suggestion.reason}
          </Alert>
        )}

        {recent.length > 0 && (
          <Stack spacing={1} sx={{ mt: 2 }}>
            <Typography variant="subtitle2">
              Field tests found in the log
            </Typography>
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
