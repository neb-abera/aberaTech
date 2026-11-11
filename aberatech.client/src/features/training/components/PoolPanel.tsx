import Alert from "@mui/material/Alert";
import Chip from "@mui/material/Chip";
import Link from "@mui/material/Link";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { Link as RouterLink } from "react-router";
import { clock } from "../core/dates";
import {
  atcoderScaleSource,
  type Band,
  mix,
  type Pick,
  type poolStart,
  tableSource,
} from "../core/saad";
import type { BandAnswer } from "../hooks/useBand";
import type { PoolAnswer } from "../hooks/usePool";

const bandLabel = Object.fromEntries(
  mix.map((band) => [band.band, band.label]),
) as Record<Band, string>;

/**
 * This week's ten from Mostafa Saad's pool, by his mix around the current
 * band. Each says its level, its importance, and where it came from. A
 * slot his pool cannot fill takes a Codeforces problem in the band, and
 * says so. Those alternate between Codeforces and AtCoder, and each
 * names its judge.
 */
export default function PoolPanel({
  pool,
  picks,
  level,
  band,
  bandList,
  start,
}: {
  pool: PoolAnswer;
  picks: Pick[];
  level: number;
  band: { rating: number; source: string };
  bandList: BandAnswer;
  start: ReturnType<typeof poolStart>;
}) {
  const fromSaad = picks.filter((pick) => pick.source === "saad").length;
  const fromBand = picks.length - fromSaad;
  const fromCodeforces = picks.filter(
    (pick) => pick.source === "codeforces",
  ).length;
  const fromAtCoder = fromBand - fromCodeforces;
  const missing = 10 - picks.length;
  const range = `${band.rating} to ${band.rating + 200}`;
  const atcoder = bandList.atcoder;
  const atcoderRange = atcoder ? `${atcoder.from} to ${atcoder.to}` : "";
  const judges = [
    fromCodeforces > 0 &&
      `${fromCodeforces} Codeforces ${fromCodeforces === 1 ? "problem" : "problems"} rated ${range}`,
    fromAtCoder > 0 &&
      `${fromAtCoder} AtCoder ${fromAtCoder === 1 ? "problem" : "problems"} at difficulty ${atcoderRange}`,
  ]
    .filter(Boolean)
    .join(" and ");
  const unread = [
    bandList.status === "error" || (bandList.status === "ready" && !bandList.ok)
      ? "the Codeforces problemset"
      : null,
    bandList.status === "error" || (atcoder && !atcoder.ok)
      ? "AtCoder Problems"
      : null,
  ].filter((name): name is string => name !== null);
  const opens =
    start && start.rating > band.rating
      ? ` His pool starts near Div. 2 ${start.letter}, at level ${start.level}. It opens to you at a Codeforces rating of about ${start.rating}.`
      : "";
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
            {fromBand > 0 && (
              <Alert severity="info" aria-label="Below his pool">
                {fromSaad === 0
                  ? `None of Mostafa Saad's pool fits your level yet.${opens} Until then, all ${fromBand} are from your band: ${judges}, with tags hidden.`
                  : `His pool has too few problems at your level, so ${fromBand} of the ten are from your band: ${judges}, with tags hidden.${opens}`}
              </Alert>
            )}
            {missing > 0 && (
              <Alert severity="warning">
                {bandList.status === "loading" || bandList.status === "idle"
                  ? `Reading problems in your band of ${range} for the other ${missing}.`
                  : unread.length > 0
                    ? `${unread.join(" and ")} could not be read, so ${missing} of the ten are missing.${opens}`
                    : `Your band has too few unsolved problems, so ${missing} of the ten are missing.${opens}`}
              </Alert>
            )}
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              Your level: {level.toFixed(2)}, from {band.rating} ({band.source}
              ). {tableSource} Pool:{" "}
              <Link href={pool.source} target="_blank" rel="noopener">
                Mostafa Saad's supervision pool
              </Link>
              , read {pool.readAt ? clock(pool.readAt) : ""},{" "}
              {pool.problems.length} of {pool.rows} problems at importance P5 to
              P3. Solved problems are left out. The order holds for the week.
              {fromAtCoder > 0 ? ` ${atcoderScaleSource}` : ""}
              {atcoder && !atcoder.ok && missing === 0
                ? ` AtCoder Problems could not be read${atcoder.error ? `: ${atcoder.error}` : ""}, so Codeforces fills the band.`
                : ""}
            </Typography>
            <Stack component="ol" spacing={0.75} sx={{ pl: 2.5, m: 0 }}>
              {picks.map((pick) =>
                pick.source === "saad" ? (
                  <Typography
                    component="li"
                    variant="body2"
                    key={pick.problem.code}
                  >
                    <Link
                      href={pick.problem.url ?? undefined}
                      target="_blank"
                      rel="noopener"
                    >
                      {pick.problem.code}
                    </Link>{" "}
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
                      {pick.problem.editorial
                        ? ". His sheet notes an editorial."
                        : "."}{" "}
                    </Typography>
                    <LogIt problem={pick.problem.code} />
                  </Typography>
                ) : pick.source === "atcoder" ? (
                  <Typography
                    component="li"
                    variant="body2"
                    key={pick.problem.id}
                  >
                    <Link
                      href={pick.problem.url}
                      target="_blank"
                      rel="noopener"
                    >
                      AtCoder {pick.problem.contest.toUpperCase()}{" "}
                      {pick.problem.index}
                    </Link>{" "}
                    <Chip
                      size="small"
                      label={`difficulty ${pick.problem.difficulty}`}
                    />{" "}
                    <Typography
                      component="span"
                      variant="caption"
                      sx={{ color: "text.secondary" }}
                    >
                      AtCoder Problems, at difficulty {atcoderRange}: your band
                      on AtCoder's scale. Tags hidden.{" "}
                    </Typography>
                    <LogIt problem={pick.problem.id} />
                  </Typography>
                ) : (
                  <Typography
                    component="li"
                    variant="body2"
                    key={pick.problem.id}
                  >
                    <Link
                      href={pick.problem.url}
                      target="_blank"
                      rel="noopener"
                    >
                      Codeforces {pick.problem.id}
                    </Link>{" "}
                    <Chip size="small" label={`rated ${pick.problem.rating}`} />{" "}
                    <Typography
                      component="span"
                      variant="caption"
                      sx={{ color: "text.secondary" }}
                    >
                      Codeforces problemset, in your band of {range}. Tags
                      hidden.{" "}
                    </Typography>
                    <LogIt problem={pick.problem.id} />
                  </Typography>
                ),
              )}
            </Stack>
          </>
        )}
      </Stack>
    </Paper>
  );
}

function LogIt({ problem }: { problem: string }) {
  return (
    <Link
      component={RouterLink}
      to={`/training?view=log&problem=${encodeURIComponent(problem)}`}
    >
      Log it
    </Link>
  );
}
