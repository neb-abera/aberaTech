import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import QueuePanel from './QueuePanel';
import SlotList from './SlotList';
import { useSchedule } from '../hooks/useSchedule';
import { formatDay, formatTime, viewerZone } from '../core/format';

/**
 * One page that decides for itself what it is.
 *
 * This is the whole reason the queue is not a separate application. A link to
 * "schedule time with me" has to be one link — it goes in an email signature and
 * in a text message to everybody at once, and it cannot be two links that the
 * reader is expected to choose between. So the server answers "what is
 * happening right now", and the page renders that: the queue when one is open,
 * otherwise the slots.
 */
export default function SchedulePanel() {
  const { state, place, error, loading, join, leave, book, booking, selectDate } = useSchedule();

  if (loading) {
    return <CircularProgress size={28} aria-label="Loading the schedule" />;
  }

  if (error || !state) {
    return <Alert severity="error">{error ?? 'The schedule is unavailable.'}</Alert>;
  }

  return (
    <Stack spacing={3}>
      {state.mode === 'unavailable' ? (
        <Alert severity="info">
          Online booking is still being set up. Email me and we will find a time the old fashioned way.
        </Alert>
      ) : state.mode === 'queue' && state.queue ? (
        <QueuePanel queue={state.queue} place={place} onJoin={join} onLeave={leave} />
      ) : (
        <>
          {booking ? (
            <Alert severity="success">
              Booked for {formatDay(booking.startsAt)} at {formatTime(booking.startsAt)}. A confirmation is on its way
              to your phone.
            </Alert>
          ) : null}
          <SlotList
            availableDates={state.availableDates ?? []}
            selectedDate={state.selectedDate}
            slots={state.slots}
            onSelectDate={selectDate}
            onBook={book}
          />
        </>
      )}

      {/* Only where there are times to qualify. Promising a time zone on a
          page showing no times reads as boilerplate. */}
      {state.mode === 'unavailable' ? null : (
        <Typography variant="caption" sx={{ color: 'text.disabled' }}>
          Times are shown in your own time zone ({viewerZone()}).
        </Typography>
      )}
    </Stack>
  );
}
