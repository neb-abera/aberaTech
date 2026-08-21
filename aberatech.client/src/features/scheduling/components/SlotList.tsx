import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import type { SlotView } from '../core/types';
import { formatDay, formatTime, groupByDay } from '../core/format';

/**
 * The slots on offer, grouped by the day they fall on *for the visitor*.
 *
 * Every time on screen carries its zone. Somebody booking from Germany sees
 * their own evening, labelled as their own evening, and is never asked to do
 * the arithmetic themselves.
 */
export default function SlotList({ slots }: { slots: SlotView[] }) {
  if (slots.length === 0) {
    return (
      <Typography variant="body1" sx={{ color: 'text.secondary' }}>
        There is nothing open at the moment. Check back, or send me a note and I will open something up.
      </Typography>
    );
  }

  return (
    <Stack spacing={3}>
      {groupByDay(slots).map(([key, daySlots]) => (
        <Box key={key}>
          <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
            {formatDay(daySlots[0].startsAt)}
          </Typography>
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
            {daySlots.map((slot) => (
              <Button key={slot.startsAt} variant="outlined" size="small" sx={{ minWidth: 132 }}>
                {formatTime(slot.startsAt)}
              </Button>
            ))}
          </Box>
        </Box>
      ))}
    </Stack>
  );
}
