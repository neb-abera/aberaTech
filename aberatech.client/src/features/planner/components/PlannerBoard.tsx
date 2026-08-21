/**
 * The planner itself: the rail of controls, the board of terms, and the pool of
 * everything not yet placed.
 *
 * The model is the source of truth and is mutated through `update`. Everything
 * on screen is derived from it on each render, so there is no second copy of the
 * plan to drift out of step.
 */
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Drawer from '@mui/material/Drawer';
import Paper from '@mui/material/Paper';
import Popper from '@mui/material/Popper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import useMediaQuery from '@mui/material/useMediaQuery';
import { useColorScheme, useTheme } from '@mui/material/styles';
import Tune from '@mui/icons-material/Tune';
import { missingFor } from '../core/prereq';
import { usePlanner } from '../hooks/usePlanner';
import CourseCard from './CourseCard';
import CoursePool from './CoursePool';
import GraduationPanel from './GraduationPanel';
import MessageList from './MessageList';
import NeedBar from './NeedBar';
import { PlannerProvider } from './PlannerContext';
import type { PlannerContextValue } from './PlannerContext';
import RuleList from './RuleList';
import SettingsRail from './SettingsRail';
import StatusPills from './StatusPills';
import TermBoard from './TermBoard';
import { TrackBanner } from './TrackPicker';

interface Detail {
  code: string;
  anchor: HTMLElement;
  pinned: boolean;
}

const HOVER_OPEN_MS = 140;
const HOVER_CLOSE_MS = 260;

export default function PlannerBoard() {
  const { model, update, version } = usePlanner();
  const theme = useTheme();
  const { mode: schemeMode, systemMode } = useColorScheme();
  // With a CSS variable theme the active scheme lives on useColorScheme, not on
  // theme.palette.mode, which stays on the default scheme.
  const mode: 'light' | 'dark' = (systemMode ?? schemeMode) === 'dark' ? 'dark' : 'light';
  const wide = useMediaQuery(theme.breakpoints.up('md'));
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
  useEffect(() => {
    setDetail((cur) => (cur && !cur.anchor.isConnected ? null : cur));
  }, [version]);

  const hoverDetail = useCallback(
    (code: string, anchor: HTMLElement) => {
      clearTimer();
      timer.current = setTimeout(() => {
        setDetail((cur) => (cur?.pinned ? cur : { code, anchor, pinned: false }));
      }, HOVER_OPEN_MS);
    },
    [clearTimer]
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
      setDetail((cur) => (cur?.pinned && cur.code === code ? null : { code, anchor, pinned: true }));
      update((m) => {
        m.focus = m.focus === code ? null : code;
      });
    },
    [clearTimer, update]
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
      if (e.key === 'Escape') closeDetail();
    };
    window.addEventListener('keydown', onKey);
    return () => {
      window.removeEventListener('keydown', onKey);
    };
  }, [closeDetail]);

  // Derived once per mutation, then read by every chip.
  const audit = useMemo(() => model.audit(), [model, version]);
  const auto = useMemo(() => model.autoAdded(), [model, version]);
  const broken = useMemo(() => new Set(model.plan.violations().map((v) => v.code)), [model, version]);
  const needed = useMemo(
    () =>
      model.focus ? new Set(missingFor(model.courses, model.focus, new Set(model.plan.courses()))) : new Set<string>(),
    [model, version]
  );
  const applied = useMemo(() => new Set(audit.counted), [audit]);

  const ctx: PlannerContextValue = {
    model,
    update,
    mode,
    drag,
    setDrag,
    hoverDetail,
    releaseDetail,
    pinDetail,
    applied,
    auto,
    broken,
    needed
  };

  const rail = <SettingsRail model={model} mode={mode} update={update} />;

  return (
    <PlannerProvider value={ctx}>
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', md: '320px minmax(0, 1fr)' },
          gap: 3,
          alignItems: 'start'
        }}
      >
        {wide ? (
          <Paper variant="outlined" sx={{ p: 2, position: 'sticky', top: 96, maxHeight: '82vh', overflowY: 'auto' }}>
            {rail}
          </Paper>
        ) : (
          <Box>
            <Button
              variant="outlined"
              startIcon={<Tune />}
              onClick={() => {
                setRailOpen(true);
              }}
            >
              Tracks, focus areas and settings
            </Button>
            <Drawer
              anchor="left"
              open={railOpen}
              onClose={() => {
                setRailOpen(false);
              }}
            >
              <Box sx={{ width: 320, p: 2 }}>{rail}</Box>
            </Drawer>
          </Box>
        )}

        <Stack spacing={2} sx={{ minWidth: 0 }}>
          <StatusPills model={model} audit={audit} />
          <TrackBanner model={model} />
          <NeedBar />
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
              Degree rules
            </Typography>
            <RuleList rules={audit.rules} />
          </Paper>
          <GraduationPanel audit={audit} calendar={model.calendar} model={model} />
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

      <Popper
        open={detail !== null && detail.anchor.isConnected}
        anchorEl={detail?.anchor ?? null}
        placement="right-start"
        modifiers={[
          { name: 'offset', options: { offset: [0, 10] } },
          { name: 'preventOverflow', options: { padding: 12 } },
          { name: 'flip', options: { padding: 12 } }
        ]}
        sx={{ zIndex: (t) => t.zIndex.tooltip }}
        onMouseEnter={clearTimer}
        onMouseLeave={() => {
          if (!detail?.pinned) releaseDetail();
        }}
      >
        {detail && (
          <Paper elevation={8} sx={{ borderRadius: 2 }}>
            <CourseCard code={detail.code} pinned={detail.pinned} onClose={closeDetail} />
          </Paper>
        )}
      </Popper>
    </PlannerProvider>
  );
}
