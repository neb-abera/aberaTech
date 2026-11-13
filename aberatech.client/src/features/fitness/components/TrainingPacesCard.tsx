import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { Summary } from "../core/api";
import { formatPace } from "../core/format";

/** The pace each kind of session should be run at: a prescription, so it lives on the Plan tab. */
export default function TrainingPacesCard({ summary }: { summary: Summary }) {
  if (summary.trainingPaces.length === 0) return null;
  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">
          Today&apos;s training paces (Daniels)
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          Prescriptions, not predictions: the pace each kind of session should
          happen at, from your current VDOT{" "}
          {summary.settings.startVdot.toFixed(0)}. Bands, not laps to hit to the
          second.
        </Typography>
        <Stack spacing={0.5}>
          {summary.trainingPaces.map((pace) => (
            <Typography key={pace.zone} variant="body2">
              <strong>
                {pace.zone} · {pace.name}:
              </strong>{" "}
              {formatPace(pace.slowSecPerKm)} to {formatPace(pace.fastSecPerKm)}{" "}
              <Typography
                component="span"
                variant="caption"
                sx={{ color: "text.secondary" }}
              >
                {pace.purpose}
              </Typography>
            </Typography>
          ))}
        </Stack>
      </CardContent>
    </Card>
  );
}
