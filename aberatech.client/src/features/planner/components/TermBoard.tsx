/**
 * The terms, in order, with the courses sitting in each.
 *
 * A term refuses a drop it cannot legally take: the drop target simply does not
 * call preventDefault, so the browser itself declines. When automatic placement
 * is on, a term that could take the course once its prerequisites were inserted
 * accepts the drop and says so.
 */

import Box from "@mui/material/Box";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { DragEvent } from "react";
import { useState } from "react";
import CourseChip from "./CourseChip";
import { usePlannerContext } from "./PlannerContext";

export default function TermBoard() {
  const ctx = usePlannerContext();
  const { model, drag } = ctx;
  const [over, setOver] = useState<number | null>(null);

  // A term takes a drop when the course is legal there, or when the planner is
  // allowed to insert the prerequisites it is missing. Anything else is refused,
  // rather than accepted and silently ignored.
  const accepts = (i: number) => drag !== null && model.acceptsDrop(drag, i);

  const onDrop = (e: DragEvent<HTMLDivElement>, i: number) => {
    e.preventDefault();
    setOver(null);
    const code = e.dataTransfer.getData("text/plain");
    ctx.setDrag(null);
    if (!code) return;
    ctx.update((m) => {
      m.placeCourse(code, i);
    });
  };

  let lastStage: string | null = null;

  return (
    <Stack spacing={1.25}>
      {model.plan.terms.map((courses, i) => {
        // The first course in the term that belongs to a stage: a preparation
        // course pulled in ahead of it belongs to none, and would hide the heading.
        const stage = courses.reduce<string | null>(
          (found, c) => found ?? model.stageOf(c),
          null,
        );
        const showStage = stage !== null && stage !== lastStage;
        if (stage) lastStage = stage;
        const overCapacity = courses.length > model.perTerm;
        const active = accepts(i);
        const refused = drag !== null && !active;
        return (
          // biome-ignore lint/suspicious/noArrayIndexKey: terms are positional - the index is the term's identity
          <Box key={i}>
            {showStage && (
              <Typography
                variant="overline"
                sx={{
                  color: "text.secondary",
                  letterSpacing: 1,
                  display: "block",
                  mt: 1,
                }}
              >
                {stage}
              </Typography>
            )}
            <Paper
              variant="outlined"
              onDragOver={(e) => {
                if (!active) return; // no preventDefault, so the browser refuses the drop
                e.preventDefault();
                e.dataTransfer.dropEffect = "move";
                setOver(i);
              }}
              onDragLeave={() => {
                setOver((cur) => (cur === i ? null : cur));
              }}
              onDrop={(e) => {
                onDrop(e, i);
              }}
              sx={{
                p: 1.25,
                display: "grid",
                gridTemplateColumns: { xs: "1fr", sm: "150px 1fr" },
                gap: 1,
                alignItems: "start",
                borderColor:
                  over === i
                    ? "primary.main"
                    : active
                      ? "success.main"
                      : "divider",
                borderStyle: refused ? "dashed" : "solid",
                backgroundColor:
                  over === i ? "action.selected" : "background.paper",
                opacity: refused ? 0.55 : 1,
              }}
            >
              <Box>
                <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                  {model.calendar.label(i)}
                </Typography>
                <Typography
                  variant="caption"
                  sx={{
                    color: overCapacity ? "warning.main" : "text.secondary",
                  }}
                >
                  Term {i + 1}
                  {overCapacity
                    ? ` · ${courses.length} of ${model.perTerm}`
                    : ""}
                </Typography>
              </Box>
              <Box
                sx={{
                  display: "flex",
                  flexWrap: "wrap",
                  gap: 0.75,
                  minHeight: 34,
                  minWidth: 0,
                  alignItems: "center",
                }}
              >
                {courses.length ? (
                  courses.map((c) => <CourseChip key={c} code={c} />)
                ) : (
                  <Typography variant="caption" sx={{ color: "text.disabled" }}>
                    Empty. Drag a course here, or use the menu on any chip.
                  </Typography>
                )}
              </Box>
            </Paper>
          </Box>
        );
      })}
    </Stack>
  );
}
