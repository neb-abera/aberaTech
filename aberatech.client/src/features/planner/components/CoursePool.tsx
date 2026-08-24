/**
 * Everything selected but not yet placed, split into what can be placed now and
 * what is waiting on a prerequisite. Dropping a course back here removes it from
 * the plan.
 */
import { useState } from 'react';
import type { ReactNode } from 'react';
import type { DragEvent } from 'react';
import Box from '@mui/material/Box';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { legalTerms } from '../core/prereq';
import CourseChip from './CourseChip';
import { usePlannerContext } from './PlannerContext';

export default function CoursePool() {
  const ctx = usePlannerContext();
  const { model } = ctx;
  const [query, setQuery] = useState('');
  const [onlyReady, setOnlyReady] = useState(false);

  const q = query.trim().toLowerCase();
  const codes = model
    .unplaced()
    .filter((c) => !q || model.title(c).toLowerCase().includes(q) || c.toLowerCase().includes(q));

  // One placement map for the whole pool rather than one per course: this list
  // can hold the entire catalog.
  const placed = model.plan.placement();
  const termCount = model.plan.terms.length;
  const ready: string[] = [];
  const blocked: string[] = [];
  for (const c of codes) (legalTerms(model.courses, c, placed, termCount).size ? ready : blocked).push(c);
  const byTitle = (a: string, b: string) => model.title(a).localeCompare(model.title(b));
  ready.sort(byTitle);
  blocked.sort(byTitle);

  const onDrop = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    const code = e.dataTransfer.getData('text/plain');
    ctx.setDrag(null);
    if (!code) return;
    ctx.update((m) => {
      m.removeCourse(code);
    });
  };

  return (
    <Paper
      variant="outlined"
      onDragOver={(e) => {
        if (ctx.drag && model.plan.has(ctx.drag)) e.preventDefault();
      }}
      onDrop={onDrop}
      sx={{ p: 1.5 }}
    >
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={1}
        sx={{ alignItems: { sm: 'center' }, justifyContent: 'space-between', mb: 1.25 }}
      >
        <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
          Not yet placed
          <Typography component="span" variant="caption" sx={{ color: 'text.secondary', ml: 1 }}>
            {codes.length} of {model.selected().size} selected
          </Typography>
        </Typography>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          <TextField
            size="small"
            label="Filter"
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
            }}
            sx={{ minWidth: 180 }}
          />
          <FormControlLabel
            control={
              <Checkbox
                size="small"
                checked={onlyReady}
                onChange={(e) => {
                  setOnlyReady(e.target.checked);
                }}
              />
            }
            label={
              <Typography component="span" variant="body2">
                Ready only
              </Typography>
            }
          />
        </Stack>
      </Stack>

      <Group title="Ready to place" count={ready.length}>
        {ready.length ? (
          ready.map((c) => <CourseChip key={c} code={c} inPool />)
        ) : (
          <Typography variant="caption" sx={{ color: 'text.disabled' }}>
            Nothing left to place.
          </Typography>
        )}
      </Group>

      {!onlyReady && blocked.length > 0 && (
        <Group title="Waiting on a prerequisite, click one to see what it needs" count={blocked.length}>
          {blocked.map((c) => (
            <CourseChip key={c} code={c} inPool blocked />
          ))}
        </Group>
      )}
    </Paper>
  );
}

function Group({ title, count, children }: { title: string; count: number; children: ReactNode }) {
  return (
    <Box sx={{ mt: 1.5 }}>
      <Typography variant="caption" sx={{ color: 'text.secondary', fontWeight: 600 }}>
        {title} · {count}
      </Typography>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.75, mt: 0.75 }}>{children}</Box>
    </Box>
  );
}
