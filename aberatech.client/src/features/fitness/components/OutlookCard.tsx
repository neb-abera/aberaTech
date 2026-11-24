import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { AerobicOutlook } from "../core/api";
import { formatSeconds } from "../core/format";

const MILE_KM = 1.609344;

function perMile(secPerKm: number): string {
  return `${formatSeconds(secPerKm * MILE_KM)}/mi`;
}

function day(on: string): string {
  return new Date(`${on}T00:00:00Z`).toLocaleDateString("en-US", {
    weekday: "short",
    day: "numeric",
    month: "short",
    year: "numeric",
    timeZone: "UTC",
  });
}

/**
 * Pace at the AeT heart rate, 4 and 8 weeks ahead: the latest drift test,
 * carried forward at the rate a training study measured. Without a recent
 * test it says what it needs and shows no number.
 */
export default function OutlookCard({
  outlook,
}: {
  outlook: AerobicOutlook | null | undefined;
}) {
  if (!outlook) return null;
  const anchor = outlook.anchor;

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">Next 8 weeks: AeT pace</Typography>
        {anchor ? (
          <Typography
            variant="body2"
            sx={{ mb: 1 }}
            data-testid="outlook-anchor"
          >
            Your drift test of {day(anchor.date)}:{" "}
            <strong>{perMile(anchor.secPerKm)}</strong> on the flat at{" "}
            {anchor.averageHr} bpm
            {anchor.gradePercent > 0
              ? `, run at ${anchor.gradePercent}% and read as the flat pace of the same cost`
              : ""}
            .
          </Typography>
        ) : null}
        {!outlook.ready ? (
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Needs: {outlook.needs}
          </Typography>
        ) : (
          <>
            <Stack spacing={0.5}>
              {outlook.ahead.map((point) => (
                <Typography key={point.weeksAhead} variant="body2">
                  In {point.weeksAhead} weeks:{" "}
                  <strong>{perMile(point.secPerKm)}</strong>{" "}
                  <Typography
                    component="span"
                    variant="caption"
                    sx={{ color: "text.secondary" }}
                  >
                    {perMile(point.highSecPerKm)} to{" "}
                    {perMile(point.lowSecPerKm)}
                  </Typography>
                </Typography>
              ))}
            </Stack>
            <Typography variant="body2" sx={{ color: "text.secondary", mt: 1 }}>
              Modeled. Recreational runners gained 3.2 to 4.0% in speed at the
              first ventilatory threshold in 8 weeks of training (Festa 2019).
              The center is 3.6%, and the range is the study&apos;s two groups.
              Your easy runs do not move this: they were on different
              treadmills, at different lengths and altitudes. Your next drift
              test replaces it.
            </Typography>
          </>
        )}
      </CardContent>
    </Card>
  );
}
