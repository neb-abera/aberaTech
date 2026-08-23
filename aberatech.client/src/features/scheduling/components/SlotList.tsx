import * as React from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import IconButton from '@mui/material/IconButton';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import type { SlotView } from '../core/types';
import { formatTime } from '../core/format';
import { addMonths, longDayLabel, monthGrid, monthLabel, monthOf } from '../core/month';
import SmsConsent from './SmsConsent';

interface Props {
  availableDates: string[];
  selectedDate: string | null;
  slots: SlotView[];
  onSelectDate: (date: string) => void;
  onBook: (startsAt: string, name: string, phone: string, smsConsent: boolean) => Promise<{ error: string | null }>;
}

const WeekdayInitials = ['S', 'M', 'T', 'W', 'T', 'F', 'S'];

/**
 * Pick a day, then a time — the shape every calendar has taught people to
 * expect.
 *
 * The previous version listed every quarter hour for the whole horizon at once,
 * which is hundreds of buttons to choose one, and then paged a week at a time to
 * keep the payload down. Both problems were the same problem: the list was the
 * wrong unit. A month knows which days are worth offering; only the chosen day
 * needs its times.
 */
export default function SlotList({ availableDates, selectedDate, slots, onSelectDate, onBook }: Props) {
  const firstAvailable = availableDates[0] ?? null;
  const anchor = selectedDate ?? firstAvailable;

  const [view, setView] = React.useState(() => (anchor ? monthOf(anchor) : monthOf(new Date().toISOString())));
  const [chosen, setChosen] = React.useState<SlotView | null>(null);
  const [name, setName] = React.useState('');
  const [phone, setPhone] = React.useState('');
  const [smsConsent, setSmsConsent] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [busy, setBusy] = React.useState(false);

  const open = new Set(availableDates);
  const cells = monthGrid(view.year, view.month);

  // Only offer months the horizon actually reaches, so nobody pages through
  // empty grids looking for availability that was never there.
  const withinRange = (delta: number) => {
    const next = addMonths(view.year, view.month, delta);
    const key = `${next.year}-${String(next.month).padStart(2, '0')}`;
    return availableDates.some((date) => date.startsWith(key));
  };

  const close = () => {
    setChosen(null);
    setError(null);
  };

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!chosen) return;

    setBusy(true);
    const result = await onBook(chosen.startsAt, name.trim(), phone.trim(), smsConsent);
    setBusy(false);

    if (result.error) {
      setError(result.error);
      return;
    }

    close();
  };

  if (availableDates.length === 0) {
    return (
      <Typography variant="body1" sx={{ color: 'text.secondary' }}>
        There is nothing open at the moment. Check back, or send me a note and I will open something up.
      </Typography>
    );
  }

  return (
    <>
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={4} alignItems="flex-start">
        <Box sx={{ width: { xs: '100%', md: 320 }, flexShrink: 0 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
            <IconButton
              size="small"
              aria-label="Previous month"
              disabled={!withinRange(-1)}
              onClick={() => setView(addMonths(view.year, view.month, -1))}
            >
              <ChevronLeftIcon fontSize="small" />
            </IconButton>
            <Typography variant="subtitle1" sx={{ flexGrow: 1, textAlign: 'center', fontWeight: 600 }}>
              {monthLabel(view.year, view.month)}
            </Typography>
            <IconButton
              size="small"
              aria-label="Next month"
              disabled={!withinRange(1)}
              onClick={() => setView(addMonths(view.year, view.month, 1))}
            >
              <ChevronRightIcon fontSize="small" />
            </IconButton>
          </Box>

          <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', gap: 0.5 }}>
            {WeekdayInitials.map((initial, index) => (
              <Typography key={index} variant="caption" sx={{ textAlign: 'center', color: 'text.disabled', pb: 0.5 }}>
                {initial}
              </Typography>
            ))}

            {cells.map((cell, index) => {
              // Bound to a const so the click handler keeps the narrowing. The
              // closure would otherwise see a mutable property that TypeScript
              // can no longer prove is non-null.
              const day = cell.date;

              return day === null ? (
                <Box key={`blank-${index}`} />
              ) : (
                <Button
                  key={day}
                  size="small"
                  disabled={!open.has(day)}
                  variant={day === anchor ? 'contained' : 'text'}
                  onClick={() => onSelectDate(day)}
                  sx={{ minWidth: 0, px: 0, aspectRatio: '1 / 1', borderRadius: '50%' }}
                >
                  {cell.dayOfMonth}
                </Button>
              );
            })}
          </Box>
        </Box>

        <Box sx={{ flexGrow: 1, width: '100%' }}>
          {anchor ? (
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1.5 }}>
              {longDayLabel(anchor)}
            </Typography>
          ) : null}

          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
            {slots.map((slot) => (
              <Button
                key={slot.startsAt}
                variant="outlined"
                size="small"
                sx={{ minWidth: 128 }}
                onClick={() => setChosen(slot)}
              >
                {formatTime(slot.startsAt)}
              </Button>
            ))}
          </Box>
        </Box>
      </Stack>

      <Dialog open={chosen !== null} onClose={close} fullWidth maxWidth="xs">
        <DialogTitle>{chosen && anchor ? `${longDayLabel(anchor)} at ${formatTime(chosen.startsAt)}` : ''}</DialogTitle>
        <Box component="form" onSubmit={submit}>
          <DialogContent>
            <Stack spacing={2}>
              {error ? <Alert severity="error">{error}</Alert> : null}
              <TextField
                label="Your name"
                value={name}
                onChange={(event) => setName(event.target.value)}
                required
                size="small"
                autoFocus
                slotProps={{ htmlInput: { maxLength: 120 } }}
              />
              <SmsConsent checked={smsConsent} onChange={setSmsConsent} />

              {/* Only asked for when it will be used. Collecting a number from
                  somebody who declined texts would be holding data with no
                  purpose. */}
              {smsConsent ? (
                <TextField
                  label="Mobile number"
                  value={phone}
                  onChange={(event) => setPhone(event.target.value)}
                  required
                  size="small"
                  type="tel"
                  helperText="US numbers only."
                  slotProps={{ htmlInput: { maxLength: 32 } }}
                />
              ) : null}
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={close} color="inherit">
              Cancel
            </Button>
            <Button type="submit" variant="contained" disabled={busy}>
              {busy ? 'Booking…' : 'Book it'}
            </Button>
          </DialogActions>
        </Box>
      </Dialog>
    </>
  );
}
