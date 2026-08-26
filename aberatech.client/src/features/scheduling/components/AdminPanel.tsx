import * as React from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import Divider from '@mui/material/Divider';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import MenuItem from '@mui/material/MenuItem';
import { useAdminQueue } from '../hooks/useAdminQueue';
import AvailabilityEditor from './AvailabilityEditor';
import { formatTime } from '../core/format';

/**
 * The host's side of the queue: open it, work down the line, close it.
 *
 * This is the only screen in the feature that shows names and numbers, and it
 * is behind a Google sign-in restricted to one address.
 */
export default function AdminPanel() {
  const {
    configured,
    calendar,
    disconnectCalendar,
    signedIn,
    email,
    queue,
    error,
    loading,
    openSession,
    closeSession,
    advance,
    setDuration
  } = useAdminQueue();
  const [name, setName] = React.useState('');
  const [hoursOpen, setHoursOpen] = React.useState(8);
  const [openError, setOpenError] = React.useState<string | null>(null);

  if (loading) {
    return <CircularProgress size={28} aria-label="Loading" />;
  }

  if (!configured) {
    return (
      <Alert severity="info">
        Queue administration is not set up on this deployment yet. It needs Google credentials and an allowed address.
      </Alert>
    );
  }

  if (!signedIn) {
    return (
      <Stack spacing={2} sx={{ maxWidth: 420 }}>
        <Typography variant="body1" sx={{ color: 'text.secondary' }}>
          Sign in to run the queue.
        </Typography>
        <Box>
          <Button variant="contained" href="/api/scheduling/admin/sign-in?returnUrl=/schedule/admin">
            Sign in with Google
          </Button>
        </Box>
      </Stack>
    );
  }

  const waiting = queue?.entries.filter((entry) => entry.state === 'Waiting') ?? [];
  const serving = queue?.entries.find((entry) => entry.state === 'Serving');

  return (
    <Stack spacing={3}>
      {error ? <Alert severity="error">{error}</Alert> : null}

      <Typography variant="caption" sx={{ color: 'text.disabled' }}>
        Signed in as {email}
      </Typography>

      <AvailabilityEditor enabled={signedIn} />

      <Card variant="outlined">
        <CardContent>
          <Stack spacing={1.5}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
              Google calendar
            </Typography>

            {calendar?.connected ? (
              <>
                <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                  Reading free/busy from {calendar.email}. Times you are busy there are not offered here.
                </Typography>
                <Box>
                  <Button size="small" variant="outlined" color="inherit" onClick={() => void disconnectCalendar()}>
                    Disconnect
                  </Button>
                </Box>
                <Typography variant="caption" sx={{ color: 'text.disabled' }}>
                  Disconnecting removes the stored token from this site. It does not withdraw the grant at Google — do
                  that from your{' '}
                  <a
                    href="https://myaccount.google.com/permissions"
                    target="_blank"
                    rel="noopener noreferrer"
                    style={{ color: 'inherit' }}
                  >
                    account permissions
                  </a>
                  .
                </Typography>
              </>
            ) : (
              <>
                <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                  Not connected. Slots are offered from your availability rules alone, so anything already in your
                  calendar can still be booked over.
                </Typography>
                <Box>
                  <Button size="small" variant="contained" href="/api/scheduling/admin/calendar/connect">
                    Connect Google calendar
                  </Button>
                </Box>
              </>
            )}
          </Stack>
        </CardContent>
      </Card>

      {queue?.open ? (
        <Card variant="outlined">
          <CardContent>
            <Stack spacing={2}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
                <Typography variant="h6" component="p" sx={{ flexGrow: 1 }}>
                  {queue.name}
                </Typography>
                <Chip color="success" size="small" label="Open" />
                <Button size="small" color="inherit" variant="outlined" onClick={() => void closeSession()}>
                  Close the queue
                </Button>
              </Box>

              <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                {waiting.length} waiting{serving ? `, with ${serving.displayName} now` : ''}.
                {queue.closesAt ? ` Open until ${formatTime(queue.closesAt)}, then it closes itself.` : ''}
              </Typography>
            </Stack>
          </CardContent>
        </Card>
      ) : (
        <Card variant="outlined">
          <CardContent>
            <Stack
              spacing={2}
              component="form"
              onSubmit={async (event: React.FormEvent) => {
                event.preventDefault();
                setOpenError(await openSession(name.trim(), hoursOpen));
              }}
            >
              <Typography variant="h6" component="p">
                No queue is open
              </Typography>
              <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                Opening puts the queue on the public booking page immediately — there is nothing further to save. It
                stops taking names on its own after the time you pick, or when you close it.
              </Typography>
              {openError ? <Alert severity="error">{openError}</Alert> : null}
              <TextField
                label="What is this session?"
                placeholder="November initial counselling"
                value={name}
                onChange={(event) => setName(event.target.value)}
                required
                size="small"
                slotProps={{ htmlInput: { maxLength: 120 } }}
              />
              <TextField
                select
                size="small"
                label="Open for"
                value={hoursOpen}
                onChange={(event) => setHoursOpen(Number(event.target.value))}
                sx={{ width: 128 }}
              >
                {/* The same 1–24 range the server clamps to, so what is picked
                    here is what actually happens. */}
                {[1, 2, 3, 4, 6, 8, 12, 24].map((hours) => (
                  <MenuItem key={hours} value={hours}>
                    {hours === 1 ? '1 hour' : `${hours} hours`}
                  </MenuItem>
                ))}
              </TextField>
              <Box>
                <Button type="submit" variant="contained">
                  Open the queue
                </Button>
              </Box>
            </Stack>
          </CardContent>
        </Card>
      )}

      {queue && queue.entries.length > 0 ? (
        <Card variant="outlined">
          <CardContent>
            <Stack divider={<Divider />} spacing={1.5}>
              {queue.entries.map((entry) => (
                <Box key={entry.id} sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap', pt: 1 }}>
                  <Chip size="small" label={entry.position} />
                  <Box sx={{ flexGrow: 1, minWidth: 180 }}>
                    <Typography variant="body1">{entry.displayName}</Typography>
                    <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                      {entry.phoneE164}
                      {entry.projectedStart ? ` · ${formatTime(entry.projectedStart)}` : ''}
                    </Typography>
                  </Box>

                  {entry.state === 'Waiting' || entry.state === 'Serving' ? (
                    <TextField
                      select
                      size="small"
                      label="Needs"
                      value={entry.expectedMinutes}
                      onChange={(event) => void setDuration(entry.id, Number(event.target.value))}
                      sx={{ width: 104 }}
                    >
                      {/* Bounded to the same range the server accepts. A queue
                          estimate feeds everybody behind, so this is a choice
                          from sensible lengths rather than a free number. */}
                      {[5, 10, 15, 20, 30, 45, 60, 90, 120].map((minutes) => (
                        <MenuItem key={minutes} value={minutes}>
                          {minutes} min
                        </MenuItem>
                      ))}
                    </TextField>
                  ) : null}

                  {entry.state === 'Waiting' ? (
                    <>
                      <Button size="small" variant="contained" onClick={() => void advance(entry.id, 'start')}>
                        Start
                      </Button>
                      <Button size="small" color="inherit" onClick={() => void advance(entry.id, 'no-show')}>
                        No show
                      </Button>
                    </>
                  ) : entry.state === 'Serving' ? (
                    <Button size="small" variant="contained" onClick={() => void advance(entry.id, 'done')}>
                      Done
                    </Button>
                  ) : (
                    <Chip size="small" variant="outlined" label={entry.state} />
                  )}
                </Box>
              ))}
            </Stack>
          </CardContent>
        </Card>
      ) : null}
    </Stack>
  );
}
