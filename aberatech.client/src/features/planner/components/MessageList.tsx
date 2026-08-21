/**
 * What the plan is telling you: broken sequences first, then the courses the
 * planner pulled in for you, then the preparation it is assuming you still need.
 */
import Accordion from '@mui/material/Accordion';
import AccordionDetails from '@mui/material/AccordionDetails';
import AccordionSummary from '@mui/material/AccordionSummary';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import ExpandMore from '@mui/icons-material/ExpandMore';
import { plural } from '../core/format';
import type { PlannerModel } from '../model/PlannerModel';

export default function MessageList({ model }: { model: PlannerModel }) {
  const violations = model.plan.violations();
  const auto = model.autoAdded();
  const prep = model.preparation();

  return (
    <Stack spacing={1}>
      {violations.slice(0, 6).map((v) => (
        <Alert severity="error" key={`${v.kind}-${v.code}-${v.detail}`}>
          {v.kind === 'exclusion' ? (
            <>
              <b>{model.title(v.code)}</b> and <b>{model.title(v.detail)}</b> are mutually exclusive.
            </>
          ) : (
            <>
              <b>{model.title(v.code)}</b> sits before its prerequisite{' '}
              {(v.group ?? []).map((g) => model.title(g)).join(' or ')}.
            </>
          )}
        </Alert>
      ))}

      {violations.length === 0 && model.plan.courses().length > 0 && (
        <Alert severity="success">Every prerequisite is satisfied in sequence.</Alert>
      )}

      {auto.size > 0 && (
        <Accordion disableGutters variant="outlined">
          <AccordionSummary expandIcon={<ExpandMore />}>
            <Typography variant="body2">
              <b>
                {auto.size} {plural(auto.size, 'course')} pulled in automatically
              </b>{' '}
              because something you chose requires {auto.size > 1 ? 'them' : 'it'}. Marked <i>req</i>.
            </Typography>
          </AccordionSummary>
          <AccordionDetails>
            {[...auto].map((c) => (
              <Typography variant="body2" key={c} sx={{ py: 0.25 }}>
                <b>{model.title(c)}</b> required by{' '}
                {model
                  .pulledBy(c)
                  .map((k) => model.title(k))
                  .join(', ') || 'the plan'}
              </Typography>
            ))}
          </AccordionDetails>
        </Accordion>
      )}

      {prep.length > 0 && (
        <Accordion disableGutters variant="outlined">
          <AccordionSummary expandIcon={<ExpandMore />}>
            <Typography variant="body2">
              <b>
                {prep.length} preparation {plural(prep.length, 'course')}
              </b>{' '}
              in the plan. The catalog states these in prose rather than course numbers, so they are scheduled like any
              other prerequisite. Tick one under Background if you already have it and it disappears.
            </Typography>
          </AccordionSummary>
          <AccordionDetails>
            {prep.map((c) => (
              <Box key={c} sx={{ py: 0.4 }}>
                <Typography variant="body2">
                  <b>{model.title(c)}</b>
                </Typography>
                <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                  {model.get(c)?.desc}
                </Typography>
              </Box>
            ))}
          </AccordionDetails>
        </Accordion>
      )}
    </Stack>
  );
}
