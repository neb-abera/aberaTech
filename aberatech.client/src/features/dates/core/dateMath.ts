/**
 * Calendar arithmetic on plain dates, with no time and no zone: the days
 * between two dates, and a date plus or minus years, months, weeks and
 * days. The phone app carries the same rules and the same test table.
 *
 * A month added to the 31st lands on the last day of a shorter month:
 * 31 January plus one month is 28 February.
 */

/** A date on the proleptic Gregorian calendar. Month 1 is January. */
export interface CalendarDate {
  year: number;
  month: number;
  day: number;
}

const dayMs = 86_400_000;

export const minYear = 1;
export const maxYear = 9999;

/** "2026-10-01" to a date, or null for anything else, including 2026-02-30. */
export function parseDate(text: string): CalendarDate | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(text.trim());
  if (!match) return null;
  const date = {
    year: Number(match[1]),
    month: Number(match[2]),
    day: Number(match[3]),
  };
  if (date.year < minYear || date.month < 1 || date.month > 12) return null;
  if (date.day < 1 || date.day > daysInMonth(date.year, date.month))
    return null;
  return date;
}

/** "2026-10-01", the form a date input takes. */
export function formatDate(date: CalendarDate): string {
  return `${String(date.year).padStart(4, "0")}-${String(date.month).padStart(2, "0")}-${String(date.day).padStart(2, "0")}`;
}

export function daysInMonth(year: number, month: number): number {
  if (month === 2) return isLeap(year) ? 29 : 28;
  return [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][month - 1];
}

function isLeap(year: number): boolean {
  return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
}

/** Days since 1970-01-01. Date.UTC maps years 0 to 99 to 1900 to 1999, so the year is set apart. */
export function dayNumber(date: CalendarDate): number {
  const at = new Date(0);
  at.setUTCFullYear(date.year, date.month - 1, date.day);
  return Math.round(at.getTime() / dayMs);
}

export function fromDayNumber(days: number): CalendarDate {
  const at = new Date(days * dayMs);
  return {
    year: at.getUTCFullYear(),
    month: at.getUTCMonth() + 1,
    day: at.getUTCDate(),
  };
}

/** 0 Sunday to 6 Saturday. */
export function weekday(date: CalendarDate): number {
  return (((dayNumber(date) + 4) % 7) + 7) % 7;
}

/** The date `months` later (or earlier), on the same day or the month's last. */
export function addMonths(date: CalendarDate, months: number): CalendarDate {
  const index = date.year * 12 + (date.month - 1) + months;
  const year = Math.floor(index / 12);
  const month = index - year * 12 + 1;
  return { year, month, day: Math.min(date.day, daysInMonth(year, month)) };
}

export interface Between {
  /** Negative when the end is before the start. */
  totalDays: number;
  /** True when the end is before the start. The parts below are then for the reversed range. */
  before: boolean;
  years: number;
  months: number;
  days: number;
  weeks: number;
  /** The days left over after whole weeks. */
  weekRemainder: number;
  /** Monday to Friday in the range. The end date counts only when included. */
  weekdays: number;
}

/**
 * The time from start to end. With `includeEnd`, the end date counts as a
 * day, as when counting the days of a leave from its first day to its last.
 * Years, months and days: the most whole months that fit from the start,
 * then the days left.
 */
export function between(
  start: CalendarDate,
  end: CalendarDate,
  includeEnd: boolean,
): Between {
  const before = dayNumber(end) < dayNumber(start);
  const from = before ? end : start;
  const to = before ? start : end;
  const first = dayNumber(from);
  // The day after the last one counted.
  const last = dayNumber(to) + (includeEnd ? 1 : 0);
  const total = last - first;

  let months = (to.year - from.year) * 12 + (to.month - from.month) + 1;
  while (months > 0 && dayNumber(addMonths(from, months)) > last) months -= 1;
  const days = last - dayNumber(addMonths(from, months));

  return {
    totalDays: before ? -total : total,
    before,
    years: Math.floor(months / 12),
    months: months % 12,
    days,
    weeks: Math.floor(total / 7),
    weekRemainder: total % 7,
    weekdays: weekdaysIn(first, total),
  };
}

/** Monday to Friday among `count` days from day number `first`. */
function weekdaysIn(first: number, count: number): number {
  const whole = Math.floor(count / 7);
  let found = whole * 5;
  const startDay = (((first + 4) % 7) + 7) % 7;
  for (let n = 0; n < count % 7; n++) {
    const day = (startDay + n) % 7;
    if (day !== 0 && day !== 6) found += 1;
  }
  return found;
}

export interface Offset {
  years: number;
  months: number;
  weeks: number;
  days: number;
}

/**
 * The date plus the offset: years and months first, held to the month's
 * last day, then weeks and days. Negative numbers go back. Null outside
 * the years 1 to 9999.
 */
export function addToDate(
  date: CalendarDate,
  offset: Offset,
): CalendarDate | null {
  const moved = addMonths(date, offset.years * 12 + offset.months);
  const result = fromDayNumber(
    dayNumber(moved) + offset.weeks * 7 + offset.days,
  );
  return result.year < minYear || result.year > maxYear ? null : result;
}

const weekdayNames = [
  "Sunday",
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
] as const;

const monthNames = [
  "January",
  "February",
  "March",
  "April",
  "May",
  "June",
  "July",
  "August",
  "September",
  "October",
  "November",
  "December",
] as const;

/** "Thursday 1 October 2026". */
export function describeDate(date: CalendarDate): string {
  return `${weekdayNames[weekday(date)]} ${date.day} ${monthNames[date.month - 1]} ${date.year}`;
}

/** "1 day", "45 days". */
export function plural(count: number, unit: string): string {
  return `${count.toLocaleString("en-US")} ${unit}${count === 1 ? "" : "s"}`;
}
