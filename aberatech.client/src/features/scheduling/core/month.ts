/** Laying out a month grid without pulling in a date library. */

export interface MonthDay {
  /** yyyy-MM-dd, or null for the blank cells before the first of the month. */
  date: string | null;
  dayOfMonth: number | null;
}

/** yyyy-MM-dd for a Date, read in the given zone. */
export function isoDate(value: Date, zone: string): string {
  // en-CA formats as yyyy-MM-dd, which saves hand-assembling the parts and
  // getting the zero padding subtly wrong.
  return value.toLocaleDateString('en-CA', { timeZone: zone });
}

/** The month an ISO date belongs to, as { year, month } with month 1-12. */
export function monthOf(iso: string): { year: number; month: number } {
  const [year, month] = iso.split('-').map(Number);
  return { year, month };
}

export function monthLabel(year: number, month: number): string {
  return new Date(Date.UTC(year, month - 1, 1)).toLocaleDateString('en-US', {
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC'
  });
}

export function addMonths(year: number, month: number, delta: number): { year: number; month: number } {
  const zeroBased = month - 1 + delta;
  return { year: year + Math.floor(zeroBased / 12), month: (((zeroBased % 12) + 12) % 12) + 1 };
}

/**
 * The cells of a month grid, Sunday first, padded so the first of the month
 * lands in the right column.
 *
 * Built in UTC deliberately. These are calendar dates, not instants — "the 3rd
 * of June" is the same cell wherever the reader is — and constructing them in
 * local time would shift the whole grid by a day for anybody far enough east or
 * west.
 */
export function monthGrid(year: number, month: number): MonthDay[] {
  const firstOfMonth = new Date(Date.UTC(year, month - 1, 1));
  const daysInMonth = new Date(Date.UTC(year, month, 0)).getUTCDate();
  const leadingBlanks = firstOfMonth.getUTCDay();

  const cells: MonthDay[] = [];

  for (let i = 0; i < leadingBlanks; i++) {
    cells.push({ date: null, dayOfMonth: null });
  }

  for (let day = 1; day <= daysInMonth; day++) {
    const padded = String(day).padStart(2, '0');
    cells.push({ date: `${year}-${String(month).padStart(2, '0')}-${padded}`, dayOfMonth: day });
  }

  return cells;
}

/** "Tuesday, 3 June" for a heading over the times. */
export function longDayLabel(iso: string): string {
  const [year, month, day] = iso.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day)).toLocaleDateString('en-US', {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
    timeZone: 'UTC'
  });
}
