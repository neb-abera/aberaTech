/**
 * A missing colour used to render a chip with a transparent background, which
 * reads as an empty term. Every group the catalog uses must have one, in both
 * themes, and the two must differ where the surface differs.
 */
import { describe, expect, test } from 'vitest';
import rawCatalog from '../../data/catalog.json';
import { AREA_NAMES, areaColor, courseColor } from '../areaColors';
import type { RawCatalog } from '../types';

const data = rawCatalog as unknown as RawCatalog;

describe('area colours', () => {
  test('every group in the catalog has a colour assigned', () => {
    expect(Object.keys(data.areas).filter((a) => !AREA_NAMES.includes(a))).toEqual([]);
  });
  test('every course names at least one group, so nothing falls through', () => {
    const orphans = Object.values(data.courses)
      .filter((c) => !c.areas.length)
      .map((c) => c.code);
    expect(orphans).toEqual([]);
  });
  test('preparation has its own colour, distinct from every focus area', () => {
    const prep = areaColor('Preparation', 'light');
    const others = AREA_NAMES.filter((a) => a !== 'Preparation').map((a) => areaColor(a, 'light'));
    expect(others).not.toContain(prep);
  });
  test('an unknown group falls back rather than rendering with nothing', () => {
    expect(areaColor('Something Else', 'light')).toBe(areaColor('Signal Processing', 'light'));
  });
  test('a course keys to the group the reader has turned on', () => {
    const areas = ['Signal Processing', 'RF and Microwave Engineering'];
    expect(courseColor(areas, new Set(['RF and Microwave Engineering']), 'light')).toBe(
      areaColor('RF and Microwave Engineering', 'light')
    );
    expect(courseColor(areas, new Set(['Signal Processing']), 'light')).toBe(areaColor('Signal Processing', 'light'));
  });
  test('every colour is a hex value in both themes', () => {
    for (const name of AREA_NAMES) {
      expect(areaColor(name, 'light')).toMatch(/^#[0-9a-f]{6}$/);
      expect(areaColor(name, 'dark')).toMatch(/^#[0-9a-f]{6}$/);
    }
  });
});
