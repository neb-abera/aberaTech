/**
 * One colour per focus area, in both themes.
 *
 * The hues and their order come from a categorical palette selected so that
 * adjacent slots stay distinguishable under colour vision deficiency. Colour
 * only ever appears on the mark, never on the text: a chip carries a coloured
 * key and its label uses the theme's own text tokens, so nothing depends on
 * reading a light hue as type.
 */

export type ThemeMode = 'light' | 'dark';

const SLOTS: Record<string, [string, string]> = {
  'Signal Processing': ['#2a78d6', '#3987e5'],
  'RF and Microwave Engineering': ['#eb6834', '#d95926'],
  'Communications and Networking': ['#1baf7a', '#199e70'],
  'Electronics and the Solid State': ['#eda100', '#c98500'],
  'Systems and Controls': ['#e87ba4', '#d55181'],
  'AI and Autonomous Systems': ['#008300', '#008300'],
  'Computer Engineering': ['#4a3aa7', '#9085e9'],
  'Optics and Photonics': ['#e34948', '#e66767'],
  'Bridge and other courses': ['#898781', '#898781'],
  Preparation: ['#a6803c', '#c2a05c']
};

const FALLBACK: [string, string] = SLOTS['Signal Processing'];

export function areaColor(area: string | undefined, mode: ThemeMode): string {
  const slot = (area ? SLOTS[area] : undefined) ?? FALLBACK;
  return mode === 'dark' ? slot[1] : slot[0];
}

/**
 * The colour for a course: the first of its areas the reader has turned on, so a
 * course listed under two headings keys to the one they are looking at.
 */
export function courseColor(areas: string[], active: Set<string>, mode: ThemeMode): string {
  return areaColor(areas.find((a) => active.has(a)) ?? areas[0], mode);
}

export const AREA_NAMES = Object.keys(SLOTS);
