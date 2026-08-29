/**
 * How the admin messages view names things.
 *
 * The server reports enum names (`ReminderDayBefore`, `DeadLettered`) because
 * they are stable identifiers; what the host should read is what the message
 * is for and what its state means for delivery. Unknown values pass through
 * as themselves: a new server-side kind showing up verbatim is odd but
 * honest, and a blank row would just make the outbox look shorter than it is.
 */

export interface AdminMessage {
  id: string;
  kind: string;
  to: string;
  body: string;
  state: string;
  attempts: number;
  dueAt: string | null;
  sentAt: string | null;
  lastError: string | null;
}

export interface AdminMessages {
  upcoming: AdminMessage[];
  recent: AdminMessage[];
}

const kinds: Record<string, string> = {
  Joined: "Queue welcome",
  TimeChanged: "Time changed",
  Imminent: "Up soon",
  YourTurn: "Your turn",
  Booked: "Booking confirmation",
  ReminderDayBefore: "Day-before reminder",
  Reminder: "Final reminder",
  Cancelled: "Cancellation",
  HostBooked: "New booking, to you",
  HostCancelled: "Cancellation, to you",
};

export function kindLabel(kind: string): string {
  return kinds[kind] ?? kind;
}

export type ChipColor = "default" | "success" | "warning" | "error" | "info";

export interface StateChip {
  label: string;
  color: ChipColor;
}

const states: Record<string, StateChip> = {
  Pending: { label: "Queued", color: "default" },
  Failed: { label: "Retrying", color: "warning" },
  // "Sent" deliberately does not claim delivery: the provider accepted it and
  // the receipt is still to come. Treating those as the same thing is the bug
  // this whole outbox exists to avoid.
  Sent: { label: "Sent, awaiting receipt", color: "info" },
  Delivered: { label: "Delivered", color: "success" },
  DeadLettered: { label: "Not delivered", color: "error" },
};

export function stateChip(state: string): StateChip {
  return states[state] ?? { label: state, color: "default" };
}
