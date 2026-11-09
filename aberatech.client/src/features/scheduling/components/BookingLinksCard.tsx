import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Divider from "@mui/material/Divider";
import FormControlLabel from "@mui/material/FormControlLabel";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import Switch from "@mui/material/Switch";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import { linkUrl } from "../core/link";
import {
  type BookingLink,
  type LinkRules,
  type NewKey,
  useBookingLinks,
} from "../hooks/useBookingLinks";

const units = [
  { label: "minutes", minutes: 1 },
  { label: "hours", minutes: 60 },
  { label: "days", minutes: 24 * 60 },
] as const;

/** The largest unit that divides the stored minutes, so 720 reads as 12 hours. */
function splitMinutes(total: number): { amount: number; unit: number } {
  for (let index = units.length - 1; index > 0; index--) {
    if (total % units[index].minutes === 0 && total > 0) {
      return { amount: total / units[index].minutes, unit: index };
    }
  }
  return { amount: total, unit: 0 };
}

const blankRules: LinkRules = {
  name: "",
  earliestDate: null,
  horizonDays: 21,
  leadMinutes: 30,
  appointmentMinutes: 30,
  showsQueue: false,
  active: true,
};

/**
 * Booking links: one per audience, each with its own rules.
 *
 * The rules decide which times a visitor holding the link is offered, and the
 * server checks every booking against them. A link carries a key the site
 * keeps only as a hash, so the full link is shown once, when it is made.
 */
export default function BookingLinksCard({ enabled }: { enabled: boolean }) {
  const { list, error, create, update, saveDefault, newKey } =
    useBookingLinks(enabled);
  const [shown, setShown] = React.useState<NewKey | null>(null);
  const [adding, setAdding] = React.useState(false);

  if (!enabled || !list) {
    return error ? <Alert severity="error">{error}</Alert> : null;
  }

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={2}>
          <Box>
            <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
              Booking links
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              Give each audience its own link. A link offers times between its
              first day and its last, inside your hours. Plain /schedule, the
              link in your signature, uses the first row.
            </Typography>
          </Box>

          {error ? <Alert severity="error">{error}</Alert> : null}

          {shown ? (
            <ShownKey shown={shown} onDone={() => setShown(null)} />
          ) : null}

          <LinkRow link={list.default} onSave={(rules) => saveDefault(rules)} />

          {list.links.map((link) => (
            <React.Fragment key={link.id}>
              <Divider />
              <LinkRow
                link={link}
                onSave={(rules) => update(link.id as string, rules)}
                onNewKey={async () => {
                  const made = await newKey(link.id as string);
                  if (typeof made === "string") return { error: made };
                  setShown(made);
                  return { error: null };
                }}
              />
            </React.Fragment>
          ))}

          <Divider />
          {adding ? (
            <LinkRow
              link={{ ...blankRules, id: null, isDefault: false, bookings: 0 }}
              isNew
              onSave={async (rules) => {
                const made = await create(rules);
                if (typeof made === "string") return { error: made };
                setShown(made);
                setAdding(false);
                return { error: null };
              }}
              onCancel={() => setAdding(false)}
            />
          ) : (
            <Box>
              <Button variant="outlined" onClick={() => setAdding(true)}>
                New link
              </Button>
            </Box>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
}

/** The full link, once. The site keeps only its hash. */
function ShownKey({ shown, onDone }: { shown: NewKey; onDone: () => void }) {
  const url = linkUrl(window.location.origin, shown.key);
  const [copied, setCopied] = React.useState(false);

  return (
    <Alert
      severity="success"
      action={
        <Button color="inherit" size="small" onClick={onDone}>
          Done
        </Button>
      }
    >
      <Stack spacing={1}>
        <Typography variant="body2">
          The link for {shown.link.name}. Copy it now: the site keeps no copy of
          its key.
        </Typography>
        <TextField
          value={url}
          size="small"
          fullWidth
          label="Link"
          slotProps={{ htmlInput: { readOnly: true } }}
          onFocus={(event) => event.target.select()}
        />
        <Box>
          <Button
            size="small"
            variant="contained"
            onClick={async () => {
              await navigator.clipboard.writeText(url);
              setCopied(true);
            }}
          >
            {copied ? "Copied" : "Copy link"}
          </Button>
        </Box>
      </Stack>
    </Alert>
  );
}

interface LinkRowProps {
  link: BookingLink;
  isNew?: boolean;
  onSave: (rules: LinkRules) => Promise<{ error: string | null }>;
  onNewKey?: () => Promise<{ error: string | null }>;
  onCancel?: () => void;
}

function LinkRow({ link, isNew, onSave, onNewKey, onCancel }: LinkRowProps) {
  const [rules, setRules] = React.useState<LinkRules>(link);
  const lead = splitMinutes(link.leadMinutes);
  const [leadAmount, setLeadAmount] = React.useState(String(lead.amount));
  const [leadUnit, setLeadUnit] = React.useState(lead.unit);
  const [status, setStatus] = React.useState<string | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  const leadMinutes = () =>
    Math.round(Number(leadAmount) * units[leadUnit].minutes);

  const save = async (next: LinkRules): Promise<boolean> => {
    setStatus("Saving…");
    setError(null);
    const result = await onSave(next);
    if (result.error) {
      setError(result.error);
      setStatus(null);
      return false;
    }
    setStatus("Saved");
    setTimeout(() => setStatus(null), 2000);
    return true;
  };

  const title = link.isDefault ? "Plain /schedule, no link" : link.name;
  const label = isNew ? "new link" : title;

  return (
    <Stack spacing={1.5} aria-label={`Rules for ${label}`} role="group">
      <Box
        sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}
      >
        <Typography variant="body1" sx={{ fontWeight: 600, flexGrow: 1 }}>
          {isNew ? "New link" : title}
        </Typography>
        {isNew ? null : (
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {link.bookings} {link.bookings === 1 ? "booking" : "bookings"}
          </Typography>
        )}
        {link.isDefault || isNew ? null : (
          <FormControlLabel
            label="On"
            control={
              <Switch
                checked={rules.active}
                onChange={(event) => {
                  // Moves at once and saves. A refusal puts it back.
                  const before = rules;
                  const next = { ...rules, active: event.target.checked };
                  setRules(next);
                  void save(next).then((ok) => {
                    if (!ok) setRules(before);
                  });
                }}
              />
            }
          />
        )}
      </Box>

      {error ? <Alert severity="error">{error}</Alert> : null}

      <Box
        sx={{
          display: "grid",
          gap: 1.5,
          gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" },
        }}
      >
        {link.isDefault ? null : (
          <TextField
            label="Name, seen only by you"
            size="small"
            value={rules.name}
            onChange={(event) =>
              setRules({ ...rules, name: event.target.value })
            }
            slotProps={{ htmlInput: { maxLength: 120 } }}
          />
        )}
        <TextField
          label="First day"
          type="date"
          size="small"
          value={rules.earliestDate ?? ""}
          onChange={(event) =>
            setRules({ ...rules, earliestDate: event.target.value || null })
          }
          helperText="Empty is today. A date on your calendar, in your hours' zone."
          slotProps={{ inputLabel: { shrink: true } }}
        />
        <TextField
          label="Days ahead"
          type="number"
          size="small"
          value={rules.horizonDays}
          onChange={(event) =>
            setRules({ ...rules, horizonDays: Number(event.target.value) })
          }
          helperText="The last day offered, counted from today."
          slotProps={{ htmlInput: { min: 1, max: 365 } }}
        />
        <Box sx={{ display: "flex", gap: 1 }}>
          <TextField
            label="Notice"
            type="number"
            size="small"
            value={leadAmount}
            onChange={(event) => setLeadAmount(event.target.value)}
            helperText="How soon a time can start."
            sx={{ flexGrow: 1 }}
            slotProps={{ htmlInput: { min: 0 } }}
          />
          <TextField
            select
            size="small"
            label="Unit"
            value={leadUnit}
            onChange={(event) => setLeadUnit(Number(event.target.value))}
            sx={{ width: 120 }}
          >
            {units.map((unit, index) => (
              <MenuItem key={unit.label} value={index}>
                {unit.label}
              </MenuItem>
            ))}
          </TextField>
        </Box>
        <TextField
          label="Meeting length, minutes"
          type="number"
          size="small"
          value={rules.appointmentMinutes}
          onChange={(event) =>
            setRules({
              ...rules,
              appointmentMinutes: Number(event.target.value),
            })
          }
          slotProps={{ htmlInput: { min: 5, max: 240 } }}
        />
      </Box>

      <FormControlLabel
        label="When a queue is open, show the queue instead of times"
        control={
          <Switch
            checked={rules.showsQueue}
            onChange={(event) =>
              setRules({ ...rules, showsQueue: event.target.checked })
            }
          />
        }
      />

      <Box
        sx={{ display: "flex", gap: 1, alignItems: "center", flexWrap: "wrap" }}
      >
        <Button
          variant="contained"
          size="small"
          onClick={() => void save({ ...rules, leadMinutes: leadMinutes() })}
        >
          {isNew ? "Make the link" : "Save"}
        </Button>
        {onCancel ? (
          <Button size="small" color="inherit" onClick={onCancel}>
            Cancel
          </Button>
        ) : null}
        {onNewKey ? (
          <Button
            size="small"
            color="inherit"
            onClick={async () => {
              const result = await onNewKey();
              if (result.error) setError(result.error);
            }}
          >
            New key
          </Button>
        ) : null}
        {status ? (
          <Typography
            variant="caption"
            sx={{ color: "text.secondary" }}
            role="status"
          >
            {status}
          </Typography>
        ) : null}
      </Box>
    </Stack>
  );
}
