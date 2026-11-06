import Accordion from "@mui/material/Accordion";
import AccordionDetails from "@mui/material/AccordionDetails";
import AccordionSummary from "@mui/material/AccordionSummary";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Link from "@mui/material/Link";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import * as React from "react";
import { Link as RouterLink, useSearchParams } from "react-router";
import Blocks from "../../playbook/components/Blocks";
import SignInToSee from "../../progress/components/SignInToSee";
import {
  fetchPlan,
  fetchPlanPage,
  type PlanFile,
  type PlanNote,
  type PlanPageResult,
  type PlanResult,
  type PlanWorkout,
  planFileUrl,
} from "../core/api";
import {
  currentWeek,
  formatDate,
  formatMinutes,
  type PlanWeek,
  planWeeks,
  todayIso,
} from "../core/weeks";

const busy = "Notion is busy. Reload in a minute.";
const failed = "Notion did not answer. Reload to try again.";

/**
 * The owner's training plan, read live from Notion, one week at a time:
 * each day's notes and workouts with their full text, the week's totals,
 * and every file. The week is in the address, so a reload or Back keeps
 * it. Edits are made in Notion and show on the next load.
 */
export default function TrainingPlanPanel({ today }: { today?: string }) {
  const [result, setResult] = React.useState<
    PlanResult | { status: "loading" }
  >({ status: "loading" });
  const [params, setParams] = useSearchParams();

  React.useEffect(() => {
    let alive = true;
    void fetchPlan().then((next) => {
      if (alive) setResult(next);
    });
    return () => {
      alive = false;
    };
  }, []);

  const plan = result.status === "owner" ? result.plan : null;
  const weeks = React.useMemo(() => (plan ? planWeeks(plan) : []), [plan]);

  if (result.status === "loading") {
    return <CircularProgress size={28} aria-label="Loading" />;
  }
  if (result.status === "visitor") {
    return (
      <SignInToSee
        message="This page is the owner's. Sign in to see it."
        returnUrl="/training-plan"
      />
    );
  }
  if (result.status === "unconfigured") {
    return (
      <Alert severity="info">
        {result.message ??
          "The training plan is not connected. Set Notion__TrainingPlanPageId on the container app."}
      </Alert>
    );
  }
  if (result.status === "error") {
    return <Alert severity="error">{result.busy ? busy : failed}</Alert>;
  }

  const { plan: p } = result;
  const openPage = params.get("page");
  const pages = p.pages ?? [];
  if (openPage) {
    return (
      <LinkedPage
        id={openPage}
        title={pages.find((page) => page.id === openPage)?.title}
      />
    );
  }
  if (weeks.length === 0) {
    return (
      <Alert severity="info">
        The plan has no rows with a Week and a Date yet.
      </Alert>
    );
  }

  const asked = Number(params.get("week"));
  const shown =
    weeks.find((w) => w.week === asked) ??
    weeks.find((w) => w.week === currentWeek(weeks, today ?? todayIso())) ??
    weeks[0];
  const at = weeks.indexOf(shown);
  const go = (week: number) => setParams({ week: String(week) });

  return (
    <Stack spacing={4}>
      <Box>
        <Typography variant="h3" component="h2" sx={{ mb: 1 }}>
          {p.title}
        </Typography>
        <Typography sx={{ color: "text.secondary" }}>
          {weeks.length} weeks, {p.workouts.length} workouts and{" "}
          {p.notes.length} notes. Week {weeks[0].week} starts{" "}
          {formatDate(weeks[0].start)}.
        </Typography>
        {p.url && (
          <Button
            variant="outlined"
            href={p.url}
            target="_blank"
            rel="noopener"
            sx={{ mt: 2 }}
          >
            Edit in Notion
          </Button>
        )}
      </Box>

      {pages.length > 0 && (
        <Box component="nav" aria-label="Related pages">
          <Typography variant="h4" component="h3" sx={{ mb: 1 }}>
            Related pages
          </Typography>
          <Box component="ul" sx={{ pl: 2.5, my: 0 }}>
            {pages.map((page) => (
              <li key={page.id}>
                <Link
                  component={RouterLink}
                  to={`/training-plan?page=${page.id}`}
                >
                  {page.title || "Untitled"}
                </Link>
              </li>
            ))}
          </Box>
        </Box>
      )}

      <Box component="nav" aria-label="Weeks">
        <Stack direction="row" spacing={1} sx={{ mb: 1.5 }}>
          <Button
            onClick={() => go(weeks[at - 1].week)}
            disabled={at === 0}
            variant="outlined"
          >
            Previous week
          </Button>
          <Button
            onClick={() => go(weeks[at + 1].week)}
            disabled={at === weeks.length - 1}
            variant="outlined"
          >
            Next week
          </Button>
        </Stack>
        <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.75 }}>
          {weeks.map((w) => (
            <Chip
              key={w.week}
              label={w.week}
              component="button"
              onClick={() => go(w.week)}
              aria-label={`Week ${w.week}`}
              aria-current={w.week === shown.week ? "true" : undefined}
              color={w.week === shown.week ? "primary" : "default"}
              variant={w.week === shown.week ? "filled" : "outlined"}
              size="small"
              sx={{ minWidth: 36 }}
            />
          ))}
        </Box>
      </Box>

      <WeekView week={shown} total={weeks.length} />

      {(p.sections.length > 0 || p.files.length > 0) && (
        <Box component="section" aria-label="About the plan">
          <Typography variant="h4" component="h3" sx={{ mb: 1 }}>
            About the plan
          </Typography>
          {p.files.length > 0 && <Files files={p.files} />}
          {p.sections.map((section) => (
            <Accordion
              key={section.heading}
              disableGutters
              slotProps={{ transition: { unmountOnExit: true } }}
            >
              <AccordionSummary>
                <Typography sx={{ fontWeight: 600 }}>
                  {section.heading || "Plan"}
                </Typography>
              </AccordionSummary>
              <AccordionDetails>
                {section.paragraphs.map((text) => (
                  <Typography key={text} sx={{ mb: 1 }}>
                    <Linked text={text} />
                  </Typography>
                ))}
              </AccordionDetails>
            </Accordion>
          ))}
        </Box>
      )}
    </Stack>
  );
}

/** A page the plan links, read live and drawn the way /playbook draws a page. */
function LinkedPage({ id, title }: { id: string; title?: string }) {
  const [page, setPage] = React.useState<
    PlanPageResult | { status: "loading" }
  >({ status: "loading" });

  React.useEffect(() => {
    const controller = new AbortController();
    setPage({ status: "loading" });
    void fetchPlanPage(id, controller.signal).then((next) => {
      if (!controller.signal.aborted) setPage(next);
    });
    return () => controller.abort();
  }, [id]);

  return (
    <Box component="article" aria-label="Linked page">
      <Link
        component={RouterLink}
        to="/training-plan"
        sx={{ mb: 2, display: "inline-block" }}
      >
        Back to the plan
      </Link>
      {page.status === "loading" && (
        <CircularProgress size={24} aria-label="Loading page" />
      )}
      {page.status === "visitor" && (
        <Alert severity="warning">
          The session expired. Reload and sign in again.
        </Alert>
      )}
      {page.status === "missing" && (
        <Alert severity="info">The plan does not link that page.</Alert>
      )}
      {page.status === "error" && (
        <Alert severity="error">{page.busy ? busy : failed}</Alert>
      )}
      {page.status === "owner" && (
        <>
          <Typography variant="h3" component="h2" sx={{ mb: 2 }}>
            {page.page.title || title}
          </Typography>
          <Blocks blocks={page.page.blocks} />
        </>
      )}
    </Box>
  );
}

function WeekView({ week, total }: { week: PlanWeek; total: number }) {
  return (
    <Box component="section" aria-label={`Week ${week.week}`}>
      <Typography variant="h4" component="h3">
        Week {week.week} of {total}
      </Typography>
      <Typography sx={{ color: "text.secondary", mb: 1 }}>
        {[
          week.block,
          week.label,
          week.start &&
            `${formatDate(week.start, false)} to ${formatDate(week.end)}`,
        ]
          .filter(Boolean)
          .join(". ")}
      </Typography>
      <Typography sx={{ mb: 2 }}>
        {formatMinutes(week.minutes) || "No planned time"}
        {week.tss > 0 && `, ${week.tss} TSS`}
        {week.bySport.length > 0 &&
          `. ${week.bySport.map((s) => `${s.sport} ${formatMinutes(s.minutes)}`).join(", ")}`}
        .
      </Typography>
      {week.days.length === 0 && (
        <Typography sx={{ color: "text.secondary" }}>
          Nothing is planned this week.
        </Typography>
      )}
      <Stack spacing={3}>
        {week.days.map((day) => (
          <Box
            key={day.date}
            component="section"
            aria-label={`${day.weekday} ${formatDate(day.date)}`}
          >
            <Typography variant="h5" component="h4" sx={{ mb: 1 }}>
              {day.weekday} {formatDate(day.date)}
            </Typography>
            {day.notes.map((note) => (
              <NoteItem key={note.id} note={note} />
            ))}
            {day.workouts.map((workout) => (
              <WorkoutItem key={workout.id} workout={workout} />
            ))}
          </Box>
        ))}
      </Stack>
    </Box>
  );
}

function NoteItem({ note }: { note: PlanNote }) {
  return (
    <Accordion
      disableGutters
      variant="outlined"
      slotProps={{ transition: { unmountOnExit: true } }}
    >
      <AccordionSummary>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
          <Chip label="Note" size="small" />
          <Typography sx={{ fontWeight: 600 }}>{note.title}</Typography>
          {note.files.length > 0 && (
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              {note.files.length === 1
                ? "1 file"
                : `${note.files.length} files`}
            </Typography>
          )}
          {note.changed && <Chip label="Changed" size="small" color="info" />}
        </Stack>
      </AccordionSummary>
      <AccordionDetails>
        <Text text={note.description} />
        {note.files.length > 0 && <Files files={note.files} />}
        {note.url && <NotionLink url={note.url} />}
      </AccordionDetails>
    </Accordion>
  );
}

function WorkoutItem({ workout }: { workout: PlanWorkout }) {
  const facts = [
    workout.sport,
    formatMinutes(workout.minutes),
    workout.kilometers ? `${workout.kilometers} km` : "",
    workout.tss !== undefined ? `${workout.tss} TSS` : "",
  ].filter(Boolean);
  return (
    <Accordion
      disableGutters
      variant="outlined"
      slotProps={{ transition: { unmountOnExit: true } }}
    >
      <AccordionSummary>
        <Stack
          direction="row"
          spacing={1}
          sx={{ alignItems: "baseline", flexWrap: "wrap" }}
        >
          <Typography sx={{ fontWeight: 600 }}>{workout.title}</Typography>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {facts.join(", ")}
          </Typography>
          {workout.changed && (
            <Chip label="Changed" size="small" color="info" />
          )}
        </Stack>
      </AccordionSummary>
      <AccordionDetails>
        {workout.description && <Text text={workout.description} />}
        {workout.comments && (
          <>
            <Typography variant="subtitle2" component="h5" sx={{ mt: 2 }}>
              Pre-activity comments
            </Typography>
            <Text text={workout.comments} />
          </>
        )}
        {workout.structure && (
          <>
            <Typography variant="subtitle2" component="h5" sx={{ mt: 2 }}>
              Structure
            </Typography>
            <Text text={workout.structure} />
          </>
        )}
        {workout.intensityFactor !== undefined && (
          <Typography variant="body2" sx={{ mt: 1, color: "text.secondary" }}>
            Planned IF {workout.intensityFactor}
          </Typography>
        )}
        {workout.url && <NotionLink url={workout.url} />}
      </AccordionDetails>
    </Accordion>
  );
}

function Files({ files }: { files: PlanFile[] }) {
  return (
    <Box component="ul" sx={{ pl: 2.5, my: 1 }}>
      {files.map((file) => (
        <li key={file.id}>
          <Link href={planFileUrl(file.id)} download={file.name}>
            Download {file.name}
          </Link>
        </li>
      ))}
    </Box>
  );
}

function NotionLink({ url }: { url: string }) {
  return (
    <Typography variant="body2" sx={{ mt: 1.5 }}>
      <Link href={url} target="_blank" rel="noopener">
        Edit in Notion
      </Link>
    </Typography>
  );
}

/** Text the way Notion holds it: one paragraph per line, links live. */
function Text({ text }: { text: string }) {
  return (
    <>
      {text
        .split("\n")
        .map((line, index) => ({ line, key: `${index}:${line}` }))
        .filter(({ line }) => line.trim().length > 0)
        .map(({ line, key }) => (
          <Typography key={key} sx={{ mb: 0.75 }}>
            <Linked text={line} />
          </Typography>
        ))}
    </>
  );
}

const urlPattern = /(https?:\/\/[^\s)\]]+)/g;

/** A line with every https address as a link that opens in a new tab. */
export function Linked({ text }: { text: string }) {
  const parts = text.split(urlPattern);
  return (
    <>
      {parts.map((part, index) =>
        index % 2 === 1 ? (
          <Link
            // biome-ignore lint/suspicious/noArrayIndexKey: the split's order is the text's
            key={index}
            href={part}
            target="_blank"
            rel="noopener noreferrer"
          >
            {part}
          </Link>
        ) : (
          part
        ),
      )}
    </>
  );
}
