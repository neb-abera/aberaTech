import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Typography from "@mui/material/Typography";

/**
 * How to run the two threshold tests, on the Plan tab with the rest of what
 * to do. The Log's Thresholds card holds the numbers the tests set.
 */
export default function TestProtocolCard() {
  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="h6">Threshold tests</Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          Your AeT and LT are set only by these tests, named as below so the
          site reads them.
        </Typography>
        <Typography variant="subtitle2" component="h3">
          AeT: Evoke&apos;s heart-rate drift test
        </Typography>
        <Typography component="ol" variant="body2" sx={{ pl: 2.5, mt: 0.5 }}>
          <li>
            Warm up 10 to 15 minutes at nose-breathing effort. Find the pace
            where your heart rate holds within 2 to 3 beats for 3 minutes.
          </li>
          <li>
            Save the warm-up. Start a new run and name it{" "}
            <strong>HR drift test</strong>, so this page reads the test alone.
          </li>
          <li>
            On a treadmill at 2%, run 60 minutes without touching the speed or
            incline. Outdoors, run 30 to 45 minutes on a track or flat loop,
            never an out-and-back. No laps needed: the page splits the recording
            in half itself.
          </li>
          <li>
            The drift between the halves is the verdict. 5% or less: that heart
            rate is at or below your AeT. More: it was above it, so retest 5
            beats lower.
          </li>
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          The plan schedules it in week 3 (16 Nov 2026) and week 13 (25 Jan
          2027), then about every 6 weeks. Until the first test, Evoke&apos;s
          Block 1 runs at MAF, 180 minus your age, within 10 beats below it.
        </Typography>
        <Typography variant="subtitle2" component="h3">
          LT: the 5-mile time trial
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
          Warm up well, run 5 miles at an even all-out pace, and name it{" "}
          <strong>5 mile time trial</strong>. The last 20 minutes set your LT
          heart rate and pace. The plan runs it in the Block 2 test week (10 May
          2027).
        </Typography>
      </CardContent>
    </Card>
  );
}
