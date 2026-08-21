import { describe, expect, it } from 'vitest';
import { dayKey, describeWait, formatDay, formatTime, groupByDay } from '../format';

// 2027-06-01T23:30:00Z is 7:30 PM in New York and 1:30 AM the *next day* in
// Berlin. Every grouping and day-boundary case below hangs off that.
const lateEvening = '2027-06-01T23:30:00Z';

describe('formatTime', () => {
  it('renders an instant in the zone it is asked for', () => {
    expect(formatTime(lateEvening, 'America/New_York')).toContain('7:30');
    expect(formatTime(lateEvening, 'America/Chicago')).toContain('6:30');
  });

  it('always names the zone, so a time cannot be read as another one', () => {
    expect(formatTime(lateEvening, 'America/New_York')).toContain('EDT');
    expect(formatTime(lateEvening, 'America/Chicago')).toContain('CDT');
  });
});

describe('formatDay and dayKey', () => {
  it('puts an instant on the day it falls on for the viewer, not the host', () => {
    // The same moment is Tuesday in Washington and Wednesday in Berlin. A
    // scheduler that groups in the host's zone shows a German visitor a slot
    // filed under the wrong day.
    expect(formatDay(lateEvening, 'America/New_York')).toBe('Tue, Jun 1');
    expect(formatDay(lateEvening, 'Europe/Berlin')).toBe('Wed, Jun 2');

    expect(dayKey(lateEvening, 'America/New_York')).toBe('2027-06-01');
    expect(dayKey(lateEvening, 'Europe/Berlin')).toBe('2027-06-02');
  });
});

describe('groupByDay', () => {
  it('groups in the viewer zone', () => {
    const slots = [{ startsAt: '2027-06-01T22:00:00Z' }, { startsAt: lateEvening }];

    // Both are Tuesday evening in New York.
    expect(groupByDay(slots, 'America/New_York')).toHaveLength(1);
    // In Berlin one is Wednesday 00:00 and the other Wednesday 01:30 — still
    // one group, but a different day from the New York answer.
    expect(groupByDay(slots, 'Europe/Berlin')).toHaveLength(1);
    expect(groupByDay(slots, 'Europe/Berlin')[0][0]).toBe('2027-06-02');
  });

  it('splits across a day boundary in the viewer zone', () => {
    const slots = [{ startsAt: '2027-06-01T12:00:00Z' }, { startsAt: lateEvening }];

    expect(groupByDay(slots, 'America/New_York')).toHaveLength(1);
    expect(groupByDay(slots, 'Europe/Berlin')).toHaveLength(2);
  });

  it('keeps the order it was given', () => {
    const slots = [{ startsAt: '2027-06-01T12:00:00Z' }, { startsAt: '2027-06-02T12:00:00Z' }];
    const keys = groupByDay(slots, 'America/New_York').map(([key]) => key);

    expect(keys).toEqual(['2027-06-01', '2027-06-02']);
  });

  it('handles an empty list', () => {
    expect(groupByDay([], 'America/New_York')).toEqual([]);
  });
});

describe('describeWait', () => {
  it('reads like a person wrote it', () => {
    expect(describeWait(0)).toBe('now');
    expect(describeWait(1)).toBe('in about a minute');
    expect(describeWait(25)).toBe('in about 25 minutes');
    expect(describeWait(60)).toBe('in about an hour');
    expect(describeWait(150)).toBe('in about 3 hours');
  });

  it('never shows a negative wait', () => {
    expect(describeWait(-5)).toBe('now');
  });

  it('says so when there is no estimate rather than showing a zero', () => {
    expect(describeWait(null)).toBe('not estimated yet');
  });
});
