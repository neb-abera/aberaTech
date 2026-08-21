import * as React from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Chip from '@mui/material/Chip';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import type { MyPlace, QueueView } from '../core/types';
import { describeWait, formatTime } from '../core/format';

interface Props {
  queue: QueueView;
  place: MyPlace | null;
  onJoin: (name: string, phone: string) => Promise<string | null>;
  onLeave: () => Promise<void>;
}

/**
 * The queue: either the form to join it, or where you stand in it.
 *
 * Note what this never shows: who else is waiting. Position and count only. The
 * people in this queue are being seen for counselling, and who is in line ahead
 * of you is not something the person behind you is entitled to know.
 */
export default function QueuePanel({ queue, place, onJoin, onLeave }: Props) {
  const [name, setName] = React.useState('');
  const [phone, setPhone] = React.useState('');
  const [error, setError] = React.useState<string | null>(null);
  const [busy, setBusy] = React.useState(false);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(await onJoin(name.trim(), phone.trim()));
    setBusy(false);
  };

  if (place && place.state === 'Waiting') {
    return (
      <Card variant="outlined">
        <CardContent>
          <Stack spacing={2}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
              <Chip color="primary" label={`Position ${place.position}`} />
              <Typography variant="body1">
                {place.ahead === 0
                  ? 'You are next.'
                  : `${place.ahead} ${place.ahead === 1 ? 'person' : 'people'} ahead of you.`}
              </Typography>
            </Box>

            <Typography variant="h6" component="p">
              {describeWait(place.minutesAway)}
              {place.projectedStart ? ` — around ${formatTime(place.projectedStart)}` : ''}
            </Typography>

            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              This updates on its own. I will text you if the estimate moves by more than ten minutes, and again when
              you are up.
            </Typography>

            <Box>
              <Button variant="outlined" color="inherit" size="small" onClick={() => void onLeave()}>
                Leave the queue
              </Button>
            </Box>
          </Stack>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={2} component="form" onSubmit={submit}>
          <Box>
            <Typography variant="h6" component="p">
              {queue.name}
            </Typography>
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {queue.waiting === 0
                ? 'Nobody is waiting. You would be first.'
                : `${queue.waiting} waiting${queue.nextStartsAt ? `, next at ${formatTime(queue.nextStartsAt)}` : ''}.`}
            </Typography>
          </Box>

          {error ? <Alert severity="error">{error}</Alert> : null}

          <TextField
            label="Your name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            required
            size="small"
            slotProps={{ htmlInput: { maxLength: 120 } }}
          />
          <TextField
            label="Mobile number"
            value={phone}
            onChange={(event) => setPhone(event.target.value)}
            required
            size="small"
            type="tel"
            helperText="US numbers only. Used for queue updates and nothing else."
            slotProps={{ htmlInput: { maxLength: 32 } }}
          />

          <Box>
            <Button type="submit" variant="contained" disabled={busy || !name.trim() || !phone.trim()}>
              {busy ? 'Joining…' : 'Join the queue'}
            </Button>
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}
