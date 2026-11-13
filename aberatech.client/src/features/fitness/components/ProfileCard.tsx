import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Divider from "@mui/material/Divider";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import {
  type SettingsDto,
  type SettingsUpdate,
  saveSettings,
} from "../core/api";
import { formatSeconds, kgToLb, lbToKg } from "../core/format";

import { MILE, parseRaceTime, RACE_DISTANCES } from "./raceDistances";

type Status = { ok: boolean; text: string } | null;

/** One part of the profile: its own fields, its own Save, its own result. */
function Part({
  title,
  help,
  status,
  onSave,
  saveLabel = "Save",
  children,
}: {
  title: string;
  help: string;
  status: Status;
  onSave: () => void;
  saveLabel?: string;
  children: React.ReactNode;
}) {
  return (
    <Stack spacing={1.5} component="section" aria-label={title}>
      <div>
        <Typography variant="subtitle1" component="h3">
          {title}
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {help}
        </Typography>
      </div>
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={2}
        sx={{ alignItems: { sm: "flex-start" }, flexWrap: "wrap", rowGap: 2 }}
      >
        {children}
        <Button
          variant="outlined"
          onClick={onSave}
          sx={{ alignSelf: "center" }}
        >
          {saveLabel}
        </Button>
      </Stack>
      {status && (
        <Alert severity={status.ok ? "success" : "error"}>{status.text}</Alert>
      )}
    </Stack>
  );
}

/**
 * The athlete's history and context, in parts that save on their own.
 * Until 2026-11-04 one Save sent every field, so changing the hours appeared
 * to re-submit the anchor race with it (Neb). Since 2026-11-13 races are kept
 * on the Races and time trials card, and altitude on the place log.
 */
export default function ProfileCard({
  settings,
  onSaved,
}: {
  settings: SettingsDto;
  onSaved: () => void;
}) {
  // Training history. The hours a week to train are set on the Goals tab,
  // beside the forecasts that use them.
  const [sustainedHours, setSustainedHours] = React.useState(
    settings.sustainedWeeklyHours === null
      ? ""
      : String(settings.sustainedWeeklyHours),
  );
  const [hoursStatus, setHoursStatus] = React.useState<Status>(null);

  // Lifetime best.
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
  const [peakWeightLb, setPeakWeightLb] = React.useState(
    settings.pastPeakWeightKg === null
      ? ""
      : String(Math.round(kgToLb(settings.pastPeakWeightKg))),
  );
  const [peakStatus, setPeakStatus] = React.useState<Status>(null);

  // About you.
  const [book, setBook] = React.useState(
    settings.female === null ? "unstated" : settings.female ? "women" : "men",
  );
  const [birthYear, setBirthYear] = React.useState(
    settings.birthYear === null ? "" : String(settings.birthYear),
  );
  const [goalWeightLb, setGoalWeightLb] = React.useState(
    settings.goalWeightKg === null
      ? ""
      : String(Math.round(kgToLb(settings.goalWeightKg))),
  );
  const [aboutStatus, setAboutStatus] = React.useState<Status>(null);

  const send = async (
    changes: Partial<SettingsUpdate>,
    said: string,
    set: (s: Status) => void,
  ) => {
    try {
      await saveSettings(changes);
      set({ ok: true, text: said });
      onSaved();
    } catch {
      set({ ok: false, text: "Could not save. Nothing changed." });
    }
  };

  const saveHours = () => {
    const sustained = Number(sustainedHours);
    if (
      sustainedHours.trim() !== "" &&
      (!Number.isFinite(sustained) || sustained < 0 || sustained > 40)
    ) {
      setHoursStatus({ ok: false, text: "Biggest week must be 0-40 hours." });
      return;
    }
    void send(
      { sustainedWeeklyHours: sustainedHours.trim() === "" ? null : sustained },
      "Saved the biggest week. Nothing else changed.",
      setHoursStatus,
    );
  };

  const savePeak = () => {
    const seconds = peakTime.trim() === "" ? null : parseRaceTime(peakTime);
    if (peakTime.trim() !== "" && seconds === null) {
      setPeakStatus({ ok: false, text: "Time must look like 12:45." });
      return;
    }
    const pounds = Number(peakWeightLb);
    if (
      peakWeightLb.trim() !== "" &&
      (!Number.isFinite(pounds) || pounds < 66 || pounds > 550)
    ) {
      setPeakStatus({ ok: false, text: "Weight then must be 66-550 lb." });
      return;
    }
    void send(
      {
        pastPeakDistanceMeters: seconds === null ? null : peakDistance,
        pastPeakSeconds: seconds,
        pastPeakYear: peakYear.trim() === "" ? null : Number(peakYear),
        pastPeakWeightKg: peakWeightLb.trim() === "" ? null : lbToKg(pounds),
      },
      "Saved the lifetime best. Nothing else changed.",
      setPeakStatus,
    );
  };

  const saveAbout = () => {
    const goal = Number(goalWeightLb);
    if (
      goalWeightLb.trim() !== "" &&
      (!Number.isFinite(goal) || goal < 66 || goal > 550)
    ) {
      setAboutStatus({
        ok: false,
        text: "Goal race weight must be 66-550 lb.",
      });
      return;
    }
    void send(
      {
        female: book === "unstated" ? null : book === "women",
        birthYear: birthYear.trim() === "" ? null : Number(birthYear),
        goalWeightKg: goalWeightLb.trim() === "" ? null : lbToKg(goal),
      },
      "Saved. Nothing else changed.",
      setAboutStatus,
    );
  };

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6" sx={{ mb: 0.5 }}>
          Athlete profile
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          Four parts. Each Save sends only its own part.
        </Typography>

        <Stack spacing={2.5} divider={<Divider flexItem />}>
          <Part
            title="Training history"
            help="The hours a week to train are set on the Goals tab, beside its forecasts."
            status={hoursStatus}
            onSave={saveHours}
          >
            <TextField
              label="Biggest week you have held (h)"
              type="number"
              value={sustainedHours}
              onChange={(event) => setSustainedHours(event.target.value)}
              helperText="Held for a month without breaking down"
              slotProps={{ htmlInput: { min: 0, max: 40, step: 0.5 } }}
              sx={{ width: 260 }}
            />
          </Part>

          <Part
            title="Lifetime best"
            help="Fitness you have held before comes back faster, so this moves every date."
            status={peakStatus}
            onSave={savePeak}
          >
            <TextField
              select
              label="Distance"
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
              slotProps={{ htmlInput: { min: 66, max: 550, step: 1 } }}
              sx={{ width: 170 }}
            />
          </Part>

          <Part
            title="About you"
            help="Age, record book, and the weight the projection opens at. Altitude is set by the place log."
            status={aboutStatus}
            onSave={saveAbout}
          >
            <TextField
              select
              label="Grade targets against"
              value={book}
              onChange={(event) => setBook(event.target.value)}
              sx={{ minWidth: 220 }}
            >
              <MenuItem value="unstated">
                Unstated (open men's, most permissive)
              </MenuItem>
              <MenuItem value="men">Men's record book</MenuItem>
              <MenuItem value="women">Women's record book</MenuItem>
            </TextField>
            <TextField
              label="Birth year"
              value={birthYear}
              onChange={(event) => setBirthYear(event.target.value)}
              sx={{ width: 140 }}
            />
            <TextField
              label="Goal race weight (lb)"
              type="number"
              value={goalWeightLb}
              onChange={(event) => setGoalWeightLb(event.target.value)}
              slotProps={{ htmlInput: { min: 66, max: 550, step: 1 } }}
              sx={{ width: 190 }}
            />
          </Part>
        </Stack>
      </CardContent>
    </Card>
  );
}
