/**
 * Everything that changes the plan, in one column, with the count each control
 * affects sitting next to it rather than a screen away.
 */
import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Checkbox from '@mui/material/Checkbox';
import Divider from '@mui/material/Divider';
import FormControlLabel from '@mui/material/FormControlLabel';
import MenuItem from '@mui/material/MenuItem';
import Stack from '@mui/material/Stack';
import Switch from '@mui/material/Switch';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { areaColor } from '../core/areaColors';
import { holdsBackground, isComposite, missingParts } from '../core/background';
import { MAX_PER_TERM } from '../model/PlannerModel';
import type { PlannerModel } from '../model/PlannerModel';
import TrackPicker from './TrackPicker';

export interface SettingsRailProps {
  model: PlannerModel;
  mode: 'light' | 'dark';
  update: (fn: (model: PlannerModel) => void) => void;
}

const START_OPTIONS = [2027, 2028, 2029].flatMap((y) => ['Spring', 'Summer', 'Fall'].map((t) => `${t} ${y}`));

export default function SettingsRail({ model, mode, update }: SettingsRailProps) {
  return (
    <Stack spacing={2.5}>
      <Section title="Recommended tracks">
        <TrackPicker
          model={model}
          onSelect={(id) => {
            update((m) => {
              m.selectTrack(id);
            });
          }}
        />
      </Section>

      <Section title="Focus areas" hint="Turning one on replaces any track you had selected.">
        {Object.keys(model.data.areas).map((name) => (
          <AreaToggle
            key={name}
            name={name}
            colour={areaColor(name, mode)}
            count={model.data.areas[name].filter((c) => model.data.get(c)).length}
            checked={model.areas.has(name)}
            onChange={() => {
              update((m) => {
                m.toggleArea('area', name);
              });
            }}
          />
        ))}
      </Section>

      <Section title="Transcript concentrations">
        {Object.keys(model.data.concentrations).map((name) => (
          <AreaToggle
            key={name}
            name={name.replace(' (transcript)', '')}
            colour={areaColor('Computer Engineering', mode)}
            count={model.data.concentrations[name].filter((c) => model.data.get(c)).length}
            checked={model.conc.has(name)}
            onChange={() => {
              update((m) => {
                m.toggleArea('conc', name);
              });
            }}
          />
        ))}
      </Section>

      <Section
        title="Background you already have"
        hint="A subject left unticked becomes a preparation course in the plan, scheduled like any other prerequisite. A whole degree is not scheduled: it ticks itself once its parts are ticked, and the courses that assume one say so on their card."
      >
        {model.data.background.map(([id, label]) => {
          const composite = isComposite(id);
          const parts = composite ? missingParts(id, model.background) : [];
          const satisfied = holdsBackground(id, model.background);
          return (
            <FormControlLabel
              key={id}
              sx={{ display: 'flex', ml: 0, alignItems: 'flex-start' }}
              control={
                <Checkbox
                  size="small"
                  checked={satisfied}
                  // A composite is a summary of the rows above it. Ticking it
                  // outright is still allowed, because someone who holds the
                  // degree should not have to tick seven boxes to say so.
                  indeterminate={composite && !model.background.has(id) && satisfied}
                  onChange={(e) => {
                    update((m) => {
                      m.setBackground(id, e.target.checked);
                    });
                  }}
                />
              }
              label={
                <Box>
                  <Typography component="span" variant="body2">
                    {label}
                  </Typography>
                  {composite && parts.length > 0 && (
                    <Typography variant="caption" sx={{ display: 'block', color: 'text.secondary' }}>
                      {parts.length} of its parts still unticked
                    </Typography>
                  )}
                </Box>
              }
            />
          );
        })}
      </Section>

      <Section title="Schedule">
        <Stack spacing={1.5}>
          <TextField
            size="small"
            type="number"
            label="Courses per term"
            value={model.perTerm}
            slotProps={{ htmlInput: { min: 1, max: MAX_PER_TERM } }}
            helperText={`One to ${MAX_PER_TERM}. Over the limit is allowed on the board and flagged, never blocked.`}
            onChange={(e) => {
              update((m) => {
                m.setPerTerm(Number(e.target.value));
              });
            }}
          />
          <TextField
            select
            size="small"
            label="Terms per year"
            value={model.termsPerYear}
            onChange={(e) => {
              update((m) => {
                m.termsPerYear = Number(e.target.value);
              });
            }}
          >
            <MenuItem value={3}>Three, including summer</MenuItem>
            <MenuItem value={2}>Two, spring and fall</MenuItem>
          </TextField>
          <TextField
            select
            size="small"
            label="First term"
            value={`${model.startTerm} ${model.startYear}`}
            onChange={(e) => {
              const [t, y] = e.target.value.split(' ');
              update((m) => {
                m.startTerm = t;
                m.startYear = Number(y);
              });
            }}
          >
            {START_OPTIONS.map((s) => (
              <MenuItem key={s} value={s}>
                {s}
              </MenuItem>
            ))}
          </TextField>
        </Stack>
      </Section>

      <Section title="The five year clock">
        <Stack spacing={1.5}>
          <TextField
            size="small"
            type="number"
            label="Leave of absence, months"
            value={model.leaveMonths}
            slotProps={{ htmlInput: { min: 0, max: 24 } }}
            onChange={(e) => {
              update((m) => {
                m.leaveMonths = Number(e.target.value);
              });
            }}
          />
          <TextField
            size="small"
            type="number"
            label="Time limitation exception, months"
            value={model.extensionMonths}
            slotProps={{ htmlInput: { min: 0, max: 24 } }}
            onChange={(e) => {
              update((m) => {
                m.extensionMonths = Number(e.target.value);
              });
            }}
          />
        </Stack>
      </Section>

      <Section title="Behaviour">
        <FormControlLabel
          sx={{ display: 'flex', ml: 0 }}
          control={
            <Switch
              size="small"
              checked={model.autoPrereq}
              onChange={(e) => {
                update((m) => {
                  m.autoPrereq = e.target.checked;
                  m.pruneToSelection();
                });
              }}
            />
          }
          label={
            <Typography component="span" variant="body2">
              Pull in prerequisites automatically
            </Typography>
          }
        />
        <FormControlLabel
          sx={{ display: 'flex', ml: 0 }}
          control={
            <Switch
              size="small"
              checked={model.autoOnDrop}
              onChange={(e) => {
                update((m) => {
                  m.autoOnDrop = e.target.checked;
                });
              }}
            />
          }
          label={
            <Typography component="span" variant="body2">
              Insert prerequisites when a blocked course is dropped
            </Typography>
          }
        />
        <Stack direction="row" spacing={1} sx={{ mt: 1.5 }}>
          <Button
            size="small"
            variant="outlined"
            onClick={() => {
              update((m) => {
                m.rescheduleAll();
              });
            }}
          >
            Reschedule
          </Button>
          <Button
            size="small"
            color="inherit"
            onClick={() => {
              update((m) => {
                m.clearPlan();
              });
            }}
          >
            Clear the board
          </Button>
        </Stack>
      </Section>
    </Stack>
  );
}

function Section({ title, hint, children }: { title: string; hint?: string; children: ReactNode }) {
  return (
    <Box>
      <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
        {title}
      </Typography>
      {hint && (
        <Typography variant="caption" sx={{ color: 'text.disabled', display: 'block', mb: 0.75 }}>
          {hint}
        </Typography>
      )}
      <Divider sx={{ my: 1 }} />
      {children}
    </Box>
  );
}

function AreaToggle({
  name,
  colour,
  count,
  checked,
  onChange
}: {
  name: string;
  colour: string;
  count: number;
  checked: boolean;
  onChange: () => void;
}) {
  return (
    <FormControlLabel
      sx={{ display: 'flex', ml: 0, mr: 0 }}
      control={<Checkbox size="small" checked={checked} onChange={onChange} />}
      label={
        <Stack component="span" direction="row" spacing={1} alignItems="center" sx={{ width: '100%' }}>
          <Box
            component="span"
            sx={{ width: 10, height: 10, borderRadius: '50%', backgroundColor: colour, flexShrink: 0 }}
          />
          <Typography component="span" variant="body2" sx={{ flexGrow: 1 }}>
            {name}
          </Typography>
          <Typography component="span" variant="caption" sx={{ color: 'text.disabled' }}>
            {count}
          </Typography>
        </Stack>
      }
    />
  );
}
