import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import { type SettingsDto, saveSettings } from "../core/api";
import { formatSeconds, kgToLb, lbToKg } from "../core/format";

const MILE = 1609.344;

const RACE_DISTANCES: { label: string; meters: number }[] = [
  { label: "1.5 mile", meters: 1.5 * MILE },
  { label: "2 mile", meters: 2 * MILE },
  { label: "5K", meters: 5000 },
  { label: "5 mile", meters: 5 * MILE },
  { label: "10K", meters: 10_000 },
];

const FEET_PER_METER = 3.28084;

function parseRaceTime(value: string): number | null {
  const parts = value.split(":").map(Number);
  if (parts.length !== 2 || parts.some((p) => !Number.isFinite(p))) {
    return null;
  }
  const seconds = parts[0] * 60 + parts[1];
  return seconds > 0 ? seconds : null;
}

/**
 * The athlete's history and context — the inputs that make the predictions
 * personal rather than generic: a recent race as the anchor, the lifetime
 * best (retraining runs faster below it), birth year (the peak age-adjusts),
 * and home altitude (thin air slows every aerobic time).
 */
export default function ProfileCard({
  settings,
  onSaved,
}: {
  settings: SettingsDto;
  onSaved: () => void;
}) {
  const [birthYear, setBirthYear] = React.useState(
    settings.birthYear === null ? "" : String(settings.birthYear),
  );
  const [altitudeFt, setAltitudeFt] = React.useState(
    String(Math.round(settings.homeAltitudeMeters * FEET_PER_METER)),
  );
  const [anchorDistance, setAnchorDistance] = React.useState(2 * MILE);
  const [anchorTime, setAnchorTime] = React.useState("");
  const [anchorDate, setAnchorDate] = React.useState(
    settings.vdotMeasuredOn ?? "",
  );
  const [peakDistance, setPeakDistance] = React.useState(
    settings.pastPeakDistanceMeters ?? 2 * MILE,
  );
  const [peakTime, setPeakTime] = React.useState(
    settings.pastPeakSeconds === null
      ? ""
      : formatSeconds(settings.pastPeakSeconds),
  );
  const [peakYear, setPeakYear] = React.useState(
    settings.pastPeakYear === null ? "" : String(settings.pastPeakYear),
  );
  const [book, setBook] = React.useState(
    settings.female === null ? "unstated" : settings.female ? "women" : "men",
  );
  const [availableHours, setAvailableHours] = React.useState(
    String(settings.availableHoursPerWeek),
  );
  const [sustainedHours, setSustainedHours] = React.useState(
    settings.sustainedWeeklyHours === null
      ? ""
      : String(settings.sustainedWeeklyHours),
  );
  const [peakWeightLb, setPeakWeightLb] = React.useState(
    settings.pastPeakWeightKg === null
      ? ""
      : String(Math.round(kgToLb(settings.pastPeakWeightKg))),
  );
  const [goalWeightLb, setGoalWeightLb] = React.useState(
    settings.goalWeightKg === null
      ? ""
      : String(Math.round(kgToLb(settings.goalWeightKg))),
  );
  const [status, setStatus] = React.useState<{
    ok: boolean;
    text: string;
  } | null>(null);

  const save = async () => {
    const anchorSeconds =
      anchorTime.trim() === "" ? null : parseRaceTime(anchorTime);
    if (anchorTime.trim() !== "" && anchorSeconds === null) {
      setStatus({ ok: false, text: "Anchor time must look like 16:49." });
      return;
    }
    const peakSeconds = peakTime.trim() === "" ? null : parseRaceTime(peakTime);
    if (peakTime.trim() !== "" && peakSeconds === null) {
      setStatus({
        ok: false,
        text: "Lifetime-best time must look like 12:45.",
      });
      return;
    }
    for (const [label, value] of [
      ["Lifetime-best weight", peakWeightLb],
      ["Goal race weight", goalWeightLb],
    ] as const) {
      const pounds = Number(value);
      if (
        value.trim() !== "" &&
        (!Number.isFinite(pounds) || pounds < 66 || pounds > 550)
      ) {
        setStatus({ ok: false, text: `${label} must be 66-550 lb.` });
        return;
      }
    }

    const sustained = Number(sustainedHours);
    if (
      sustainedHours.trim() !== "" &&
      (!Number.isFinite(sustained) || sustained < 0 || sustained > 40)
    ) {
      setStatus({
        ok: false,
        text: "Biggest sustained week must be 0-40 hours.",
      });
      return;
    }

    const hours = Number(availableHours);
    if (!Number.isFinite(hours) || hours < 0 || hours > 40) {
      setStatus({ ok: false, text: "Available hours must be 0-40 a week." });
      return;
    }
    const altitude = Number(altitudeFt);
    if (!Number.isFinite(altitude) || altitude < 0 || altitude > 15000) {
      setStatus({ ok: false, text: "Altitude must be 0-15000 ft." });
      return;
    }

    try {
      await saveSettings({
        referenceHr: settings.referenceHr,
        ltSecondsPerKm: settings.ltSecondsPerKm,
        planMinutesPerWeek: settings.planMinutesPerWeek,
        startVdot: settings.startVdot,
        vdotMeasuredOn: anchorDate.trim() === "" ? null : anchorDate,
        birthYear: birthYear.trim() === "" ? null : Number(birthYear),
        female: book === "unstated" ? null : book === "women",
        availableHoursPerWeek: Number(availableHours),
        sustainedWeeklyHours:
          sustainedHours.trim() === "" ? null : Number(sustainedHours),
        pastPeakWeightKg:
          peakWeightLb.trim() === "" ? null : lbToKg(Number(peakWeightLb)),
        goalWeightKg:
          goalWeightLb.trim() === "" ? null : lbToKg(Number(goalWeightLb)),
        pastPeakDistanceMeters: peakSeconds === null ? null : peakDistance,
        pastPeakSeconds: peakSeconds,
        pastPeakYear: peakYear.trim() === "" ? null : Number(peakYear),
        homeAltitudeMeters: altitude / FEET_PER_METER,
        anchorDistanceMeters: anchorSeconds === null ? null : anchorDistance,
        anchorSeconds,
      });
      setStatus({ ok: true, text: "Profile saved — predictions now use it." });
      onSaved();
    } catch {
      setStatus({ ok: false, text: "Could not save the profile." });
    }
  };

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6" sx={{ mb: 0.5 }}>
          Athlete profile
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          What makes the predictions yours. The lifetime best matters most:
          fitness you have held before comes back at multiples of the beginner
          rate, so a trained past moves every date.
        </Typography>

        <Stack spacing={2}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              select
              label="Grade targets against"
              value={book}
              onChange={(event) => setBook(event.target.value)}
              helperText="Which record book a target is measured against"
              sx={{ minWidth: 220 }}
            >
              <MenuItem value="unstated">
                Unstated (open men's — most permissive)
              </MenuItem>
              <MenuItem value="men">Men's record book</MenuItem>
              <MenuItem value="women">Women's record book</MenuItem>
            </TextField>
            <TextField
              label="Biggest week you have held (h)"
              type="number"
              value={sustainedHours}
              onChange={(event) => setSustainedHours(event.target.value)}
              helperText="Held for a month without breaking down — sets the recovery budget ceilings are planned against"
              slotProps={{ htmlInput: { min: 0, max: 40, step: 0.5 } }}
              sx={{ width: 260 }}
            />
            <TextField
              label="Hours you can train"
              type="number"
              value={availableHours}
              onChange={(event) => setAvailableHours(event.target.value)}
              helperText="Weekly running hours you can realistically commit"
              slotProps={{ htmlInput: { min: 0, max: 40, step: 0.5 } }}
              sx={{ width: 200 }}
            />
          </Stack>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              select
              label="Anchor race"
              value={anchorDistance}
              onChange={(event) =>
                setAnchorDistance(Number(event.target.value))
              }
              sx={{ minWidth: 140 }}
            >
              {RACE_DISTANCES.map((d) => (
                <MenuItem key={d.label} value={d.meters}>
                  {d.label}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              label="Time (m:ss)"
              value={anchorTime}
              onChange={(event) => setAnchorTime(event.target.value)}
              helperText={`Current anchor: VDOT ${settings.startVdot.toFixed(1)}`}
              sx={{ width: 160 }}
            />
            <TextField
              label="Race date"
              type="date"
              value={anchorDate}
              onChange={(event) => setAnchorDate(event.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
          </Stack>

          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              select
              label="Lifetime best"
              value={peakDistance}
              onChange={(event) => setPeakDistance(Number(event.target.value))}
              sx={{ minWidth: 140 }}
            >
              {RACE_DISTANCES.map((d) => (
                <MenuItem key={d.label} value={d.meters}>
                  {d.label}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              label="Time (m:ss)"
              value={peakTime}
              onChange={(event) => setPeakTime(event.target.value)}
              helperText="Your best ever at this distance"
              sx={{ width: 160 }}
            />
            <TextField
              label="Year it was run"
              value={peakYear}
              onChange={(event) => setPeakYear(event.target.value)}
              sx={{ width: 140 }}
            />
            <TextField
              label="Weight then (lb)"
              type="number"
              value={peakWeightLb}
              onChange={(event) => setPeakWeightLb(event.target.value)}
              helperText="What that best was run at — without it the peak cannot follow race weight"
              slotProps={{ htmlInput: { min: 66, max: 550, step: 1 } }}
              sx={{ width: 190 }}
            />
          </Stack>

          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              label="Goal race weight (lb)"
              type="number"
              value={goalWeightLb}
              onChange={(event) => setGoalWeightLb(event.target.value)}
              helperText="Where the projection opens, instead of today's weight"
              slotProps={{ htmlInput: { min: 66, max: 550, step: 1 } }}
              sx={{ width: 220 }}
            />
          </Stack>

          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              label="Birth year"
              value={birthYear}
              onChange={(event) => setBirthYear(event.target.value)}
              helperText="Age-adjusts the reclaimable peak"
              sx={{ width: 160 }}
            />
            <TextField
              label="Home altitude (ft)"
              value={altitudeFt}
              onChange={(event) => setAltitudeFt(event.target.value)}
              helperText="El Paso ≈ 3,900 ft; ~1% on race times"
              sx={{ width: 200 }}
            />
            <Button
              variant="contained"
              onClick={save}
              sx={{ alignSelf: "center" }}
            >
              Save profile
            </Button>
          </Stack>
        </Stack>

        {status && (
          <Alert severity={status.ok ? "success" : "error"} sx={{ mt: 2 }}>
            {status.text}
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}
