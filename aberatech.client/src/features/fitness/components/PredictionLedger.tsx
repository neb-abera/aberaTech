import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import {
  deleteLockedPrediction,
  type LockedPrediction,
  scorePrediction,
} from "../core/api";
import { formatDistance, formatSeconds, parseClock } from "../core/format";

/**
 * What the model said before it knew, and what happened.
 *
 * Everything else on this page is a claim about the future that nothing later
 * checks. This is the only part that can tell you the model is wrong — and the
 * interval matters more than the median: intervals that contain the day about
 * as often as they claim to are a model working; intervals that always contain
 * it are a model not saying much.
 */
export default function PredictionLedger({
  rows,
  onChanged,
}: {
  rows: LockedPrediction[] | null;
  onChanged: () => void;
}) {
  const [actuals, setActuals] = React.useState<Record<string, string>>({});
  const [error, setError] = React.useState<string | null>(null);

  if (rows === null) return null;

  const scored = rows.filter((row) => row.status === "scored");
  const held = scored.filter((row) => row.insideInterval).length;

  const record = async (row: LockedPrediction) => {
    const seconds = parseClock(actuals[row.id] ?? "");
    if (seconds === null) {
      setError("The time should look like 34:00.");
      return;
    }
    await scorePrediction(row.id, seconds);
    setError(null);
    onChanged();
  };

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">What the model said before it knew</Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          Everything else here is a claim about the future that nothing later
          checks. Lock a prediction from the answer above and it will ask you
          for the result when the date arrives.
        </Typography>

        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        {rows.length === 0 ? (
          <Alert severity="info">
            Nothing locked yet. The model has never made a prediction anyone
            later compared to reality, which is the one thing that would tell
            you whether to believe it.
          </Alert>
        ) : (
          <>
            {scored.length > 0 && (
              <Stack
                direction="row"
                spacing={1}
                sx={{ mb: 2, flexWrap: "wrap", rowGap: 1 }}
              >
                <Chip
                  size="small"
                  color={held === scored.length ? "success" : "default"}
                  label={`Intervals held: ${held} of ${scored.length}`}
                />
                <Chip
                  size="small"
                  label={`Median error: ${
                    scored.length === 0
                      ? "—"
                      : `${Math.round(
                          scored.reduce(
                            (t, r) => t + (r.errorSeconds ?? 0),
                            0,
                          ) / scored.length,
                        )}s`
                  }`}
                />
              </Stack>
            )}

            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>For</TableCell>
                  <TableCell>Distance</TableCell>
                  <TableCell align="right">Said</TableCell>
                  <TableCell align="right">Assuming</TableCell>
                  <TableCell align="right">Happened</TableCell>
                  <TableCell />
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.map((row) => (
                  <TableRow key={row.id}>
                    <TableCell>
                      {row.targetDate}
                      <Typography
                        variant="caption"
                        sx={{ display: "block", color: "text.secondary" }}
                      >
                        said {row.madeOn}
                      </Typography>
                    </TableCell>
                    <TableCell>{formatDistance(row.distanceMeters)}</TableCell>
                    <TableCell align="right">
                      {formatSeconds(row.predictedSeconds)}
                      <Typography
                        variant="caption"
                        sx={{ display: "block", color: "text.secondary" }}
                      >
                        {formatSeconds(row.predictedFastSeconds)}–
                        {formatSeconds(row.predictedSlowSeconds)}
                      </Typography>
                    </TableCell>
                    <TableCell align="right">
                      {row.weeklyHours.toFixed(1)} h/wk
                      <Typography
                        variant="caption"
                        sx={{ display: "block", color: "text.secondary" }}
                      >
                        {Math.round(row.compliance * 100)}% kept
                      </Typography>
                    </TableCell>
                    <TableCell align="right">
                      {row.status === "scored" ? (
                        <>
                          <strong>
                            {formatSeconds(row.actualSeconds ?? 0)}
                          </strong>
                          <Typography
                            variant="caption"
                            sx={{
                              display: "block",
                              color: row.insideInterval
                                ? "success.main"
                                : "warning.main",
                            }}
                          >
                            {(row.errorSeconds ?? 0) >= 0 ? "+" : ""}
                            {Math.round(row.errorSeconds ?? 0)}s
                            {row.insideInterval ? " — inside" : " — outside"}
                          </Typography>
                        </>
                      ) : row.status === "due" ? (
                        <Stack
                          direction="row"
                          spacing={1}
                          sx={{ justifyContent: "flex-end" }}
                        >
                          <TextField
                            size="small"
                            placeholder="34:00"
                            value={actuals[row.id] ?? ""}
                            onChange={(event) =>
                              setActuals((a) => ({
                                ...a,
                                [row.id]: event.target.value,
                              }))
                            }
                            sx={{ width: 110 }}
                          />
                          <Button size="small" onClick={() => record(row)}>
                            Record
                          </Button>
                        </Stack>
                      ) : (
                        <Typography
                          variant="caption"
                          sx={{ color: "text.secondary" }}
                        >
                          waiting
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell align="right">
                      <Button
                        size="small"
                        color="inherit"
                        onClick={() =>
                          deleteLockedPrediction(row.id).then(onChanged)
                        }
                      >
                        ×
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </>
        )}
      </CardContent>
    </Card>
  );
}
