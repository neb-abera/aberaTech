/** Which colour-scheme choices a visitor is offered, and what to do about stale ones. */

export type ColorModeChoice = 'system' | 'light' | 'dark';

/**
 * The choices to show.
 *
 * "System" is for account holders only. Following the operating system is a
 * preference, and this site has nowhere to keep preferences for a visitor it
 * knows nothing about — so the plain choice, dark or light, is what everybody
 * else gets. Dark is the default either way.
 */
export function availableModes(signedIn: boolean): ColorModeChoice[] {
  return signedIn ? ['system', 'light', 'dark'] : ['light', 'dark'];
}

/**
 * The mode to switch to when the current one is no longer on offer, or null to
 * leave it alone.
 *
 * The case this exists for: somebody signs in, picks System, and later signs
 * out. Their stored preference is now a setting they can neither see nor
 * change, and leaving them on it means a menu whose entries all look unselected.
 */
export function correctedMode(current: string | undefined, signedIn: boolean): ColorModeChoice | null {
  if (!current) return null;
  return availableModes(signedIn).includes(current as ColorModeChoice) ? null : 'dark';
}
