/**
 * The planner itself: the rail of controls, the board of terms, and the pool of
 * everything not yet placed.
 *
 * The model is the source of truth and is mutated through `update`. Everything
 * on screen is derived from it on each render, so there is no second copy of the
 * plan to drift out of step.
 */

import ExpandMore from "@mui/icons-material/ExpandMore";
import Tune from "@mui/icons-material/Tune";
import Accordion from "@mui/material/Accordion";
import AccordionDetails from "@mui/material/AccordionDetails";
import AccordionSummary from "@mui/material/AccordionSummary";
import Box from "@mui/material/Box";
import Drawer from "@mui/material/Drawer";
import GlobalStyles from "@mui/material/GlobalStyles";
import Paper from "@mui/material/Paper";
import Popper from "@mui/material/Popper";
import Stack from "@mui/material/Stack";
import { useTheme } from "@mui/material/styles";
import Typography from "@mui/material/Typography";
import useMediaQuery from "@mui/material/useMediaQuery";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { DEFAULT_COLOR_SCHEME } from "../../../theme/AppTheme";
import { areaColorVariables } from "../core/areaColors";
import { missingFor } from "../core/prereq";
import { type Planner, usePlanner } from "../hooks/usePlanner";
import CourseCard from "./CourseCard";
import CoursePool from "./CoursePool";
import GraduationPanel from "./GraduationPanel";
import MessageList from "./MessageList";
import NeedBar from "./NeedBar";
import type { PlannerContextValue } from "./PlannerContext";
import { PlannerProvider } from "./PlannerContext";
import RuleList from "./RuleList";
import SettingsRail from "./SettingsRail";
import StatusPills from "./StatusPills";
import TermBoard from "./TermBoard";
import { TrackBanner } from "./TrackPicker";

interface Detail {
  code: string;
  anchor: HTMLElement;
  pinned: boolean;
}

const HOVER_OPEN_MS = 140;
const HOVER_CLOSE_MS = 260;

/** What the line above the board says about where this plan is kept. */
function saveLine(saved: Planner["saved"], saving: boolean): string {
  switch (saved) {
    case "owner":
      return saving
        ? "Saving this plan to your account"
        : "Saved to your account";
    case "loading":
      return "Checking for a saved plan";
    case "error":
      return "The server did not answer. Changes stay in this tab.";
    case "visitor":
      return "Changes stay in this tab and are not saved.";
  }
}

export default function PlannerBoard() {
  const { model, update, version, saved, saving } = usePlanner();
  const theme = useTheme();
  // Only for the course card, which opens on a click, after hydration. The
  // layout itself follows the screen in CSS: the page is prerendered, and a
  // layout chosen here would be the server's guess until the script ran.
  const wide = useMediaQuery(theme.breakpoints.up("md"));
  const [railOpen, setRailOpen] = useState(false);
  const [drag, setDrag] = useState<string | null>(null);
  const [detail, setDetail] = useState<Detail | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const clearTimer = useCallback(() => {
    if (timer.current !== null) {
      clearTimeout(timer.current);
      timer.current = null;
    }
  }, []);

  useEffect(() => clearTimer, [clearTimer]);

  // The anchor is a chip. Removing that course, switching track or changing a
  // focus area detaches the node, and a Popper anchored to a detached node warns
  // and positions itself at the origin. Drop the card when its anchor goes.
  // biome-ignore lint/correctness/useExhaustiveDependencies: version is the mutation counter that forces recompute of the mutable model
  useEffect(() => {
    setDetail((cur) => (cur && !cur.anchor.isConnected ? null : cur));
  }, [version]);

  const hoverDetail = useCallback(
    (code: string, anchor: HTMLElement) => {
      clearTimer();
      timer.current = setTimeout(() => {
        setDetail((cur) =>
          cur?.pinned ? cur : { code, anchor, pinned: false },
        );
      }, HOVER_OPEN_MS);
    },
    [clearTimer],
  );

  const releaseDetail = useCallback(() => {
    clearTimer();
    // A grace period, so the pointer can travel onto the card to follow a link
    // or scroll it. The card itself cancels this while the pointer is over it.
    timer.current = setTimeout(() => {
      setDetail((cur) => (cur?.pinned ? cur : null));
    }, HOVER_CLOSE_MS);
  }, [clearTimer]);

  const pinDetail = useCallback(
    (code: string, anchor: HTMLElement) => {
      clearTimer();
      setDetail((cur) =>
        cur?.pinned && cur.code === code
          ? null
          : { code, anchor, pinned: true },
      );
      update((m) => {
        m.focus = m.focus === code ? null : code;
      });
    },
    [clearTimer, update],
  );

  const closeDetail = useCallback(() => {
    clearTimer();
    setDetail(null);
    update((m) => {
      m.focus = null;
    });
  }, [clearTimer, update]);

  // Escape closes a pinned card, which is the only one that outlives the pointer.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") closeDetail();
    };
    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("keydown", onKey);
    };
  }, [closeDetail]);

  // Derived once per mutation, then read by every chip.
  // biome-ignore lint/correctness/useExhaustiveDependencies: version is the mutation counter that forces recompute of the mutable model
  const audit = useMemo(() => model.audit(), [model, version]);
  // biome-ignore lint/correctness/useExhaustiveDependencies: version is the mutation counter that forces recompute of the mutable model
  const auto = useMemo(() => model.autoAdded(), [model, version]);
  // biome-ignore lint/correctness/useExhaustiveDependencies: version is the mutation counter that forces recompute of the mutable model
  const broken = useMemo(
    () => new Set(model.plan.violations().map((v) => v.code)),
    [model, version],
  );
  // biome-ignore lint/correctness/useExhaustiveDependencies: version is the mutation counter that forces recompute of the mutable model
  const needed = useMemo(
    () =>
      model.focus
        ? new Set(
            missingFor(
              model.courses,
              model.focus,
              new Set(model.plan.courses()),
            ),
          )
        : new Set<string>(),
    [model, version],
  );
  const applied = useMemo(() => new Set(audit.counted), [audit]);

  const ctx: PlannerContextValue = {
    model,
    update,
    drag,
    setDrag,
    hoverDetail,
    releaseDetail,
    pinDetail,
    applied,
    auto,
    broken,
    needed,
  };

  return (
    <PlannerProvider value={ctx}>
      {/*
        The area colours for both schemes. The default scheme's go on :root,
        so a page read before any script runs (no scheme attribute yet) still
        matches the theme around it.
      */}
      <GlobalStyles
        styles={{
          ":root": areaColorVariables(DEFAULT_COLOR_SCHEME),
          '[data-mui-color-scheme="light"]': areaColorVariables("light"),
          '[data-mui-color-scheme="dark"]': areaColorVariables("dark"),
        }}
      />
      <Box
        sx={{
          // A flex column rather than a grid on a phone: a sticky child can
          // only travel within its containing block, and in a grid that is the
          // row it sits in, which is exactly as tall as the pane itself.
          display: { xs: "flex", md: "grid" },
          flexDirection: "column",
          gridTemplateColumns: { md: "320px minmax(0, 1fr)" },
          gap: 3,
          alignItems: { xs: "stretch", md: "start" },
        }}
      >
        {/*
          One rail for every screen, laid out by CSS so the prerendered page
          is right before any script runs. From md up it is the left column,
          sticky, always open, with its summary hidden. Below md it is a pane
          that expands in place. Collapsed by default so the plan itself is
          the first thing a phone shows, and stuck below the app bar so it is
          one tap away however far down the page the reader has scrolled. The
          details scroll within the pane, like the wide column.

          While open on a phone the pane is fixed to the viewport rather than
          sticky: sticky travel ends at the parent's bottom edge, so a pane
          opened near the bottom of the page would be shoved up and off the
          screen.

          The collapsed details stay in the page, so a crawler reads them and
          the wide column can show them without a script.
        */}
        <Accordion
          variant="outlined"
          disableGutters
          expanded={railOpen}
          onChange={(_, open) => {
            setRailOpen(open);
          }}
          sx={(t) => ({
            position: railOpen ? "fixed" : "sticky",
            top: "calc(var(--template-frame-height, 0px) + 96px)",
            left: railOpen ? 16 : "auto",
            right: railOpen ? 16 : "auto",
            zIndex: t.zIndex.appBar - 1,
            bgcolor: "background.default",
            // Floating over the board as it scrolls, the pane needs a shadow
            // to read as above the cards passing beneath it.
            boxShadow: 4,
            "&::before": { display: "none" },
            [t.breakpoints.up("md")]: {
              position: "sticky",
              top: 96,
              left: "auto",
              right: "auto",
              zIndex: "auto",
              bgcolor: "background.paper",
              boxShadow: "none",
              maxHeight: "82vh",
              overflowY: "auto",
              "& .MuiAccordionSummary-root": { display: "none" },
              "& .MuiCollapse-root": {
                height: "auto !important",
                visibility: "visible",
              },
            },
          })}
        >
          <AccordionSummary expandIcon={<ExpandMore />}>
            <Tune fontSize="small" sx={{ mr: 1, alignSelf: "center" }} />
            <Typography>Tracks, focus areas and settings</Typography>
          </AccordionSummary>
          <AccordionDetails
            sx={(t) => ({
              maxHeight: "calc(100vh - 220px)",
              overflowY: "auto",
              [t.breakpoints.up("md")]: {
                p: 2,
                maxHeight: "none",
                overflowY: "visible",
              },
            })}
          >
            <SettingsRail model={model} update={update} />
          </AccordionDetails>
        </Accordion>

        <Stack spacing={2} sx={{ minWidth: 0 }}>
          <StatusPills model={model} audit={audit} />
          {/*
            Always one line, whoever is asking. The prerendered page cannot
            know, and a line that appeared once the answer came pushed the
            whole board down by 36 px.
          */}
          <Typography
            variant="caption"
            noWrap
            sx={{ color: "text.secondary" }}
            aria-live="polite"
          >
            {saveLine(saved, saving)}
          </Typography>
          <TrackBanner model={model} />
          <NeedBar />
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
              Degree rules
            </Typography>
            <RuleList rules={audit.rules} />
          </Paper>
          <GraduationPanel
            audit={audit}
            calendar={model.calendar}
            model={model}
          />
          <MessageList model={model} />
          <Box>
            <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
              The plan
            </Typography>
            <TermBoard />
          </Box>
          <CoursePool />
        </Stack>
      </Box>

      {wide ? (
        <Popper
          open={detail?.anchor.isConnected ?? false}
          anchorEl={detail?.anchor ?? null}
          placement="right-start"
          modifiers={[
            { name: "offset", options: { offset: [0, 10] } },
            { name: "preventOverflow", options: { padding: 12 } },
            { name: "flip", options: { padding: 12 } },
          ]}
          sx={{ zIndex: (t) => t.zIndex.tooltip }}
          onMouseEnter={clearTimer}
          onMouseLeave={() => {
            if (!detail?.pinned) releaseDetail();
          }}
        >
          {detail && (
            <Paper elevation={8} sx={{ borderRadius: 2, maxWidth: 380 }}>
              <CourseCard
                code={detail.code}
                pinned={detail.pinned}
                onClose={closeDetail}
              />
            </Paper>
          )}
        </Popper>
      ) : (
        // A popper beside the chip would hang off the edge of a phone, so the
        // card rises from the bottom instead. Persistent rather than modal on
        // purpose: a backdrop would cover the chip the moment the card opened,
        // fire mouseleave, close it, and start over — hover has to be able to
        // show this sheet without anything coming between pointer and chip.
        detail && (
          <Drawer
            variant="persistent"
            anchor="bottom"
            open
            slotProps={{
              transition: { appear: true },
              paper: {
                sx: {
                  maxHeight: "75vh",
                  overflowY: "auto",
                  borderTopLeftRadius: 12,
                  borderTopRightRadius: 12,
                  boxShadow: 8,
                },
                onMouseEnter: clearTimer,
                onMouseLeave: () => {
                  if (!detail.pinned) releaseDetail();
                },
              },
            }}
          >
            <CourseCard
              code={detail.code}
              pinned={detail.pinned}
              onClose={closeDetail}
            />
          </Drawer>
        )
      )}
    </PlannerProvider>
  );
}
