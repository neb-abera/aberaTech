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

/**
 * The forecast that works on a few weeks of training: pace at the AeT heart
 * rate, 4 and 8 weeks ahead. Until enough weeks hold enough easy runs it
 * says what it still needs and shows nothing to be misread.
 */
export default function OutlookCard({
  outlook,
}: {
  outlook: AerobicOutlook | null | undefined;
}) {
  if (!outlook) return null;

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">Next 8 weeks: AeT pace</Typography>
        {!outlook.ready ? (
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Needs more data. The forecast starts once {outlook.weeksNeeded}{" "}
            weeks in the last 12 each hold {outlook.runsPerWeek} or more easy
            runs. {outlook.qualifyingWeeks} of {outlook.weeksNeeded} so far. The
            plan&apos;s 4 to 5 runs a week get there in{" "}
            {outlook.weeksNeeded - outlook.qualifyingWeeks} weeks.
          </Typography>
        ) : (
          <>
            <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
              Pace at {outlook.referenceHr} bpm across {outlook.qualifyingWeeks}{" "}
              weeks of easy runs, carried ahead on a straight line. Modelled
              from training runs, so it never sets a threshold.
            </Typography>
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
                    80% range {perMile(point.lowSecPerKm)} to{" "}
                    {perMile(point.highSecPerKm)}
                  </Typography>
                </Typography>
              ))}
              {outlook.slopeSecPerKmPerWeek !== null ? (
                <Typography variant="caption" sx={{ color: "text.secondary" }}>
                  {outlook.slopeSecPerKmPerWeek <= 0 ? "Faster" : "Slower"} by{" "}
                  {Math.abs(outlook.slopeSecPerKmPerWeek * MILE_KM).toFixed(1)}{" "}
                  s/mi a week.
                </Typography>
              ) : null}
            </Stack>
          </>
        )}
      </CardContent>
    </Card>
  );
}
