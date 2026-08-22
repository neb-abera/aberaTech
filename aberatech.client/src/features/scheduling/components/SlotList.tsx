import * as React from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import type { SlotView } from '../core/types';
import { formatDay, formatTime, groupByDay } from '../core/format';

interface Props {
  slots: SlotView[];
  onBook: (startsAt: string, name: string, phone: string) => Promise<{ error: string | null }>;
}

/**
 * The slots on offer, grouped by the day they fall on *for the visitor*.
 *
 * Every time on screen carries its zone. Somebody booking from Germany sees
 * their own evening, labelled as their own evening, and is never asked to do
 * the arithmetic themselves.
 */
export default function SlotList({ slots, onBook }: Props) {
  const [chosen, setChosen] = React.useState<SlotView | null>(null);
  const [name, setName] = React.useState('');
  const [phone, setPhone] = React.useState('');
  const [error, setError] = React.useState<string | null>(null);
  const [busy, setBusy] = React.useState(false);

  const close = () => {
    setChosen(null);
    setError(null);
  };

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!chosen) return;

    setBusy(true);
    const result = await onBook(chosen.startsAt, name.trim(), phone.trim());
    setBusy(false);

    if (result.error) {
      setError(result.error);
      return;
    }

    close();
  };

  if (slots.length === 0) {
    return (
      <Typography variant="body1" sx={{ color: 'text.secondary' }}>
        There is nothing open at the moment. Check back, or send me a note and I will open something up.
      </Typography>
    );
  }

  return (
    <>
      <Stack spacing={3}>
        {groupByDay(slots).map(([key, daySlots]) => (
          <Box key={key}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
              {formatDay(daySlots[0].startsAt)}
            </Typography>
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
              {daySlots.map((slot) => (
                <Button
                  key={slot.startsAt}
                  variant="outlined"
                  size="small"
                  sx={{ minWidth: 132 }}
                  onClick={() => setChosen(slot)}
                >
                  {formatTime(slot.startsAt)}
                </Button>
              ))}
            </Box>
          </Box>
        ))}
      </Stack>

      <Dialog open={chosen !== null} onClose={close} fullWidth maxWidth="xs">
        <DialogTitle>{chosen ? `${formatDay(chosen.startsAt)} at ${formatTime(chosen.startsAt)}` : ''}</DialogTitle>
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
              <TextField
                label="Mobile number"
                value={phone}
                onChange={(event) => setPhone(event.target.value)}
                required
                size="small"
                type="tel"
                helperText="US numbers only. Used for your confirmation and reminder, nothing else."
                slotProps={{ htmlInput: { maxLength: 32 } }}
              />
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
