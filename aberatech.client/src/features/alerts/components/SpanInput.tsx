import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import * as React from "react";

/** The units a length or a reminder is typed in, in minutes each. */
export const spanUnits = { minutes: 1, hours: 60, days: 1440 } as const;
export type SpanUnit = keyof typeof spanUnits;

/** The largest unit that holds the minutes whole: 90 is 90 minutes, 120 is 2 hours. */
export function splitMinutes(minutes: string): {
  amount: string;
  unit: SpanUnit;
} {
  const value = Number(minutes);
  if (minutes.trim() === "" || !Number.isInteger(value) || value === 0) {
    return { amount: minutes, unit: "minutes" };
  }
  const unit =
    (["days", "hours"] as const).find((u) => value % spanUnits[u] === 0) ??
    "minutes";
  return { amount: String(value / spanUnits[unit]), unit };
}

/** The minutes a typed amount and unit make, or "" for an empty amount. */
export function joinMinutes(amount: string, unit: SpanUnit): string {
  return amount.trim() === "" ? "" : String(Number(amount) * spanUnits[unit]);
}

/**
 * A length typed as a number with its unit beside it. The value in and
 * out is in minutes, as a string, so an empty field stays empty.
 */
export default function SpanInput({
  label,
  minutes,
  onChange,
  error,
  helperText,
  placeholder,
}: {
  label: string;
  minutes: string;
  onChange: (minutes: string) => void;
  error?: boolean;
  helperText?: string;
  placeholder?: string;
}) {
  const [unit, setUnit] = React.useState<SpanUnit>(
    () => splitMinutes(minutes).unit,
  );
  const [amount, setAmount] = React.useState(
    () => splitMinutes(minutes).amount,
  );
  // A reset from outside, such as a cleared form, starts over. Only the
  // value from outside decides, so the typed amount and unit are left out.
  // biome-ignore lint/correctness/useExhaustiveDependencies: the outside value alone triggers a reset
  React.useEffect(() => {
    if (joinMinutes(amount, unit) !== minutes) {
      const split = splitMinutes(minutes);
      setAmount(split.amount);
      setUnit(split.unit);
    }
  }, [minutes]);

  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: "flex-start" }}>
      <TextField
        label={label}
        type="number"
        value={amount}
        placeholder={placeholder}
        onChange={(event) => {
          setAmount(event.target.value);
          onChange(joinMinutes(event.target.value, unit));
        }}
        error={error}
        helperText={helperText}
        slotProps={{ htmlInput: { min: 0 } }}
        sx={{ flex: 1 }}
      />
      <TextField
        select
        label={`${label} unit`}
        value={unit}
        onChange={(event) => {
          const next = event.target.value as SpanUnit;
          setUnit(next);
          onChange(joinMinutes(amount, next));
        }}
        sx={{ minWidth: 120 }}
      >
        {(Object.keys(spanUnits) as SpanUnit[]).map((u) => (
          <MenuItem key={u} value={u}>
            {u}
          </MenuItem>
        ))}
      </TextField>
    </Stack>
  );
}
