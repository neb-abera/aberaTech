/**
 * The course card. Shown on hover, pinned on click or tap.
 *
 * The body is identical either way, so nothing is hidden behind a click that a
 * hover would not tell you. Only the actions appear when it is pinned, because
 * acting on a card that is about to dismiss itself is a trap.
 */
import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Divider from '@mui/material/Divider';
import IconButton from '@mui/material/IconButton';
import Link from '@mui/material/Link';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import CloseRounded from '@mui/icons-material/CloseRounded';
import OpenInNew from '@mui/icons-material/OpenInNew';
import { courseLinks } from '../core/links';
import { missingFor } from '../core/prereq';
import { plural } from '../core/format';
import { usePlannerContext } from './PlannerContext';

export interface CourseCardProps {
  code: string;
  pinned: boolean;
  onClose: () => void;
}

export default function CourseCard({ code, pinned, onClose }: CourseCardProps) {
  const ctx = usePlannerContext();
  const { model } = ctx;
  const course = model.get(code);
  if (!course) return null;

  const need = missingFor(model.courses, code, new Set(model.plan.courses()));
  const applied = ctx.applied.has(code);
  const gates = model.gates(code);
  const term = model.plan.termOf(code);
  const links = courseLinks(code, course.title);

  return (
    <Box sx={{ p: 2, maxWidth: 380, maxHeight: '70vh', overflowY: 'auto' }}>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'flex-start', mb: 0.5 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600, flexGrow: 1, lineHeight: 1.3 }}>
          {course.title}
        </Typography>
        <IconButton size="small" aria-label="Close the course card" onClick={onClose} sx={{ mt: -0.5, mr: -0.5 }}>
          <CloseRounded fontSize="small" />
        </IconButton>
      </Stack>

      <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block' }}>
        {course.prep ? 'Preparation' : course.code} · {course.credits} credits
        {course.prep ? '' : ` · ${course.level}00 level`}
        {course.gradeable ? '' : ' · no graduate credit'}
        {term !== undefined ? ` · ${model.calendar.label(term)}` : ''}
      </Typography>

      {!course.prep && (
        <Stack direction="row" spacing={2} sx={{ mt: 1 }}>
          {links.map((l) => (
            <Link
              key={l.label}
              href={l.href}
              target="_blank"
              rel="noopener"
              title={l.note}
              variant="body2"
              sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }}
            >
              {l.label}
              {l.verified ? '' : ' (address derived, not checked)'}
              <OpenInNew sx={{ fontSize: 13 }} />
            </Link>
          ))}
        </Stack>
      )}

      <Typography variant="body2" sx={{ mt: 1.25, color: 'text.secondary' }}>
        {course.desc.length > 900 ? `${course.desc.slice(0, 900)}…` : course.desc}
      </Typography>

      <Divider sx={{ my: 1.25 }} />

      <Row label="Prerequisite as printed">{course.prereq_text || 'none stated'}</Row>
      {course.groups.length > 0 && (
        <Row label="Enforced here">
          {course.groups.map((g) => g.map((m) => model.title(m)).join(' or ')).join(' and ')}
        </Row>
      )}
      {need.length > 0 && (
        <Row label={`Still needs ${need.length} ${plural(need.length, 'course')}`} tone="success.main">
          {need.map((m) => model.title(m)).join(', ')}
        </Row>
      )}
      {gates.map((g) =>
        g.composite ? (
          <Box key={g.id} sx={{ mt: 0.75 }}>
            <Typography variant="body2" sx={{ color: 'warning.main' }}>
              <Box component="span" sx={{ fontWeight: 600 }}>
                Assumes:
              </Box>{' '}
              {g.label}
            </Typography>
            <Typography variant="caption" sx={{ display: 'block', color: 'text.secondary', mt: 0.25 }}>
              That is a degree, not a course, so nothing is scheduled for it and the course is not blocked.{' '}
              {g.missing.length
                ? `The parts of it you have not ticked are ${g.missing.join(', ')}.`
                : 'You have ticked every part of it.'}
            </Typography>
            {pinned && g.missing.length > 0 && (
              <Button
                size="small"
                variant="outlined"
                sx={{ mt: 0.75 }}
                onClick={() => {
                  ctx.update((m) => {
                    m.expandBackground(g.id, !m.expandedBackground.has(g.id));
                  });
                }}
              >
                {model.expandedBackground.has(g.id)
                  ? 'Stop scheduling the equivalent coursework'
                  : `Add the ${g.missing.length} equivalent ${plural(g.missing.length, 'course')}`}
              </Button>
            )}
          </Box>
        ) : (
          <Row key={g.id} label="Assumes" tone="warning.main">
            {g.label}
          </Row>
        )
      )}
      {course.excl.length > 0 && (
        <Row label="Mutually exclusive with" tone="warning.main">
          {course.excl.map((m) => model.title(m)).join(', ')}
        </Row>
      )}
      <Row label="Groups">{course.areas.join(', ') || 'none'}</Row>
      {applied && (
        <Row label="Applied to the degree" tone="success.main">
          One of your ten.
        </Row>
      )}

      {pinned && (
        <Stack direction="row" spacing={1} sx={{ mt: 1.5, flexWrap: 'wrap', gap: 1 }}>
          {course.gradeable && term !== undefined && (
            <Button
              size="small"
              variant="outlined"
              onClick={() => {
                ctx.update((m) => {
                  m.toggleDegreePick(code);
                });
              }}
            >
              {applied ? 'Remove from the degree' : 'Apply to the degree'}
            </Button>
          )}
          {term === undefined && (
            <Button
              size="small"
              variant="contained"
              onClick={() => {
                ctx.update((m) => {
                  m.placeAtEnd(code);
                });
              }}
            >
              {need.length ? `Add ${need.length} ${plural(need.length, 'prerequisite')} and place it` : 'Place it'}
            </Button>
          )}
          {term !== undefined && (
            <Button
              size="small"
              color="inherit"
              onClick={() => {
                ctx.update((m) => {
                  m.removeCourse(code);
                });
                onClose();
              }}
            >
              Back to the pool
            </Button>
          )}
        </Stack>
      )}
      {!pinned && (
        <Typography variant="caption" sx={{ display: 'block', mt: 1.25, color: 'text.disabled' }}>
          Click the course to pin this card and act on it.
        </Typography>
      )}
    </Box>
  );
}

function Row({ label, tone, children }: { label: string; tone?: string; children: ReactNode }) {
  return (
    <Typography variant="body2" sx={{ mt: 0.75, color: tone ?? 'text.primary' }}>
      <Box component="span" sx={{ fontWeight: 600 }}>
        {label}:
      </Box>{' '}
      {children}
    </Typography>
  );
}
