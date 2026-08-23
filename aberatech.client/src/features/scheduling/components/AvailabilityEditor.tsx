import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import CircularProgress from '@mui/material/CircularProgress';
import Stack from '@mui/material/Stack';
import Switch from '@mui/material/Switch';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useAvailability } from '../hooks/useAvailability';
import type { AvailabilityDay } from '../hooks/useAvailability';

const DayNames = ['', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

/**
 * The hours the booking page offers, before anything on the calendar is taken
 * out of them.
 *
 * These are the outer bounds of a bookable day, not a second copy of the
 * calendar. Google says when the host is busy; this says when to start and stop
 * offering time at all, which nothing in a calendar can answer.
 */
export default function AvailabilityEditor({ enabled }: { enabled: boolean }) {
  const { week, loading, error, saved, setWeek, save } = useAvailability(enabled);

  if (!enabled) return null;
  if (loading) return <CircularProgress size={24} aria-label="Loading your hours" />;
  if (!week) return error ? <Alert severity="error">{error}</Alert> : null;

  const update = (day: number, change: Partial<AvailabilityDay>) =>
    setWeek({ ...week, days: week.days.map((d) => (d.day === day ? { ...d, ...change } : d)) });

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={2}>
          <Box>
            <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
              Your hours
            </Typography>
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              When to offer times at all. Anything on your Google calendar is taken out of these hours automatically.
            </Typography>
          </Box>

          {error ? <Alert severity="error">{error}</Alert> : null}
          {saved ? <Alert severity="success">Saved.</Alert> : null}

          <TextField
            label="Time zone"
            value={week.zoneId}
            onChange={(event) => setWeek({ ...week, zoneId: event.target.value })}
            size="small"
            helperText="An IANA name, such as America/New_York, or Etc/GMT-3 for a fixed +3 offset."
            sx={{ maxWidth: 420 }}
          />

          <Stack spacing={1}>
            {week.days.map((day) => (
              <Box key={day.day} sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
                <Switch
                  checked={day.active}
                  onChange={(event) => update(day.day, { active: event.target.checked })}
                  inputProps={{ 'aria-label': DayNames[day.day] }}
                />
                <Typography variant="body2" sx={{ width: 96 }}>
                  {DayNames[day.day]}
                </Typography>
                <TextField
                  type="time"
                  size="small"
                  value={day.startsAt}
                  disabled={!day.active}
                  onChange={(event) => update(day.day, { startsAt: event.target.value })}
                  sx={{ width: 128 }}
                />
                <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                  to
                </Typography>
                <TextField
                  type="time"
                  size="small"
                  value={day.endsAt}
                  disabled={!day.active}
                  onChange={(event) => update(day.day, { endsAt: event.target.value })}
                  sx={{ width: 128 }}
                />
              </Box>
            ))}
          </Stack>

          <Box>
            <Button variant="contained" onClick={() => void save()}>
              Save hours
            </Button>
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}
