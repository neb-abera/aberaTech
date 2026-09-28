/**
 * The alert settings and bounds as the server sends them with nothing
 * saved. Shared by the panel, form and API suites so they agree on the shape.
 */
export const settings = {
  priority: 2 as 0 | 1 | 2,
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
};

export const bounds = {
  repeatSeconds: { min: 30, max: 10800 },
  stopAfterMinutes: { min: 1, max: 180 },
  defaultLeadMinutes: { min: 0, max: 1440 },
  pollMinutes: { min: 1, max: 60 },
  lookaheadHours: { min: 1, max: 336 },
  maxOwnerEmails: 10,
  maxEmergencySounds: 50,
  sounds: ["pushover", "siren", "persistent", "none"],
};
