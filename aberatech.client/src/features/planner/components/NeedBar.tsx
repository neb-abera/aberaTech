/**
 * What the focused course is waiting on, and the one button that fixes it.
 *
 * The feedback sits directly under the control that produced it. An earlier
 * version put this at the top of the page, a screen and a half above the chip
 * that had just been clicked, and it read as though nothing had happened.
 */
import Alert from '@mui/material/Alert';
import AlertTitle from '@mui/material/AlertTitle';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { plural } from '../core/format';
import CourseChip from './CourseChip';
import { usePlannerContext } from './PlannerContext';

export default function NeedBar() {
  const ctx = usePlannerContext();
  const { model } = ctx;
  const code = model.focus;
  if (!code) return null;
  const course = model.get(code);
  if (!course) return null;
  const needed = [...ctx.needed];

  const dismiss = () => {
    ctx.update((m) => {
      m.focus = null;
    });
  };

  if (!needed.length) {
    return (
      <Alert severity="success" onClose={dismiss}>
        <AlertTitle>{course.title} is ready to place</AlertTitle>
        Every prerequisite is already in the plan. Drag it into any term outlined in green, or use the menu on the chip.
      </Alert>
    );
  }

  return (
    <Alert severity="info" onClose={dismiss}>
      <AlertTitle>
        {course.title} needs {needed.length} {plural(needed.length, 'course')} first
      </AlertTitle>
      <Typography variant="body2" sx={{ mb: 1 }}>
        Outlined in green below. Add them yourself, or let the planner place them for you.
      </Typography>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.75, mb: 1.25 }}>
        {needed.map((n) => (
          <CourseChip key={n} code={n} inPool blocked readOnly />
        ))}
      </Box>
      <Stack direction="row" spacing={1}>
        <Button
          size="small"
          variant="contained"
          onClick={() => {
            ctx.update((m) => {
              m.autoPlace(code);
            });
          }}
        >
          Add these and place {course.title}
        </Button>
      </Stack>
    </Alert>
  );
}
