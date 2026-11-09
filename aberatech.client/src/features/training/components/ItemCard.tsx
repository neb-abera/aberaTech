import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Checkbox from "@mui/material/Checkbox";
import Chip from "@mui/material/Chip";
import FormControlLabel from "@mui/material/FormControlLabel";
import Link from "@mui/material/Link";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { useState } from "react";
import { clock, localDateOf, shortDate } from "../core/dates";
import {
  canTick,
  type ItemStatus,
  itemStatus,
  type Solves,
  setMissed,
  setScore,
  type TrainingDocument,
  toggleItem,
} from "../core/progress";
import {
  formatMinutes,
  type Item,
  kindLabel,
  systemOf,
} from "../core/syllabus";
import DrillPanel from "./DrillPanel";

const byLabel: Record<string, string> = {
  manual: "ticked by hand",
  codeforces: "Codeforces API",
  atcoder: "AtCoder Problems API",
  drill: "every test passed on the page",
};

export function statusText(status: ItemStatus): string {
  if (!status.done) return "open";
  const when = status.at
    ? `${shortDate(localDateOf(status.at))} ${clock(status.at)}`
    : "";
  const parts = [
    "done",
    when,
    status.by ? `(${byLabel[status.by]})` : "",
    status.missed ? "with misses" : "",
  ].filter(Boolean);
  return parts.join(" ");
}

function Videos({ item }: { item: Item }) {
  const [open, setOpen] = useState(false);
  if (!item.videos?.length) return null;
  if (!open)
    return (
      <Button size="small" onClick={() => setOpen(true)}>
        Show the {item.videos.length} videos
      </Button>
    );
  return (
    <Stack spacing={2}>
      {item.videos.map((video) => (
        <Box key={video.youtube}>
          <Typography variant="body2" sx={{ mb: 0.5 }}>
            <Link href={video.url} target="_blank" rel="noopener">
              {video.title}
            </Link>{" "}
            ({formatMinutes(Math.ceil(video.seconds / 60))})
          </Typography>
          <Box
            component="iframe"
            title={video.title}
            src={`https://www.youtube-nocookie.com/embed/${video.youtube}`}
            loading="lazy"
            allow="encrypted-media; picture-in-picture"
            allowFullScreen
            sx={{ width: "100%", aspectRatio: "16 / 9", border: 0 }}
          />
        </Box>
      ))}
    </Stack>
  );
}

/**
 * One item: where to go, what to do there, how long, and how it is
 * recorded. The owner ticks it, marks it missed, or enters a score. A
 * visitor reads the same card without any of that.
 */
export default function ItemCard({
  item,
  owner,
  document,
  solves,
  change,
  children,
}: {
  item: Item;
  owner: boolean;
  document: TrainingDocument;
  solves: Solves;
  change: (fn: (current: TrainingDocument) => TrainingDocument) => void;
  /** Extra controls below, the note field among them. */
  children?: React.ReactNode;
}) {
  const system = systemOf(item);
  const status = itemStatus(item, document, solves);
  const tickable = canTick(item, solves);
  const record = document.items[item.id];
  return (
    <Paper
      variant="outlined"
      component="article"
      aria-label={item.title}
      sx={{ p: 2 }}
    >
      <Stack spacing={1}>
        <Stack
          direction="row"
          spacing={1}
          sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 1 }}
        >
          <Chip size="small" label={kindLabel[item.kind]} />
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {system.name} · {formatMinutes(item.minutes)}
            {item.minutesBasis ? ` (${item.minutesBasis})` : ""}
          </Typography>
        </Stack>
        <Typography variant="subtitle1" component="h3" sx={{ fontWeight: 600 }}>
          {item.url ? (
            <Link
              href={item.url}
              target={item.url.startsWith("http") ? "_blank" : undefined}
              rel="noopener"
            >
              {item.title}
            </Link>
          ) : (
            item.title
          )}
        </Typography>
        <Typography variant="body2">{item.what}</Typography>
        {item.problem?.rating && (
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Rated {item.problem.rating} by the Codeforces API, read 9 Nov 2026.
          </Typography>
        )}
        {item.problem?.difficulty !== undefined && (
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Difficulty {item.problem.difficulty} on AtCoder Problems, read 9 Nov
            2026.
          </Typography>
        )}
        {item.links?.map((link) => (
          <Link key={link.url} href={link.url} target="_blank" rel="noopener">
            {link.label}
          </Link>
        ))}
        <Videos item={item} />
        {item.drill && (
          <DrillPanel
            item={item}
            drill={item.drill}
            owner={owner}
            document={document}
            change={change}
          />
        )}
        {owner && (
          <Stack
            direction="row"
            spacing={2}
            sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 1 }}
          >
            {tickable ? (
              <FormControlLabel
                control={
                  <Checkbox
                    checked={record !== undefined}
                    onChange={() =>
                      change((current) =>
                        toggleItem(current, item.id, new Date()),
                      )
                    }
                  />
                }
                label={`Done: ${item.title}`}
                slotProps={{
                  typography: {
                    sx: {
                      position: "absolute",
                      width: 1,
                      height: 1,
                      overflow: "hidden",
                      clip: "rect(0 0 0 0)",
                    },
                  },
                }}
                sx={{ mr: 0 }}
              />
            ) : null}
            <Typography
              variant="body2"
              data-testid="item-status"
              sx={{ color: status.done ? "success.main" : "text.secondary" }}
            >
              {statusText(status)}
              {item.record === "drill" && !status.done
                ? " (done when every test passes)"
                : ""}
              {!tickable && !status.done && item.record !== "drill"
                ? ` (counts when the ${item.record === "atcoder" ? "AtCoder Problems" : "Codeforces"} API sees it solved)`
                : ""}
              {tickable && item.record !== "manual" && item.problem
                ? " (the API did not answer, so it is ticked by hand)"
                : ""}
            </Typography>
            {(item.kind === "do" || item.kind === "code") && (
              <Button
                size="small"
                variant={status.missed ? "contained" : "outlined"}
                color="warning"
                onClick={() =>
                  change((current) =>
                    setMissed(current, item.id, !status.missed, new Date()),
                  )
                }
              >
                {status.missed ? "Missed: clear" : "Mark missed"}
              </Button>
            )}
            {item.score && (
              <TextField
                size="small"
                type="number"
                label="Score out of 150"
                value={record?.score ?? ""}
                onChange={(event) => {
                  const value = event.target.value;
                  const score = value === "" ? null : Number(value);
                  if (score !== null && !(score >= 0 && score <= 150)) return;
                  change((current) =>
                    setScore(current, item.id, score, new Date()),
                  );
                }}
                sx={{ width: 160 }}
              />
            )}
          </Stack>
        )}
        {children}
      </Stack>
    </Paper>
  );
}
