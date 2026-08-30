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
import { formatSeconds } from "../core/format";

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
