/**
 * The alert settings and bounds as the server sends them with nothing
 * saved. Shared by the panel, form and API suites so they agree on the shape.
 */
export const settings = {
  repeatSeconds: 60,
  stopAfterMinutes: 180,
  sound: "",
  defaultLeadMinutes: 10,
  pollMinutes: 5,
  lookaheadHours: 48,
  includeAllDay: false,
  timeZone: "",
  ownerEmails: [] as string[],
  notificationPriority: 0 as 0 | 1,
  notificationSound: "",
  defaultType: "none" as "none" | "notification",
  backupDelaySeconds: 0,
  phoneSound: "default",
  phoneSnoozeMinutes: 9,
};

export const bounds = {
  repeatSeconds: { min: 30, max: 10800 },
  stopAfterMinutes: { min: 1, max: 180 },
  defaultLeadMinutes: { min: 0, max: 1440 },
  pollMinutes: { min: 1, max: 60 },
  lookaheadHours: { min: 1, max: 336 },
  backupDelaySeconds: { min: 0, max: 900 },
  maxOwnerEmails: 10,
  maxEmergencySounds: 50,
  sounds: [
    { name: "pushover", description: "Pushover (default)", custom: false },
    { name: "siren", description: "Siren", custom: false },
    { name: "persistent", description: "Persistent (long)", custom: false },
    { name: "none", description: "None (silent)", custom: false },
  ],
  phoneSounds: [
    { value: "default", label: "iPhone default" },
    { value: "pulse", label: "Pulse" },
    { value: "chime", label: "Chime" },
    { value: "rise", label: "Rise" },
    { value: "siren", label: "Siren" },
    { value: "beacon", label: "Beacon" },
  ],
  phoneSnoozeMinutes: { min: 1, max: 30 },
};
