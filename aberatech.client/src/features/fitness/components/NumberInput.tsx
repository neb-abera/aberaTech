import TextField from "@mui/material/TextField";
import * as React from "react";

/**
 * A number box that can be cleared and retyped. The draft is text, and only
 * a value inside the range reaches the scenario, so deleting the digits shows
 * an empty box rather than a zero that comes back.
 */
export function NumberInput({
  label,
  value,
  min,
  max,
  step,
  onCommit,
  disabled,
  helperText,
  sx,
}: {
  label?: string;
  value: number | null;
  min: number;
  max: number;
  step: number;
  onCommit: (value: number) => void;
  disabled?: boolean;
  helperText?: string;
  sx?: object;
}) {
  const shown = value === null ? "" : String(value);
  const [draft, setDraft] = React.useState(shown);
  const [editing, setEditing] = React.useState(false);

  return (
    <TextField
      fullWidth={!sx}
      size="small"
      type="number"
      label={label}
      disabled={disabled}
      helperText={helperText}
      sx={sx}
      value={editing ? draft : shown}
      slotProps={{ htmlInput: { min, max, step } }}
      onFocus={() => {
        setDraft(shown);
        setEditing(true);
      }}
      onBlur={() => setEditing(false)}
      onChange={(event) => {
        setDraft(event.target.value);
        const parsed = Number(event.target.value);
        if (
          event.target.value.trim() !== "" &&
          Number.isFinite(parsed) &&
          parsed >= min &&
          parsed <= max
        )
          onCommit(parsed);
      }}
    />
  );
}
