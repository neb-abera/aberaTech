/**
 * The curated tracks.
 *
 * A focus area is a bulk listing of everything JHU files under a heading, which
 * is useful for browsing and useless as a plan. A track is a specific, ordered
 * set of courses chosen for a purpose, and it says what it costs you as plainly
 * as what it buys.
 */
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import type { Track } from '../core/types';
import type { PlannerModel } from '../model/PlannerModel';

export interface TrackPickerProps {
  model: PlannerModel;
  onSelect: (id: string | null) => void;
}

export default function TrackPicker({ model, onSelect }: TrackPickerProps) {
  const groups: [string, Track[]][] = [
    ["Ten courses, the master's exactly", model.tracks.byKind('degree')],
    ['Longer, for mastery', model.tracks.byKind('mastery')]
  ];

  return (
    <Stack spacing={1.5}>
      {groups.map(([label, items]) =>
        items.length ? (
          <Box key={label}>
            <Typography variant="caption" sx={{ color: 'text.secondary', fontWeight: 700 }}>
              {label}
            </Typography>
            <Stack spacing={0.75} sx={{ mt: 0.75 }}>
              {items.map((t) => {
                const on = model.track === t.id;
                return (
                  <Paper
                    key={t.id}
                    variant="outlined"
                    component="button"
                    type="button"
                    onClick={() => {
                      onSelect(t.id);
                    }}
                    sx={{
                      textAlign: 'left',
                      cursor: 'pointer',
                      p: 1.25,
                      font: 'inherit',
                      borderColor: on ? 'primary.main' : 'divider',
                      borderWidth: on ? 2 : 1,
                      backgroundColor: on ? 'action.selected' : 'background.paper'
                    }}
                  >
                    <Typography component="span" variant="body2" sx={{ display: 'block', fontWeight: 600 }}>
                      {t.name}
                    </Typography>
                    <Typography component="span" variant="caption" sx={{ color: 'text.secondary', display: 'block' }}>
                      {t.length}
                    </Typography>
                    <Typography
                      component="span"
                      variant="caption"
                      sx={{ color: 'text.disabled', display: 'block', mt: 0.5 }}
                    >
                      {t.goal}
                    </Typography>
                  </Paper>
                );
              })}
            </Stack>
          </Box>
        ) : null
      )}
      <Button
        size="small"
        disabled={model.track === null}
        onClick={() => {
          onSelect(null);
        }}
      >
        Clear track and browse by focus area
      </Button>
    </Stack>
  );
}

/** The banner above the board describing the track currently in play. */
export function TrackBanner({ model }: { model: PlannerModel }) {
  const t = model.track ? model.tracks.get(model.track) : undefined;
  if (!t) return null;
  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Stack direction="row" spacing={1} alignItems="baseline" sx={{ flexWrap: 'wrap' }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          {t.name}
        </Typography>
        <Typography variant="caption" sx={{ color: 'text.secondary' }}>
          {t.length}
        </Typography>
      </Stack>
      <Typography variant="body2" sx={{ color: 'text.secondary', mt: 0.5 }}>
        {t.goal}
      </Typography>
      <Typography variant="body2" sx={{ mt: 1 }}>
        <Box component="span" sx={{ fontWeight: 600 }}>
          What it costs you.
        </Box>{' '}
        {t.tradeoff}
      </Typography>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.75, mt: 1.25 }}>
        {t.stages.map((s) => (
          <Box key={s.name} sx={{ px: 1, py: 0.25, border: '1px solid', borderColor: 'divider', borderRadius: 1 }}>
            <Typography variant="caption">
              <b>{s.name}</b> {s.courses.length}
            </Typography>
          </Box>
        ))}
      </Box>
    </Paper>
  );
}
