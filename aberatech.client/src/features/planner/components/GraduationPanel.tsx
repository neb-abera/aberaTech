/**
 * The five year clock, the deadline, and the levers if the plan does not fit.
 *
 * The clock spans the courses you APPLY to the degree, not everything you take,
 * which is the single most misread rule in the regulations. The panel says so
 * every time rather than assuming it was read once.
 */
import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import CheckCircleOutline from '@mui/icons-material/CheckCircleOutlineOutlined';
import ErrorOutline from '@mui/icons-material/ErrorOutlineOutlined';
import WarningAmber from '@mui/icons-material/WarningAmber';
import type { Calendar } from '../core/calendar';
import type { DegreeAudit } from '../core/rules';
import { LIMITS } from '../core/rules';
import { formatMonthYear } from '../core/format';
import type { PlannerModel } from '../model/PlannerModel';

export interface GraduationPanelProps {
  audit: DegreeAudit;
  calendar: Calendar;
  model: PlannerModel;
}

export default function GraduationPanel({ audit, calendar, model }: GraduationPanelProps) {
  const k = audit.clock;

  if (k.startTerm === null) {
    return (
      <Paper variant="outlined" sx={{ p: 2 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          Graduation
        </Typography>
        <Typography variant="body2" sx={{ color: 'text.secondary', mt: 0.5 }}>
          No course carrying graduate credit is placed yet, so the five year clock has not started. It begins with the
          first course you apply to the degree, not the first course you take.
        </Typography>
      </Paper>
    );
  }

  const late = k.onTime === false;
  const ready = audit.readyToGraduate;
  const finish = k.finishTerm ?? k.startTerm;
  const span = Math.max(1, (k.deadlineTerm ?? finish) - k.startTerm + 1);
  const used = Math.min(1, (finish - k.startTerm + 1) / span);
  const deadlinePct = k.deadlineTerm === null ? null : Math.min(100, ((k.deadlineTerm - k.startTerm + 1) / span) * 100);

  const rows: [string, string, string][] = [
    [
      'Clock starts',
      `${calendar.label(k.startTerm)} · ${formatMonthYear(k.startDate)}`,
      'the first course applied to the degree'
    ],
    [
      'Tenth course finishes',
      `${calendar.label(finish)} · ${formatMonthYear(k.projected)}`,
      'your earliest graduation'
    ],
    [
      'Hard deadline',
      formatMonthYear(k.deadline),
      `${LIMITS.YEARS} years${k.leaveMonths ? ` plus ${k.leaveMonths} months of leave` : ''}${k.extensionMonths ? ` plus a ${k.extensionMonths} month exception` : ''}`
    ],
    [
      late ? 'Over by' : 'Room to spare',
      `${Math.abs(k.slackMonths ?? 0)} months`,
      late ? 'the plan does not fit' : 'slack against the deadline'
    ]
  ];

  return (
    <Paper
      variant="outlined"
      sx={{
        p: 2,
        borderColor: late ? 'error.main' : ready ? 'success.main' : 'divider'
      }}
    >
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        {ready ? (
          <CheckCircleOutline color="success" fontSize="small" />
        ) : late ? (
          <ErrorOutline color="error" fontSize="small" />
        ) : null}
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          {ready
            ? `Ready to graduate ${calendar.label(finish)}`
            : late
              ? 'The five year limit is exceeded'
              : 'Graduation'}
        </Typography>
      </Stack>

      <Typography variant="body2" sx={{ color: 'text.secondary', mt: 0.5 }}>
        {audit.counted.length} of {LIMITS.COURSES} courses applied to the degree. The clock spans the courses you{' '}
        <Box component="span" sx={{ fontWeight: 600 }}>
          apply
        </Box>
        , not everything you take, so the other {audit.excluded.length} sit outside it.
      </Typography>

      {/* The bar is the elapsed span; the hairline is the deadline. */}
      <Box sx={{ position: 'relative', mt: 2, mb: 3 }}>
        <Box sx={{ height: 16, borderRadius: 1, backgroundColor: 'action.hover' }} />
        <Box
          sx={{
            position: 'absolute',
            inset: 0,
            width: `${(used * 100).toFixed(1)}%`,
            height: 16,
            borderTopRightRadius: 4,
            borderBottomRightRadius: 4,
            backgroundColor: late ? 'error.main' : 'primary.main'
          }}
        />
        <Typography
          variant="caption"
          sx={{ position: 'absolute', top: 20, left: 0, color: 'text.secondary', whiteSpace: 'nowrap' }}
        >
          {calendar.label(k.startTerm)}
        </Typography>
        {deadlinePct !== null && (
          <>
            <Box
              sx={{
                position: 'absolute',
                top: -3,
                left: `${deadlinePct.toFixed(1)}%`,
                width: '1px',
                height: 22,
                backgroundColor: 'text.primary'
              }}
            />
            <Typography
              variant="caption"
              sx={{
                position: 'absolute',
                top: 20,
                right: 0,
                color: 'text.secondary',
                whiteSpace: 'nowrap'
              }}
            >
              deadline {formatMonthYear(k.deadline)}
            </Typography>
          </>
        )}
      </Box>

      <Box component="table" sx={{ width: '100%', borderCollapse: 'collapse' }}>
        <Box component="tbody">
          {rows.map(([a, b, c]) => (
            <Box component="tr" key={a}>
              <Box component="td" sx={{ py: 0.4, pr: 1.5, whiteSpace: 'nowrap' }}>
                <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                  {a}
                </Typography>
              </Box>
              <Box component="td" sx={{ py: 0.4, pr: 1.5 }}>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {b}
                </Typography>
              </Box>
              <Box component="td" sx={{ py: 0.4 }}>
                <Typography variant="body2" sx={{ color: 'text.disabled' }}>
                  {c}
                </Typography>
              </Box>
            </Box>
          ))}
        </Box>
      </Box>

      {audit.blockers.length > 0 && (
        <Section title="Blocking graduation">
          {audit.blockers.slice(0, 6).map((b) => (
            <Line key={b.id + b.detail} icon={<ErrorOutline color="error" fontSize="small" />}>
              {b.detail}
            </Line>
          ))}
        </Section>
      )}

      {audit.levers.length > 0 && (
        <Section title="Ways out, cheapest first">
          {audit.levers.map((l) => (
            <Line key={l.id} icon={<WarningAmber color="warning" fontSize="small" />}>
              <Box component="span" sx={{ fontWeight: 600 }}>
                {l.title}.
              </Box>{' '}
              {l.detail}
            </Line>
          ))}
        </Section>
      )}

      <Section title={`The ten applied, ${audit.selection.automatic ? 'chosen for you' : 'chosen by you'}`}>
        <Typography variant="body2" sx={{ color: 'text.disabled', mb: 1 }}>
          {audit.selection.why}
        </Typography>
        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.75 }}>
          {audit.counted.map((c) => (
            <Box
              key={c}
              sx={{
                px: 1,
                py: 0.25,
                borderRadius: 1,
                border: '1px solid',
                borderColor: 'divider',
                backgroundColor: 'action.hover'
              }}
            >
              <Typography variant="caption">
                {(model.get(c)?.level ?? 0) >= 7 ? <b>700 </b> : null}
                {model.title(c)}
              </Typography>
            </Box>
          ))}
        </Box>
      </Section>
    </Paper>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Box sx={{ mt: 2 }}>
      <Divider sx={{ mb: 1.25 }} />
      <Typography variant="caption" sx={{ fontWeight: 700, display: 'block', mb: 0.75 }}>
        {title}
      </Typography>
      {children}
    </Box>
  );
}

function Line({ icon, children }: { icon: ReactNode; children: ReactNode }) {
  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: 'flex-start', py: 0.4 }}>
      <Box sx={{ mt: '2px' }}>{icon}</Box>
      <Typography variant="body2" sx={{ color: 'text.secondary' }}>
        {children}
      </Typography>
    </Stack>
  );
}
