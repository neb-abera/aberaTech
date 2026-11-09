/**
 * Calendar dates as "YYYY-MM-DD" strings. Arithmetic is done at noon UTC,
 * so a daylight saving change never moves a date by one.
 */

const noon = (date: string): number => Date.parse(`${date}T12:00:00Z`);

export function addDays(date: string, days: number): string {
  return new Date(noon(date) + days * 86_400_000).toISOString().slice(0, 10);
}

export function daysBetween(from: string, to: string): number {
  return Math.round((noon(to) - noon(from)) / 86_400_000);
}

/** Sunday 0 to Saturday 6. */
export function weekday(date: string): 0 | 1 | 2 | 3 | 4 | 5 | 6 {
  return new Date(noon(date)).getUTCDay() as 0 | 1 | 2 | 3 | 4 | 5 | 6;
}

/** The local calendar date of a moment, as the owner's browser sees it. */
export function localDate(moment: Date): string {
  const month = String(moment.getMonth() + 1).padStart(2, "0");
  const day = String(moment.getDate()).padStart(2, "0");
  return `${moment.getFullYear()}-${month}-${day}`;
}

/** The local calendar date of an ISO date and time. */
export const localDateOf = (iso: string): string => localDate(new Date(iso));

const days = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
const months = [
  "Jan",
  "Feb",
  "Mar",
  "Apr",
  "May",
  "Jun",
  "Jul",
  "Aug",
  "Sep",
  "Oct",
  "Nov",
  "Dec",
];

/** "Tue 12 Jan 2027". */
export function longDate(date: string): string {
  const [year, month, day] = date.split("-").map(Number);
  return `${days[weekday(date)]} ${day} ${months[month - 1]} ${year}`;
}

/** "12 Jan". */
export function shortDate(date: string): string {
  const [, month, day] = date.split("-").map(Number);
  return `${day} ${months[month - 1]}`;
}

/** "09:40", in the browser's zone. */
export function clock(iso: string): string {
  const moment = new Date(iso);
  return `${String(moment.getHours()).padStart(2, "0")}:${String(moment.getMinutes()).padStart(2, "0")}`;
}
