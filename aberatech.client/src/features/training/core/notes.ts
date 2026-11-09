/**
 * Notes per lesson and per item, and search over them.
 */
import type { Note, TrainingDocument } from "./progress";
import { locate, type Syllabus, syllabus } from "./syllabus";

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

export interface NoteHit {
  /** The lesson or item id the note is kept under. */
  id: string;
  note: Note;
  /** Where it lives: the unit, and the lesson or item title. */
  unitTitle: string;
  title: string;
  /** The lesson to open. */
  lessonId?: string;
  unitId: string;
}

/**
 * Notes holding every word of the query, case and accents ignored, newest
 * first. An empty query lists every note.
 */
export function searchNotes(
  document: TrainingDocument,
  query: string,
  from: Syllabus = syllabus,
): NoteHit[] {
  const fold = (text: string) =>
    text.normalize("NFD").replace(/\p{M}/gu, "").toLowerCase();
  const words = fold(query).split(/\s+/).filter(Boolean);
  const hits: NoteHit[] = [];
  for (const [id, note] of Object.entries(document.notes)) {
    const place = locate(id, from);
    const title = place?.item?.title ?? place?.lesson?.title ?? id;
    const haystack = fold(`${note.text} ${title}`);
    if (!words.every((word) => haystack.includes(word))) continue;
    hits.push({
      id,
      note,
      unitTitle: place?.unit.title ?? "",
      title,
      lessonId: place?.lesson?.id,
      unitId: place?.unit.id ?? "",
    });
  }
  return hits.sort((a, b) => b.note.at.localeCompare(a.note.at));
}
