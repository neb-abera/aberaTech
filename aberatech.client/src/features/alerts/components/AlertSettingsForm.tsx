import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import FormControlLabel from "@mui/material/FormControlLabel";
import InputAdornment from "@mui/material/InputAdornment";
import Stack from "@mui/material/Stack";
import Switch from "@mui/material/Switch";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import {
  type ActionResult,
  type AlertSettings,
  type AlertsState,
  type PushoverSound,
  type SettingsBounds,
  saveAlertSettings,
} from "../core/api";
import {
  count,
  isNonstop,
  longSounds,
  nonstop,
  stopWork,
} from "../core/settings";

const notificationPriorities = [
  {
    value: 0,
    label: "Normal",
    text: "One sound, follows the phone's settings.",
  },
  {
    value: 1,
    label: "High",
    text: "One sound, even during Pushover's quiet hours.",
  },
] as const;

const defaultTypes = [
  { value: "none", label: "Off", text: "Nothing is sent." },
  {
    value: "notification",
    label: "Ring once",
    text: "Rings once, with the settings above.",
  },
] as const;

const repeatPresets = [
  { seconds: 30, label: "30 s" },
  { seconds: 60, label: "1 min" },
  { seconds: 120, label: "2 min" },
  { seconds: 300, label: "5 min" },
];

/**
 * A sound select's options: the phone's default, the sounds uploaded to the
 * Pushover account under "Your sounds", then Pushover's own. A saved sound
 * the list no longer names (Pushover could not be reached) stays choosable.
 */
function soundOptions(sounds: PushoverSound[], value: string) {
  const custom = sounds.filter((sound) => sound.custom);
  const builtIn = sounds.filter((sound) => !sound.custom);
  const listed = value === "" || sounds.some((sound) => sound.name === value);
  return (
    <>
      <option value="">Phone's default</option>
      {(custom.length > 0 || !listed) && (
        <optgroup label="Your sounds">
          {custom.map((sound) => (
            <option key={sound.name} value={sound.name}>
              {sound.description === sound.name
                ? sound.name
                : `${sound.description} (${sound.name})`}
            </option>
          ))}
          {!listed && <option value={value}>{value}</option>}
        </optgroup>
      )}
      <optgroup label="Pushover's sounds">
        {builtIn.map((sound) => (
          <option key={sound.name} value={sound.name}>
            {(longSounds as readonly string[]).includes(sound.name)
              ? `${sound.name} (long)`
              : sound.name}
          </option>
        ))}
      </optgroup>
    </>
  );
}

/** The form as typed: numbers stay text until Save, so a half-typed value is allowed. */
interface Draft {
  repeatSeconds: string;
  stopAfterMinutes: string;
  sound: string;
  defaultLeadMinutes: string;
  pollMinutes: string;
  lookaheadHours: string;
  includeAllDay: boolean;
  timeZone: string;
  ownerEmails: string;
  notificationPriority: 0 | 1;
  notificationSound: string;
  defaultType: "none" | "notification";
  backupDelaySeconds: string;
  phoneSound: string;
  phoneSnoozeMinutes: string;
}

function toDraft(settings: AlertSettings): Draft {
  return {
    ...settings,
    repeatSeconds: `${settings.repeatSeconds}`,
    stopAfterMinutes: `${settings.stopAfterMinutes}`,
    defaultLeadMinutes: `${settings.defaultLeadMinutes}`,
    pollMinutes: `${settings.pollMinutes}`,
    lookaheadHours: `${settings.lookaheadHours}`,
    backupDelaySeconds: `${settings.backupDelaySeconds}`,
    phoneSnoozeMinutes: `${settings.phoneSnoozeMinutes}`,
    ownerEmails: settings.ownerEmails.join(", "),
  };
}

/** A blank or non-numeric field goes as null, and the server names it. */
function number(text: string): number {
  return text.trim() === "" ? Number.NaN : Number(text);
}

function fromDraft(draft: Draft): AlertSettings {
  return {
    repeatSeconds: number(draft.repeatSeconds),
    stopAfterMinutes: number(draft.stopAfterMinutes),
    sound: draft.sound,
    defaultLeadMinutes: number(draft.defaultLeadMinutes),
    pollMinutes: number(draft.pollMinutes),
    lookaheadHours: number(draft.lookaheadHours),
    includeAllDay: draft.includeAllDay,
    timeZone: draft.timeZone.trim(),
    ownerEmails: draft.ownerEmails
      .split(/[\s,;]+/)
      .filter((email) => email.length > 0),
    notificationPriority: draft.notificationPriority,
    notificationSound: draft.notificationSound,
    defaultType: draft.defaultType,
    backupDelaySeconds: number(draft.backupDelaySeconds),
    phoneSound: draft.phoneSound,
    phoneSnoozeMinutes: number(draft.phoneSnoozeMinutes),
  };
}

function same(a: AlertSettings, b: AlertSettings): boolean {
  return (
    a.repeatSeconds === b.repeatSeconds &&
    a.stopAfterMinutes === b.stopAfterMinutes &&
    a.sound === b.sound &&
    a.defaultLeadMinutes === b.defaultLeadMinutes &&
    a.pollMinutes === b.pollMinutes &&
    a.lookaheadHours === b.lookaheadHours &&
    a.includeAllDay === b.includeAllDay &&
    a.timeZone === b.timeZone &&
    a.ownerEmails.join(",") === b.ownerEmails.join(",") &&
    a.notificationPriority === b.notificationPriority &&
    a.notificationSound === b.notificationSound &&
    a.defaultType === b.defaultType &&
    a.backupDelaySeconds === b.backupDelaySeconds &&
    a.phoneSound === b.phoneSound &&
    a.phoneSnoozeMinutes === b.phoneSnoozeMinutes
  );
}

/**
 * The settings section of /alerts. Everything but the three secrets, saved
 * as one row and in force from the next pass of the worker. Alarms, then
 * notifications, then what an unmarked event sends, then the paired phone,
 * then the calendar.
 */
export default function AlertSettingsForm({
  settings,
  bounds,
  onSaved,
  save = saveAlertSettings,
}: {
  settings: AlertSettings;
  bounds: SettingsBounds;
  onSaved: (state: AlertsState) => void;
  save?: (settings: AlertSettings) => Promise<ActionResult>;
}) {
  // Null until the owner edits: the form shows what the server holds, and
  // follows the page's refresh until then.
  const [draft, setDraft] = React.useState<Draft | null>(null);
  const [saving, setSaving] = React.useState(false);
  const [errors, setErrors] = React.useState<Record<string, string[]>>({});
  const [outcome, setOutcome] = React.useState<{
    severity: "success" | "warning";
    text: string;
  } | null>(null);

  const shown = draft ?? toDraft(settings);
  const changed = draft !== null && !same(fromDraft(draft), settings);

  const edit = (change: Partial<Draft>) => {
    setDraft({ ...shown, ...change });
    setOutcome(null);
  };

  const submit = async () => {
    if (draft === null) return;
    setSaving(true);
    setOutcome(null);
    const result = await save(fromDraft(draft));
    setSaving(false);
    if (result.ok) {
      setErrors({});
      setDraft(null);
      if (result.state) onSaved(result.state);
      setOutcome({ severity: "success", text: "Settings saved." });
      return;
    }
    if (result.reason === "invalid") {
      setErrors(result.errors ?? {});
      setOutcome({
        severity: "warning",
        text: "Not saved. Check the fields marked below.",
      });
      return;
    }
    setOutcome({
      severity: "warning",
      text:
        result.reason === "throttled"
          ? "Not saved. Too many presses. Wait a minute."
          : result.reason === "visitor"
            ? "Not saved. The session expired. Reload and sign in again."
            : "Not saved. The server did not take it. Try again.",
    });
  };

  const error = (field: string) => errors[field]?.[0];
  const numberField = (
    field: keyof Draft,
    label: string,
    unit: string,
    bound: { min: number; max: number },
    hint: string,
  ) => (
    <TextField
      label={label}
      type="number"
      size="small"
      value={shown[field] as string}
      onChange={(event) => edit({ [field]: event.target.value })}
      error={Boolean(error(field))}
      helperText={error(field) ?? hint}
      slotProps={{
        htmlInput: { min: bound.min, max: bound.max, inputMode: "numeric" },
        input: {
          endAdornment: <InputAdornment position="end">{unit}</InputAdornment>,
        },
      }}
      sx={{ width: "14rem", maxWidth: "100%" }}
    />
  );

  /** The helper under a field: what it does at the typed value, or its bounds while it is blank. */
  const explain = (
    text: string,
    unit: string,
    bound: { min: number; max: number },
    say: (value: string) => string,
  ) => {
    const value = number(text);
    return Number.isInteger(value) && value >= bound.min && value <= bound.max
      ? say(count(value, unit))
      : `${bound.min} to ${bound.max} ${unit}s.`;
  };

  const repeat = number(shown.repeatSeconds);
  const stop = number(shown.stopAfterMinutes);
  const work =
    Number.isFinite(repeat) && Number.isFinite(stop) && repeat > 0 && stop > 0
      ? stopWork(repeat, stop, bounds.maxEmergencySounds).text
      : null;

  return (
    <Box component="section" aria-labelledby="alert-settings-heading">
      <Typography
        id="alert-settings-heading"
        variant="h2"
        sx={{ fontSize: "1.25rem", mb: 1 }}
      >
        Settings
      </Typography>
      <Stack spacing={2.5}>
        <Typography variant="h3" sx={{ fontSize: "1.05rem", fontWeight: 600 }}>
          Ring until stopped settings
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Events marked #critical ring this way too.
        </Typography>
        <Stack spacing={1}>
          <Stack
            direction="row"
            spacing={1}
            useFlexGap
            sx={{ alignItems: "flex-start", flexWrap: "wrap" }}
          >
            {numberField(
              "repeatSeconds",
              "Pushover repeats every",
              "s",
              bounds.repeatSeconds,
              `Up to ${bounds.repeatSeconds.max} seconds`,
            )}
            {numberField(
              "stopAfterMinutes",
              "Pushover stops after",
              "min",
              bounds.stopAfterMinutes,
              `${bounds.stopAfterMinutes.min} to ${bounds.stopAfterMinutes.max} minutes`,
            )}
          </Stack>
          <Stack
            direction="row"
            spacing={1}
            useFlexGap
            sx={{ flexWrap: "wrap" }}
          >
            {repeatPresets.map((preset) => (
              <Chip
                key={preset.seconds}
                size="small"
                label={preset.label}
                aria-label={`Repeat every ${preset.label}`}
                color={repeat === preset.seconds ? "primary" : "default"}
                variant={repeat === preset.seconds ? "filled" : "outlined"}
                onClick={() => edit({ repeatSeconds: `${preset.seconds}` })}
              />
            ))}
            <Chip
              size="small"
              label="Nonstop"
              aria-pressed={isNonstop(repeat, shown.sound)}
              color={isNonstop(repeat, shown.sound) ? "primary" : "default"}
              variant={isNonstop(repeat, shown.sound) ? "filled" : "outlined"}
              onClick={() =>
                edit({
                  repeatSeconds: `${nonstop.repeatSeconds}`,
                  sound: nonstop.sound,
                })
              }
            />
          </Stack>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Pushover does not repeat faster than every{" "}
            {bounds.repeatSeconds.min} s. A paired phone rings as an iPhone
            alarm until you press Stop. Ring in this browser beeps every second
            until you acknowledge it.
          </Typography>
          {work && (
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              {work}
            </Typography>
          )}
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            A long sound plays into the gap between two Pushover repeats. iOS
            plays a notification sound for up to 30 s, the length of the{" "}
            {bounds.repeatSeconds.min} s repeat. Nonstop sets{" "}
            {nonstop.repeatSeconds} s and {nonstop.sound}, one of Pushover's{" "}
            {longSounds.length} long sounds. Pushover stops after{" "}
            {bounds.maxEmergencySounds} repeats: {bounds.maxEmergencySounds} ×{" "}
            {nonstop.repeatSeconds} s ={" "}
            {(bounds.maxEmergencySounds * nonstop.repeatSeconds) / 60} min.
          </Typography>
        </Stack>

        {numberField(
          "backupDelaySeconds",
          "Pushover backup after",
          "s",
          bounds.backupDelaySeconds,
          explain(
            shown.backupDelaySeconds,
            "second",
            bounds.backupDelaySeconds,
            (delay) =>
              delay.startsWith("0 ")
                ? "Pushover sends at the alert's time, with the paired phones."
                : `A paired phone rings first. Pushover follows ${delay} later if nobody acknowledged it, and no later than 1 minute before the start.`,
          ),
        )}

        <TextField
          select
          label="Sound"
          size="small"
          value={shown.sound}
          onChange={(event) => edit({ sound: event.target.value })}
          error={Boolean(error("sound"))}
          helperText={
            error("sound") ??
            "Your sounds first, then Pushover's. A long one plays for longer than one chime."
          }
          slotProps={{ select: { native: true }, inputLabel: { shrink: true } }}
          sx={{ width: "14rem", maxWidth: "100%" }}
        >
          {soundOptions(bounds.sounds, shown.sound)}
        </TextField>

        <Typography variant="h3" sx={{ fontSize: "1.05rem", fontWeight: 600 }}>
          Ring once settings
        </Typography>
        <Box component="fieldset" sx={{ border: 0, p: 0, m: 0 }}>
          <Typography component="legend" variant="body1" sx={{ mb: 1 }}>
            Ring once priority
          </Typography>
          <Stack spacing={1}>
            {notificationPriorities.map((option) => (
              <Stack
                key={option.value}
                direction="row"
                spacing={1.5}
                sx={{ alignItems: "center" }}
              >
                <Chip
                  label={option.label}
                  aria-label={`Ring once priority ${option.label}`}
                  color={
                    shown.notificationPriority === option.value
                      ? "primary"
                      : "default"
                  }
                  variant={
                    shown.notificationPriority === option.value
                      ? "filled"
                      : "outlined"
                  }
                  aria-pressed={shown.notificationPriority === option.value}
                  onClick={() => edit({ notificationPriority: option.value })}
                  sx={{ minWidth: "6.5rem" }}
                />
                <Typography variant="body2" sx={{ color: "text.secondary" }}>
                  {option.text}
                </Typography>
              </Stack>
            ))}
          </Stack>
          {error("notificationPriority") && (
            <Typography variant="body2" color="error" sx={{ mt: 1 }}>
              {error("notificationPriority")}
            </Typography>
          )}
        </Box>

        <TextField
          select
          label="Ring once sound"
          size="small"
          value={shown.notificationSound}
          onChange={(event) => edit({ notificationSound: event.target.value })}
          error={Boolean(error("notificationSound"))}
          helperText={
            error("notificationSound") ??
            "Ring once never repeats, so a long sound plays once."
          }
          slotProps={{ select: { native: true }, inputLabel: { shrink: true } }}
          sx={{ width: "14rem", maxWidth: "100%" }}
        >
          {soundOptions(bounds.sounds, shown.notificationSound)}
        </TextField>

        <Box component="fieldset" sx={{ border: 0, p: 0, m: 0 }}>
          <Typography component="legend" variant="body1" sx={{ mb: 1 }}>
            Events with no mark and no type set here
          </Typography>
          <Stack spacing={1}>
            {defaultTypes.map((option) => (
              <Stack
                key={option.value}
                direction="row"
                spacing={1.5}
                sx={{ alignItems: "center" }}
              >
                <Chip
                  label={option.label}
                  aria-label={`Unmarked events: ${option.label}`}
                  color={
                    shown.defaultType === option.value ? "primary" : "default"
                  }
                  variant={
                    shown.defaultType === option.value ? "filled" : "outlined"
                  }
                  aria-pressed={shown.defaultType === option.value}
                  onClick={() => edit({ defaultType: option.value })}
                  sx={{ minWidth: "6.5rem" }}
                />
                <Typography variant="body2" sx={{ color: "text.secondary" }}>
                  {option.text}
                </Typography>
              </Stack>
            ))}
          </Stack>
          {error("defaultType") && (
            <Typography variant="body2" color="error" sx={{ mt: 1 }}>
              {error("defaultType")}
            </Typography>
          )}
        </Box>

        <Typography variant="h3" sx={{ fontSize: "1.05rem", fontWeight: 600 }}>
          On the phone
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          The phone plays this sound for every alarm it rings. Snooze delays a
          calendar alarm by this many minutes without acknowledging it.
        </Typography>
        <Stack
          direction="row"
          spacing={1}
          useFlexGap
          sx={{ alignItems: "flex-start", flexWrap: "wrap" }}
        >
          <TextField
            select
            label="Alarm sound"
            size="small"
            value={shown.phoneSound}
            onChange={(event) => edit({ phoneSound: event.target.value })}
            error={Boolean(error("phoneSound"))}
            helperText={error("phoneSound")}
            slotProps={{
              select: { native: true },
              inputLabel: { shrink: true },
            }}
            sx={{ width: "14rem", maxWidth: "100%" }}
          >
            {bounds.phoneSounds.map((sound) => (
              <option key={sound.value} value={sound.value}>
                {sound.label}
              </option>
            ))}
          </TextField>
          {numberField(
            "phoneSnoozeMinutes",
            "Snooze",
            "min",
            bounds.phoneSnoozeMinutes,
            `${bounds.phoneSnoozeMinutes.min} to ${bounds.phoneSnoozeMinutes.max} minutes`,
          )}
        </Stack>

        <Typography variant="h3" sx={{ fontSize: "1.05rem", fontWeight: 600 }}>
          Calendar
        </Typography>

        <Stack
          direction="row"
          spacing={1}
          useFlexGap
          sx={{ alignItems: "flex-start", flexWrap: "wrap" }}
        >
          {numberField(
            "defaultLeadMinutes",
            "Default lead",
            "min",
            bounds.defaultLeadMinutes,
            explain(
              shown.defaultLeadMinutes,
              "minute",
              bounds.defaultLeadMinutes,
              (lead) =>
                `An event with no notification of its own alerts ${lead} before it starts.`,
            ),
          )}
          {numberField(
            "pollMinutes",
            "Check calendar every",
            "min",
            bounds.pollMinutes,
            explain(
              shown.pollMinutes,
              "minute",
              bounds.pollMinutes,
              (poll) =>
                `The server reads the calendar every ${poll}. A new or moved event shows up within ${poll}.`,
            ),
          )}
          {numberField(
            "lookaheadHours",
            "Look ahead",
            "h",
            bounds.lookaheadHours,
            explain(
              shown.lookaheadHours,
              "hour",
              bounds.lookaheadHours,
              (hours) =>
                `Events that start in the next ${hours} are planned and listed above.`,
            ),
          )}
        </Stack>

        <FormControlLabel
          control={
            <Switch
              checked={shown.includeAllDay}
              onChange={(event) =>
                edit({ includeAllDay: event.target.checked })
              }
            />
          }
          label="Alert for all-day events"
        />

        <TextField
          label="Time zone when the calendar names none"
          size="small"
          value={shown.timeZone}
          onChange={(event) => edit({ timeZone: event.target.value })}
          error={Boolean(error("timeZone"))}
          helperText={
            error("timeZone") ??
            "Blank is UTC. A time zone database name, such as America/New_York."
          }
          sx={{ maxWidth: "28rem" }}
        />

        <TextField
          label="Your addresses, for declined invitations"
          size="small"
          value={shown.ownerEmails}
          onChange={(event) => edit({ ownerEmails: event.target.value })}
          error={Boolean(error("ownerEmails"))}
          helperText={
            error("ownerEmails") ??
            `Separated by commas, up to ${bounds.maxOwnerEmails}. Needed only for a secondary calendar.`
          }
          sx={{ maxWidth: "28rem" }}
        />

        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          The calendar address and the Pushover keys stay container secrets and
          cannot be changed here. Typed into a page, a key would pass through
          the browser and the database.
        </Typography>

        {outcome && (
          <Alert severity={outcome.severity} onClose={() => setOutcome(null)}>
            {outcome.text}
          </Alert>
        )}

        <Box>
          <Button
            variant="contained"
            disabled={!changed || saving}
            onClick={() => void submit()}
          >
            {saving ? "Saving" : "Save settings"}
          </Button>
        </Box>
      </Stack>
    </Box>
  );
}
