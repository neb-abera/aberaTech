import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import * as React from "react";
import { type ActivityRow, saveActivityLoad } from "../core/api";
import { kgToLb, lbToKg } from "../core/format";

/**
 * The load a ruck was carried at, typed in pounds or kilograms. No watch
 * records it, and without it the models read a ruck as a walk.
 */
export default function LoadField({
  activity,
  onSaved,
  onError,
}: {
  activity: ActivityRow;
  onSaved: () => void;
  onError: (text: string) => void;
}) {
  const [unit, setUnit] = React.useState<"lb" | "kg">("lb");
  const [text, setText] = React.useState(() =>
    activity.loadKg === null ? "" : String(Math.round(kgToLb(activity.loadKg))),
  );

  const shown = (loadKg: number | null) =>
    loadKg === null
      ? ""
      : unit === "lb"
        ? String(Math.round(kgToLb(loadKg)))
        : String(Number(loadKg.toFixed(1)));

  const switchUnit = () => {
    const next = unit === "lb" ? "kg" : "lb";
    const value = Number(text);
    setUnit(next);
    if (text.trim() !== "" && Number.isFinite(value) && value > 0) {
      setText(
        next === "kg"
          ? String(Number(lbToKg(value).toFixed(1)))
          : String(Math.round(kgToLb(value))),
      );
    }
  };

  const save = async () => {
    const value = text.trim() === "" ? null : Number(text);
    if (value !== null && (!Number.isFinite(value) || value <= 0)) {
      onError("A load is a positive number of pounds or kilograms.");
      return;
    }
    const loadKg =
      value === null ? null : unit === "lb" ? lbToKg(value) : value;
    if (
      (loadKg === null && activity.loadKg === null) ||
      (loadKg !== null &&
        activity.loadKg !== null &&
        Math.abs(loadKg - activity.loadKg) < 0.05)
    ) {
      return;
    }
    try {
      await saveActivityLoad(activity.id, loadKg);
      onSaved();
    } catch (error) {
      onError((error as Error).message);
      setText(shown(activity.loadKg));
    }
  };

  return (
    <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end" }}>
      <TextField
        size="small"
        variant="standard"
        value={text}
        onChange={(event) => setText(event.target.value)}
        onBlur={() => void save()}
        onKeyDown={(event) => {
          if (event.key === "Enter") (event.target as HTMLInputElement).blur();
        }}
        slotProps={{
          htmlInput: {
            "aria-label": `Load for ${activity.name || "ruck"} of ${activity.startedAt.slice(0, 10)}`,
            inputMode: "decimal",
            style: { textAlign: "right", width: 48 },
          },
        }}
      />
      <Button size="small" onClick={switchUnit} aria-label="Switch load unit">
        {unit}
      </Button>
    </Stack>
  );
}
