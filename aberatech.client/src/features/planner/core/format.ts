/** Formatting helpers shared by the planner views. */

export function formatMonthYear(d: Date | null): string {
  return d ? d.toLocaleDateString('en-US', { month: 'short', year: 'numeric', timeZone: 'UTC' }) : 'not set';
}

export function plural(n: number, one: string, many = `${one}s`): string {
  return n === 1 ? one : many;
}

/** "Course code" without the EN prefix, which is the same on every course. */
export function shortCode(code: string): string {
  return code.replace(/^EN\./, '');
}
