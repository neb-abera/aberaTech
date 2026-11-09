import Alert from "@mui/material/Alert";
import Chip from "@mui/material/Chip";
import Link from "@mui/material/Link";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { Link as RouterLink } from "react-router";
import { clock } from "../core/dates";
import { type Band, mix, type Pick, tableSource } from "../core/saad";
import type { PoolAnswer } from "../hooks/usePool";

const bandLabel = Object.fromEntries(
  mix.map((band) => [band.band, band.label]),
) as Record<Band, string>;

/**
 * This week's ten from Mostafa Saad's pool, by his mix around the current
 * band. Each says its level, its importance, and where it came from.
 */
export default function PoolPanel({
  pool,
  picks,
  level,
  band,
}: {
  pool: PoolAnswer;
  picks: Pick[];
  level: number;
  band: { rating: number; source: string };
}) {
  return (
    <Paper
      variant="outlined"
      component="section"
      aria-label="Problem pool"
      sx={{ p: 2 }}
    >
      <Stack spacing={1}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          Problem pool
        </Typography>
        {pool.status === "loading" || pool.status === "idle" ? (
          <Typography variant="body2">Reading Mostafa Saad's sheet.</Typography>
        ) : !pool.ok ? (
          <Alert severity="warning">
            Mostafa Saad's sheet could not be read
            {pool.error ? `: ${pool.error}` : ""}. The syllabus's own problems
            above still count.
          </Alert>
        ) : (
          <>
            <Typography variant="body2">
              Ten for this week by his mix: 1 below your level, 2 at it, 6
              slightly above, and 1 far above. Think 60 to 120 minutes before
              the editorial, then log it.
            </Typography>
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              Your level: {level.toFixed(2)}, from {band.rating} ({band.source}
              ). {tableSource} Pool:{" "}
              <Link href={pool.source} target="_blank" rel="noopener">
                Mostafa Saad's supervision pool
              </Link>
              , read {pool.readAt ? clock(pool.readAt) : ""},{" "}
              {pool.problems.length} of {pool.rows} problems at importance P5 to
              P3. Solved problems are left out. The order holds for the week.
            </Typography>
            <Stack component="ol" spacing={0.75} sx={{ pl: 2.5, m: 0 }}>
              {picks.map((pick) => (
                <Typography
                  component="li"
                  variant="body2"
                  key={pick.problem.code}
                >
                  {pick.problem.url ? (
                    <Link
                      href={pick.problem.url}
                      target="_blank"
                      rel="noopener"
                    >
                      {pick.problem.code}
                    </Link>
                  ) : (
                    pick.problem.code
                  )}{" "}
                  <Chip size="small" label={`level ${pick.problem.level}`} />{" "}
                  <Chip
                    size="small"
                    variant="outlined"
                    label={`P${pick.problem.importance}`}
                  />{" "}
                  <Typography
                    component="span"
                    variant="caption"
                    sx={{ color: "text.secondary" }}
                  >
                    {bandLabel[pick.band]}
                    {pick.nearest ? ", the nearest level left" : ""}
                    {pick.problem.editorial
                      ? ". His sheet notes an editorial."
                      : "."}{" "}
                  </Typography>
                  <Link
                    component={RouterLink}
                    to={`/training?view=log&problem=${encodeURIComponent(pick.problem.code)}`}
                  >
                    Log it
                  </Link>
                </Typography>
              ))}
            </Stack>
          </>
        )}
      </Stack>
    </Paper>
  );
}
