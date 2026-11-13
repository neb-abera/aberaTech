import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlineOutlined";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import IconButton from "@mui/material/IconButton";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import type { PlaceInfo } from "../core/api";
import { addPlace, removePlace } from "../core/efforts";
import { FEET_PER_METER, formatAltitude } from "./raceDistances";

/**
 * Where you lived and trained, by date. Each race and test is scored at the
 * altitude of where it was run, and the times shown are for where you are
 * now. Until 2026-11-13 one home altitude scored everything, so a 2-mile run
 * at Fort Bliss read as a sea-level run.
 */
export default function PlacesCard({
  places,
  onChanged,
}: {
  places: PlaceInfo[];
  onChanged: () => void;
}) {
  const [place, setPlace] = React.useState("");
  const [altitudeFt, setAltitudeFt] = React.useState("");
  const [since, setSince] = React.useState("");
  const [status, setStatus] = React.useState<{
    ok: boolean;
    text: string;
  } | null>(null);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    const feet = Number(altitudeFt);
    if (
      altitudeFt.trim() === "" ||
      !Number.isFinite(feet) ||
      feet < -1500 ||
      feet > 16000
    ) {
      setStatus({ ok: false, text: "Altitude is -1,500 to 16,000 ft." });
      return;
    }
    if (place.trim() === "") {
      setStatus({ ok: false, text: "Name the place." });
      return;
    }
    try {
      await addPlace({
        since: since === "" ? null : since,
        place: place.trim(),
        altitudeMeters: feet / FEET_PER_METER,
      });
      setStatus({
        ok: true,
        text: `Added ${place.trim()} ${since === "" ? "for everything before your first dated place" : `from ${since}`}.`,
      });
      setPlace("");
      setAltitudeFt("");
      setSince("");
      onChanged();
    } catch (error) {
      setStatus({ ok: false, text: (error as Error).message });
    }
  };

  const remove = async (entry: PlaceInfo) => {
    try {
      await removePlace(entry.id);
      setStatus({ ok: true, text: `Removed ${entry.place}.` });
      onChanged();
    } catch (error) {
      setStatus({ ok: false, text: (error as Error).message });
    }
  };

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">Where you trained</Typography>
        <Typography variant="body2" sx={{ mb: 1 }}>
          Thin air slows an all-out run, about 1% at 1,200 m. Each race and test
          is scored at the altitude of where you were that day, and the times
          shown are for where you are now. Add a place each time you move, with
          the day you arrived. A place with no date covers everything before the
          first dated one.
        </Typography>

        <Stack spacing={0.5} sx={{ mb: 2 }}>
          {places.map((entry) => (
            <Stack
              key={entry.id}
              direction="row"
              spacing={1}
              sx={{ alignItems: "center" }}
            >
              <Typography variant="body2" sx={{ minWidth: 110 }}>
                {entry.since ? `from ${entry.since}` : "before that"}
              </Typography>
              <Typography variant="body2" sx={{ minWidth: 180 }}>
                {entry.place}
              </Typography>
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                {formatAltitude(entry.altitudeMeters)}
              </Typography>
              {entry.current && (
                <Chip size="small" color="primary" label="now" />
              )}
              <IconButton
                size="small"
                aria-label={`Remove ${entry.place}${entry.since ? ` from ${entry.since}` : ""}`}
                onClick={() => void remove(entry)}
              >
                <DeleteOutlineIcon fontSize="small" />
              </IconButton>
            </Stack>
          ))}
        </Stack>

        <form onSubmit={submit}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
            <TextField
              size="small"
              label="Place"
              value={place}
              onChange={(e) => setPlace(e.target.value)}
            />
            <TextField
              size="small"
              label="Altitude (ft)"
              value={altitudeFt}
              onChange={(e) => setAltitudeFt(e.target.value)}
              slotProps={{ htmlInput: { inputMode: "numeric" } }}
            />
            <TextField
              size="small"
              label="Arrived (blank for before everything)"
              type="date"
              value={since}
              onChange={(e) => setSince(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
              sx={{ minWidth: 240 }}
            />
            <Button type="submit" variant="outlined">
              Add place
            </Button>
          </Stack>
        </form>

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
