/**
 * Notes per lesson and per item. Search over them is a later increment
 * (README.md).
 */
import type { TrainingDocument } from "./progress";

/** Longest note kept. The whole document is capped at 256 KB on the server. */
export const noteLimit = 4000;

export function setNote(
  document: TrainingDocument,
  id: string,
  text: string,
  now: Date,
): TrainingDocument {
  const notes = { ...document.notes };
  const trimmed = text.slice(0, noteLimit);
  if (trimmed.trim() === "") delete notes[id];
  else notes[id] = { text: trimmed, at: now.toISOString() };
  return { ...document, notes };
}
