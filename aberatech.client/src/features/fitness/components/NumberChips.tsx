import Chip from "@mui/material/Chip";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import * as React from "react";

/**
 * An editable set of numbers — the distances a projection reports, the
 * horizons it reports them at. A dropdown of four presets is a decision made
 * for the athlete; this is the same convenience with the door left open.
 */
export default function NumberChips({
  label,
  values,
  format,
  parse,
  placeholder,
  onChange,
  max = 8,
}: {
  label: string;
  values: number[];
  format: (value: number) => string;
  parse: (text: string) => number | null;
  placeholder: string;
  onChange: (values: number[]) => void;
  max?: number;
}) {
  const [draft, setDraft] = React.useState("");
  const [error, setError] = React.useState(false);

  const add = () => {
    const parsed = parse(draft);
    if (parsed === null || values.some((v) => Math.abs(v - parsed) < 1e-6)) {
      setError(draft.trim() !== "");
      return;
    }
    onChange([...values, parsed].sort((a, b) => a - b).slice(0, max));
    setDraft("");
    setError(false);
  };

  return (
    <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", rowGap: 1 }}>
      {values.map((value) => (
        <Chip
          key={value}
          label={format(value)}
          size="small"
          onDelete={
            values.length > 1
              ? () => onChange(values.filter((v) => v !== value))
              : undefined
          }
        />
      ))}
      <TextField
        size="small"
        variant="standard"
        label={label}
        placeholder={placeholder}
        value={draft}
        error={error}
        disabled={values.length >= max}
        onChange={(event) => {
          setDraft(event.target.value);
          setError(false);
        }}
        onBlur={add}
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            event.preventDefault();
            add();
          }
        }}
        sx={{ width: 130 }}
      />
    </Stack>
  );
}
