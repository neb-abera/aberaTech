import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlineOutlined";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import IconButton from "@mui/material/IconButton";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import type { EffortInfo, Summary } from "../core/api";
import { enterRace, removeRace } from "../core/efforts";
import { formatSeconds } from "../core/format";
import {
  distanceLabel,
  FEET_PER_METER,
  formatAltitude,
  MILE,
  parseRaceTime,
  RACE_DISTANCES,
} from "./raceDistances";

const KIND_LABEL: Record<EffortInfo["kind"], string> = {
  Recording: "Recording",
  Entered: "Entered",
  Aft2Mile: "AFT",
};

/**
 * Every race, time trial, and AFT 2-mile the model reads, scored at the
 * altitude it was run, with the one the projections start from marked.
 * Any distance from a mile to the marathon counts (Neb, 2026-11-13).
 */
export default function EffortsCard({
  summary,
  onChanged,
}: {
  summary: Summary;
  onChanged: () => void;
}) {
  const efforts = summary.efforts ?? [];
  const skipped = summary.skippedTests ?? [];
  const [distance, setDistance] = React.useState(5 * MILE);
  const [time, setTime] = React.useState("");
  const [date, setDate] = React.useState("");
  const [name, setName] = React.useState("");
  const [altitudeFt, setAltitudeFt] = React.useState("");
  const [status, setStatus] = React.useState<{
    ok: boolean;
    text: string;
  } | null>(null);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    const seconds = parseRaceTime(time);
    if (seconds === null) {
      setStatus({ ok: false, text: "Time looks like 34:10 or 1:32:10." });
      return;
    }
    if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) {
      setStatus({ ok: false, text: "Give the date it was run." });
      return;
    }
    const feet = altitudeFt.trim() === "" ? null : Number(altitudeFt);
    if (
      feet !== null &&
      (!Number.isFinite(feet) || feet < -1500 || feet > 16000)
    ) {
      setStatus({
        ok: false,
        text: "Altitude is -1,500 to 16,000 ft, or blank.",
      });
      return;
    }
    const label = distanceLabel(distance);
    try {
      await enterRace({
        date,
        name: name.trim() === "" ? `${label} race` : name.trim(),
        distanceMeters: distance,
        seconds,
        altitudeMeters: feet === null ? null : feet / FEET_PER_METER,
      });
      setStatus({
        ok: true,
        text: `Entered a ${label} in ${time} on ${date}.`,
      });
      setTime("");
      setDate("");
      setName("");
      setAltitudeFt("");
      onChanged();
    } catch (error) {
      setStatus({ ok: false, text: (error as Error).message });
    }
  };

  const remove = async (effort: EffortInfo) => {
    if (!effort.id) return;
    try {
      await removeRace(effort.id);
      setStatus({
        ok: true,
        text: `Removed ${effort.name} of ${effort.date}.`,
      });
      onChanged();
    } catch (error) {
      setStatus({ ok: false, text: (error as Error).message });
    }
  };

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">Races and time trials</Typography>
        <Typography variant="body2" sx={{ mb: 1 }}>
          Every projection starts from your newest all-out effort, scored at the
          altitude it was run. Any distance from 1 mile to the marathon works.
          The model puts them all on one scale, VDOT.
        </Typography>
        <Typography variant="subtitle2" component="h3">
          How to add one
        </Typography>
        <Typography component="ol" variant="body2" sx={{ pl: 2.5, mt: 0.5 }}>
          <li>
            Name the run for its distance and what it was, such as{" "}
            <strong>5 mile time trial</strong>, <strong>10K race</strong>, or{" "}
            <strong>Half marathon race</strong>. It shows here once it syncs.
          </li>
          <li>
            Save the warm-up as its own run. A recording more than 3% longer
            than its distance is read only when it holds the fastest stretch of
            that length.
          </li>
          <li>
            A race with no recording goes in the form below. Leave the altitude
            blank to use your place log for that day.
          </li>
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          An AFT 2-mile is run after four other events, so it reads slower than
          a fresh 2-mile by an amount nobody has measured. It only ever sets a
          floor. It replaces your newest race when it is newer and shows more
          fitness, and otherwise it is listed for comparing AFTs.
        </Typography>

        {efforts.length > 0 && (
          <Box sx={{ overflowX: "auto", mb: 2 }}>
            <Table size="small" sx={{ minWidth: 640 }}>
              <TableHead>
                <TableRow>
                  <TableCell>Date</TableCell>
                  <TableCell>Test</TableCell>
                  <TableCell align="right">Distance</TableCell>
                  <TableCell align="right">Time</TableCell>
                  <TableCell>Altitude</TableCell>
                  <TableCell align="right">VDOT</TableCell>
                  <TableCell />
                </TableRow>
              </TableHead>
              <TableBody>
                {efforts.map((effort) => (
                  <TableRow key={`${effort.kind}-${effort.id}-${effort.date}`}>
                    <TableCell>{effort.date}</TableCell>
                    <TableCell>
                      <Stack
                        direction="row"
                        spacing={1}
                        sx={{ alignItems: "center" }}
                      >
                        <span>{effort.name}</span>
                        <Chip size="small" label={KIND_LABEL[effort.kind]} />
                        {effort.isAnchor && (
                          <Chip size="small" color="primary" label="anchor" />
                        )}
                      </Stack>
                      {effort.note && (
                        <Typography
                          variant="caption"
                          sx={{ color: "text.secondary" }}
                        >
                          {effort.note}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell align="right">
                      {distanceLabel(effort.distanceMeters)}
                    </TableCell>
                    <TableCell align="right">
                      {formatSeconds(effort.seconds)}
                    </TableCell>
                    <TableCell>
                      {formatAltitude(effort.altitudeMeters)}
                    </TableCell>
                    <TableCell align="right">
                      {effort.kind === "Aft2Mile" ? "at least " : ""}
                      {effort.vdot.toFixed(1)}
                    </TableCell>
                    <TableCell>
                      {effort.removable && (
                        <IconButton
                          size="small"
                          aria-label={`Remove ${effort.name} of ${effort.date}`}
                          onClick={() => void remove(effort)}
                        >
                          <DeleteOutlineIcon fontSize="small" />
                        </IconButton>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        )}

        {skipped.length > 0 && (
          <Alert severity="warning" sx={{ mb: 2 }}>
            {skipped.map((s) => (
              <div key={s.activityId}>
                <strong>
                  {s.name} on {s.date}
                </strong>{" "}
                was not read: {s.reason}.
              </div>
            ))}
          </Alert>
        )}

        <Typography variant="subtitle2" component="h3" sx={{ mb: 1 }}>
          Enter a race
        </Typography>
        <form onSubmit={submit}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={1.5}
            sx={{ flexWrap: "wrap" }}
          >
            <TextField
              select
              size="small"
              label="Distance"
              value={distance}
              onChange={(e) => setDistance(Number(e.target.value))}
              sx={{ minWidth: 150 }}
            >
              {RACE_DISTANCES.map((d) => (
                <MenuItem key={d.label} value={d.meters}>
                  {d.label}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              size="small"
              label="Time (m:ss or h:mm:ss)"
              value={time}
              onChange={(e) => setTime(e.target.value)}
            />
            <TextField
              size="small"
              label="Date run"
              type="date"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              size="small"
              label="Name (optional)"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
            <TextField
              size="small"
              label="Altitude (ft, optional)"
              value={altitudeFt}
              onChange={(e) => setAltitudeFt(e.target.value)}
              slotProps={{ htmlInput: { inputMode: "numeric" } }}
            />
            <Button type="submit" variant="outlined">
              Enter race
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
