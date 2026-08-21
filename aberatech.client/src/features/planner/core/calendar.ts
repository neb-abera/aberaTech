/**
 * Term numbering and the calendar dates behind it.
 *
 * Terms are integers from 0. The calendar turns an index into a label and a
 * real date, which is what the five year clock in rules.ts needs.
 */

/** Month each JHU term begins, zero indexed. Spring January, Summer May, Fall August. */
const TERM_START_MONTH: Record<string, number> = { Spring: 0, Summer: 4, Fall: 7 };
const CYCLE_3 = ['Spring', 'Summer', 'Fall'];
const CYCLE_2 = ['Spring', 'Fall'];
/** A JHU Engineering for Professionals term runs about fifteen weeks. */
const TERM_DAYS = 105;
const MS_PER_DAY = 86_400_000;

export interface CalendarOptions {
  startTerm?: string;
  startYear?: number;
  termsPerYear?: number;
}

export class Calendar {
  readonly termsPerYear: number;
  readonly cycle: string[];
  readonly offset: number;
  readonly startYear: number;

  constructor({ startTerm = 'Spring', startYear = 2027, termsPerYear = 3 }: CalendarOptions = {}) {
    this.termsPerYear = termsPerYear === 2 ? 2 : 3;
    this.cycle = this.termsPerYear === 2 ? CYCLE_2 : CYCLE_3;
    const i = this.cycle.indexOf(startTerm);
    this.offset = i < 0 ? 0 : i;
    this.startYear = startYear;
  }

  at(index: number): { name: string; year: number } {
    const abs = this.offset + index;
    return {
      name: this.cycle[abs % this.cycle.length],
      year: this.startYear + Math.floor(abs / this.cycle.length)
    };
  }

  /** For example "Spring 2027". */
  label(index: number): string {
    const { name, year } = this.at(index);
    return `${name} ${year}`;
  }

  startDate(index: number): Date {
    const { name, year } = this.at(index);
    return new Date(Date.UTC(year, TERM_START_MONTH[name], 1));
  }

  endDate(index: number): Date {
    return new Date(this.startDate(index).getTime() + TERM_DAYS * MS_PER_DAY);
  }

  /** Whole months from a to b. Negative when b precedes a. */
  static monthsBetween(a: Date, b: Date): number {
    return (b.getUTCFullYear() - a.getUTCFullYear()) * 12 + (b.getUTCMonth() - a.getUTCMonth());
  }

  static addYears(d: Date, years: number): Date {
    return new Date(Date.UTC(d.getUTCFullYear() + years, d.getUTCMonth(), d.getUTCDate()));
  }
}
