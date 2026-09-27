/**
 * One colour per focus area, in both themes.
 *
 * The hues and their order come from a categorical palette selected so that
 * adjacent slots stay distinguishable under colour vision deficiency. Colour
 * only ever appears on the mark, never on the text: a chip carries a coloured
 * key and its label uses the theme's own text tokens, so nothing depends on
 * reading a light hue as type.
 */

export type ThemeMode = "light" | "dark";

const SLOTS: Record<string, [string, string]> = {
  "Signal Processing": ["#2a78d6", "#3987e5"],
  "RF and Microwave Engineering": ["#eb6834", "#d95926"],
  "Communications and Networking": ["#1baf7a", "#199e70"],
  "Electronics and the Solid State": ["#eda100", "#c98500"],
  "Systems and Controls": ["#e87ba4", "#d55181"],
  "AI and Autonomous Systems": ["#008300", "#008300"],
  "Computer Engineering": ["#4a3aa7", "#9085e9"],
  "Optics and Photonics": ["#e34948", "#e66767"],
  "Bridge and other courses": ["#898781", "#898781"],
  Preparation: ["#a6803c", "#c2a05c"],
};

const FALLBACK: [string, string] = SLOTS["Signal Processing"];

export function areaColor(area: string | undefined, mode: ThemeMode): string {
  const slot = (area ? SLOTS[area] : undefined) ?? FALLBACK;
  return mode === "dark" ? slot[1] : slot[0];
}

/**
 * The colour for a course: the first of its areas the reader has turned on, so a
 * course listed under two headings keys to the one they are looking at.
 */
export function courseColor(
  areas: string[],
  active: Set<string>,
  mode: ThemeMode,
): string {
  return areaColor(areas.find((a) => active.has(a)) ?? areas[0], mode);
}

export const AREA_NAMES = Object.keys(SLOTS);

/**
 * The same colours as CSS custom properties, so the stylesheet carries both
 * schemes and the browser picks one. A colour chosen in script would be the
 * server's guess in the prerendered page and could change at hydration.
 */
function slotIndex(area: string | undefined): number {
  const index = area ? AREA_NAMES.indexOf(area) : -1;
  return index === -1 ? 0 : index;
}

export function areaColorVar(area: string | undefined): string {
  return `var(--planner-area-${slotIndex(area)})`;
}

export function courseColorVar(areas: string[], active: Set<string>): string {
  return areaColorVar(areas.find((a) => active.has(a)) ?? areas[0]);
}

/** How strongly a chip is tinted with its colour, per scheme. */
export const CHIP_TINT: Record<ThemeMode, string> = {
  light: "9%",
  dark: "16%",
};

/** The custom properties behind areaColorVar, for one scheme. */
export function areaColorVariables(mode: ThemeMode): Record<string, string> {
  return Object.fromEntries([
    ...AREA_NAMES.map((name, index) => [
      `--planner-area-${index}`,
      areaColor(name, mode),
    ]),
    ["--planner-chip-tint", CHIP_TINT[mode]],
  ]);
}
