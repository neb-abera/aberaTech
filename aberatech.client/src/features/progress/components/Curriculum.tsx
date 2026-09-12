import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Checkbox from "@mui/material/Checkbox";
import Divider from "@mui/material/Divider";
import FormControlLabel from "@mui/material/FormControlLabel";
import FormGroup from "@mui/material/FormGroup";
import LinearProgress from "@mui/material/LinearProgress";
import Link from "@mui/material/Link";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemText from "@mui/material/ListItemText";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { ReactNode } from "react";
import type { Block, Resource } from "../core/curriculum";
import type { Attempt } from "../core/gates";
import GateLog from "./GateLog";

/**
 * The pieces a plan page is built from, shared by every plan on the site.
 *
 * A plan is one column read top to bottom: the summary of how far along the
 * owner is, then sections, then the blocks each with its checklist, its
 * practice, its gate and its reading. The progress is the owner's, kept on
 * the server behind the owner's sign-in. Signed in, the tasks are
 * checkboxes and the gates take attempts. Anyone else, and the build-time
 * render, gets the same page read-only: the tasks as a list, the gate as
 * its text. Nothing a visitor does is sent anywhere.
 */

/** How far along, and how the plan is scored. Owner only for the numbers. */
export function ProgressSummary({
  owner,
  finished,
  total,
  saving,
  reset,
  children,
}: {
  owner: boolean;
  finished: number;
  total: number;
  saving: boolean;
  reset: () => void;
  /** The paragraphs under the bar: how the plan is scored. */
  children: ReactNode;
}) {
  return (
    <Paper variant="outlined" sx={{ p: 2.5 }}>
      <Stack spacing={1.5}>
        {owner && (
          <>
            <Stack
              direction="row"
              spacing={2}
              sx={{ alignItems: "center", justifyContent: "space-between" }}
            >
              <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                {finished} of {total} done
                {saving && (
                  <Typography
                    component="span"
                    variant="caption"
                    sx={{ color: "text.secondary", ml: 1 }}
                  >
                    saving
                  </Typography>
                )}
              </Typography>
              <Button size="small" variant="text" onClick={reset}>
                Start over
              </Button>
            </Stack>
            <LinearProgress
              variant="determinate"
              value={total === 0 ? 0 : (finished / total) * 100}
              aria-label="Tasks done"
            />
          </>
        )}
        {children}
      </Stack>
    </Paper>
  );
}

/** A note under the bar, or under a section heading. */
export function Note({ children }: { children: ReactNode }) {
  return (
    <Typography variant="body2" sx={{ color: "text.secondary" }}>
      {children}
    </Typography>
  );
}

export function Section({
  title,
  children,
}: {
  title: string;
  children: ReactNode;
}) {
  return (
    <Box component="section">
      <Typography
        variant="h2"
        component="h2"
        sx={{ color: "text.primary", fontSize: "1.5rem", mb: 1.5 }}
      >
        {title}
      </Typography>
      {children}
    </Box>
  );
}

export function BlockSection({
  block,
  index,
  level = "h2",
  owner,
  done,
  toggle,
  attempts,
  addAttempt,
  removeAttempt,
}: {
  block: Block;
  /** One-based, as the heading shows it. */
  index: number;
  /** h2 for a flat plan; h3 under a stage heading. */
  level?: "h2" | "h3";
  owner: boolean;
  done: Set<string>;
  toggle: (id: string) => void;
  attempts: Attempt[];
  addAttempt: (blockId: string, attempt: Attempt) => void;
  removeAttempt: (blockId: string, id: string) => void;
}) {
  return (
    <Box component="section">
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={{ xs: 0.5, sm: 2 }}
        sx={{ alignItems: { sm: "baseline" }, mb: 1 }}
      >
        <Typography
          variant="h2"
          component={level}
          sx={{ color: "text.primary", fontSize: "1.5rem" }}
        >
          {index}. {block.title}
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {block.weeks}
        </Typography>
      </Stack>
      <Typography variant="body1" sx={{ mb: 1.5 }}>
        {block.why}
      </Typography>

      {owner ? (
        <FormGroup>
          {block.tasks.map((task) => (
            <FormControlLabel
              key={task.id}
              control={
                <Checkbox
                  checked={done.has(task.id)}
                  onChange={() => toggle(task.id)}
                />
              }
              label={task.text}
              sx={{
                alignItems: "flex-start",
                mb: 0.5,
                "& .MuiCheckbox-root": { pt: 0.25 },
              }}
            />
          ))}
        </FormGroup>
      ) : (
        <List dense disablePadding aria-label={`Tasks for ${block.id}`}>
          {block.tasks.map((task) => (
            <ListItem
              key={task.id}
              disableGutters
              sx={{ display: "list-item", ml: 2.5 }}
            >
              <ListItemText primary={task.text} />
            </ListItem>
          ))}
        </List>
      )}

      <Typography
        variant="overline"
        component="p"
        sx={{ color: "text.secondary", mt: 1.5, lineHeight: 1.5 }}
      >
        Practice
      </Typography>
      <ResourceList resources={block.practice} />

      <GateLog
        blockId={block.id}
        gate={block.gate}
        attempts={attempts}
        add={addAttempt}
        remove={removeAttempt}
        readOnly={!owner}
      />

      <Typography
        variant="overline"
        component="p"
        sx={{ color: "text.secondary", mt: 1.5, lineHeight: 1.5 }}
      >
        Reading
      </Typography>
      <ResourceList resources={block.resources} />
      <Divider sx={{ mt: 3 }} />
    </Box>
  );
}

export function ResourceList({ resources }: { resources: Resource[] }) {
  return (
    <List dense disablePadding>
      {resources.map((resource) => (
        <ListItem key={resource.url} disableGutters>
          <ListItemText
            primary={
              <Link
                href={resource.url}
                target="_blank"
                rel="noopener noreferrer"
              >
                {resource.title}
              </Link>
            }
            secondary={resource.note}
          />
        </ListItem>
      ))}
    </List>
  );
}
