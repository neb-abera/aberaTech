/**
 * A calendar file for a booking, for the "Add to calendar" button.
 *
 * Its UID is the one Google gives the invite's event: the booking's id in hex
 * at google.com. A calendar that already holds the Google invite treats the
 * file as the same event and does not add it twice.
 */

export interface IcsBooking {
  id: string;
  startsAt: string;
  endsAt: string;
  hostName: string;
}

/** 2026-11-16T15:00:00Z as 20261116T150000Z. */
function icsTime(iso: string): string {
  return new Date(iso)
    .toISOString()
    .replace(/[-:]/g, "")
    .replace(/\.\d{3}/, "");
}

/** Commas, semicolons and backslashes are escaped in iCalendar text. */
function text(value: string): string {
  return value
    .replace(/\\/g, "\\\\")
    .replace(/;/g, ";")
    .replace(/,/g, "\\,")
    .replace(/\r?\n/g, "\\n");
}

/** The Google event id for a booking id, as the server chooses it (GoogleCalendarInvites.EventId). */
export function eventId(bookingId: string): string {
  return bookingId.replace(/-/g, "").toLowerCase();
}

export function buildIcs(booking: IcsBooking, now: Date = new Date()): string {
  return [
    "BEGIN:VCALENDAR",
    "VERSION:2.0",
    "PRODID:-//abera.tech//schedule//EN",
    "METHOD:PUBLISH",
    "BEGIN:VEVENT",
    `UID:${eventId(booking.id)}@google.com`,
    `DTSTAMP:${icsTime(now.toISOString())}`,
    `DTSTART:${icsTime(booking.startsAt)}`,
    `DTEND:${icsTime(booking.endsAt)}`,
    `SUMMARY:${text(`Meeting with ${booking.hostName}`)}`,
    "DESCRIPTION:Booked at https://abera.tech/schedule.",
    "END:VEVENT",
    "END:VCALENDAR",
    "",
  ].join("\r\n");
}
