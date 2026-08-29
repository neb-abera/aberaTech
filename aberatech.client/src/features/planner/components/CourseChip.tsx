/**
 * One course, as a chip.
 *
 * Colour carries the focus area and nothing else: it sits in a key down the left
 * edge, never in the text, so the label stays readable at every hue. State that
 * matters (blocked, applied to the degree, pulled in automatically, breaking a
 * rule) is spelled out in words and in the border, not in hue alone.
 */

import CloseRounded from "@mui/icons-material/CloseRounded";
import MoreVert from "@mui/icons-material/MoreVert";
import Box from "@mui/material/Box";
import IconButton from "@mui/material/IconButton";
import Menu from "@mui/material/Menu";
import MenuItem from "@mui/material/MenuItem";
import { alpha } from "@mui/material/styles";
import Tooltip from "@mui/material/Tooltip";
import Typography from "@mui/material/Typography";
import type { DragEvent, MouseEvent } from "react";
import { useState } from "react";
import { courseColor } from "../core/areaColors";
import { shortCode } from "../core/format";
import type { PlannerModel } from "../model/PlannerModel";
import { usePlannerContext } from "./PlannerContext";

export interface CourseChipProps {
  code: string;
  /** True when the chip sits in the pool rather than in a term. */
  inPool?: boolean;
  /** True when the course cannot legally sit anywhere yet. */
  blocked?: boolean;
  /** Shown outside the board, where dragging and removing make no sense. */
  readOnly?: boolean;
}

export default function CourseChip({
  code,
  inPool = false,
  blocked = false,
  readOnly = false,
}: CourseChipProps) {
  const ctx = usePlannerContext();
  const { model } = ctx;
  const course = model.get(code);
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null);

  if (!course) return null;

  const rescuable = model.autoOnDrop && model.isRescuable(code);
  const canDrag = !readOnly && (!blocked || rescuable);
  const isApplied = ctx.applied.has(code);
  const isAuto = ctx.auto.has(code);
  const isBroken = ctx.broken.has(code);
  const isNeeded = ctx.needed.has(code);
  const isFocus = model.focus === code;
  const colour = courseColor(course.areas, model.areas, ctx.mode);

  const borderColour = isBroken
    ? "error.main"
    : isFocus
      ? "primary.main"
      : isNeeded
        ? "success.main"
        : "divider";

  const openMenu = (e: MouseEvent<HTMLElement>) => {
    e.stopPropagation();
    setMenuAnchor(e.currentTarget);
  };

  const moveTo = (term: number) => {
    setMenuAnchor(null);
    ctx.update((m) => {
      m.placeCourse(code, term);
    });
  };

  const terms = model.plan.terms;

  return (
    <>
      <Box
        component="span"
        draggable={canDrag}
        data-code={code}
        onDragStart={(e: DragEvent<HTMLSpanElement>) => {
          e.dataTransfer.setData("text/plain", code);
          e.dataTransfer.effectAllowed = "move";
          ctx.setDrag(code);
        }}
        onDragEnd={() => {
          ctx.setDrag(null);
        }}
        onMouseEnter={(e: MouseEvent<HTMLElement>) => {
          ctx.hoverDetail(code, e.currentTarget);
        }}
        onMouseLeave={ctx.releaseDetail}
        onClick={(e: MouseEvent<HTMLElement>) => {
          ctx.pinDetail(code, e.currentTarget);
        }}
        sx={{
          display: "inline-flex",
          alignItems: "center",
          gap: 0.75,
          // A long title must shrink rather than push the buttons off the chip.
          // Without minWidth the nowrap text would overflow and, on a phone,
          // cover the very controls it sits beside.
          maxWidth: "100%",
          minWidth: 0,
          overflow: "hidden",
          px: 1,
          py: 0.5,
          borderRadius: 1.5,
          border: "1px solid",
          borderColor: borderColour,
          borderWidth: isBroken || isFocus || isNeeded ? 2 : 1,
          borderStyle: blocked ? "dashed" : "solid",
          backgroundColor: alpha(colour, ctx.mode === "dark" ? 0.16 : 0.09),
          cursor: canDrag ? "grab" : "pointer",
          opacity: blocked && !rescuable ? 0.62 : 1,
          userSelect: "none",
          "&:hover": { boxShadow: 1 },
        }}
      >
        <Box
          component="span"
          aria-hidden
          sx={{
            width: 4,
            alignSelf: "stretch",
            minHeight: 18,
            borderRadius: 2,
            backgroundColor: colour,
            flexShrink: 0,
          }}
        />
        <Typography
          component="span"
          variant="caption"
          sx={{ color: "text.secondary", fontVariantNumeric: "tabular-nums" }}
        >
          {course.prep ? "prep" : shortCode(course.code)}
        </Typography>
        <Typography
          component="span"
          variant="body2"
          sx={{
            color: "text.primary",
            fontWeight: 500,
            minWidth: 0,
            flexShrink: 1,
            overflow: "hidden",
            textOverflow: "ellipsis",
            whiteSpace: "nowrap",
          }}
        >
          {course.title}
        </Typography>
        {isApplied && <Badge label="deg" title="Applied to the degree" />}
        {isAuto && (
          <Badge
            label="req"
            title="Added because something you chose requires it"
          />
        )}
        {course.level >= 7 && <Badge label="700" title="A 700 level course" />}
        {!readOnly && (
          <Tooltip title="Move to a term">
            <IconButton
              size="small"
              draggable={false}
              aria-label={`Move ${course.title} to a term`}
              onClick={openMenu}
              sx={{ p: 0.25, fontSize: 15, flexShrink: 0 }}
            >
              <MoreVert fontSize="inherit" />
            </IconButton>
          </Tooltip>
        )}
        {!inPool && !readOnly && (
          <Tooltip title="Remove from the plan">
            <IconButton
              size="small"
              draggable={false}
              aria-label={`Remove ${course.title}`}
              onClick={(e) => {
                e.stopPropagation();
                ctx.update((m) => {
                  m.removeCourse(code);
                });
              }}
              sx={{ p: 0.25, fontSize: 15, flexShrink: 0 }}
            >
              <CloseRounded fontSize="inherit" />
            </IconButton>
          </Tooltip>
        )}
      </Box>
      <Menu
        anchorEl={menuAnchor}
        open={menuAnchor !== null}
        onClose={() => {
          setMenuAnchor(null);
        }}
      >
        {terms.map((_, i) => {
          // Computed only while the menu is open: each call walks the placement.
          const { enabled, note } = menuAnchor
            ? describe(model, code, i)
            : { enabled: false, note: "" };
          return (
            <MenuItem
              // biome-ignore lint/suspicious/noArrayIndexKey: terms are positional - the index is the term's identity
              key={i}
              disabled={!enabled}
              onClick={() => {
                moveTo(i);
              }}
            >
              {model.calendar.label(i)}
              {note}
            </MenuItem>
          );
        })}
        {!inPool && (
          <MenuItem
            onClick={() => {
              setMenuAnchor(null);
              ctx.update((m) => {
                m.removeCourse(code);
              });
            }}
          >
            Back to the pool
          </MenuItem>
        )}
      </Menu>
    </>
  );
}

function Badge({ label, title }: { label: string; title: string }) {
  return (
    <Tooltip title={title}>
      <Box
        component="span"
        sx={{
          px: 0.5,
          borderRadius: 0.75,
          fontSize: 10,
          lineHeight: "15px",
          fontWeight: 700,
          letterSpacing: 0.3,
          flexShrink: 0,
          color: "text.secondary",
          border: "1px solid",
          borderColor: "divider",
        }}
      >
        {label}
      </Box>
    </Tooltip>
  );
}

/** One menu row: whether the move is offered, and the plain reason when it is not. */
function describe(
  model: PlannerModel,
  code: string,
  term: number,
): { enabled: boolean; note: string } {
  const { kind, courses } = model.placementNote(code, term);
  const names = courses.map((c) => model.title(c)).join(", ");
  switch (kind) {
    case "ok":
      return { enabled: true, note: "" };
    case "needs":
      return { enabled: true, note: ` · adds ${names} first` };
    case "needsOff":
      return {
        enabled: false,
        note: ` · needs ${names}, and automatic insertion is off`,
      };
    case "order":
      return {
        enabled: false,
        note: ` · ${names} sits later; move that up first`,
      };
    case "unreachable":
      return {
        enabled: false,
        note: " · a prerequisite is not in this catalog",
      };
    case "strand":
      return {
        enabled: false,
        note: " · would strand a course that depends on it",
      };
  }
}
