import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { useEffect, useState } from "react";
import { clock, localDateOf, shortDate } from "../core/dates";
import { noteLimit, setNote } from "../core/notes";
import type { TrainingDocument } from "../core/progress";

/**
 * A note on a lesson or an item. It saves a beat after typing stops, with
 * the rest of the document, and says when it was last kept.
 */
export default function NoteField({
  id,
  label,
  document,
  change,
}: {
  id: string;
  label: string;
  document: TrainingDocument;
  change: (fn: (current: TrainingDocument) => TrainingDocument) => void;
}) {
  const saved = document.notes[id];
  const [text, setText] = useState(saved?.text ?? "");
  // A reload from the server, another device's edit, replaces the draft
  // only while it matches what was last saved here.
  useEffect(() => {
    setText(saved?.text ?? "");
  }, [saved?.text]);

  return (
    <div>
      <TextField
        label={label}
        value={text}
        multiline
        minRows={2}
        fullWidth
        size="small"
        slotProps={{ htmlInput: { maxLength: noteLimit } }}
        onChange={(event) => {
          const next = event.target.value;
          setText(next);
          change((current) => setNote(current, id, next, new Date()));
        }}
      />
      {saved && (
        <Typography variant="caption" sx={{ color: "text.secondary" }}>
          Kept {shortDate(localDateOf(saved.at))} {clock(saved.at)}.
        </Typography>
      )}
    </div>
  );
}
