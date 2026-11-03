/** A week of training the Plan tab can start from. */
export interface WeekPreset {
  label: string;
  weeklyHours: number;
  strengthHours: number;
  source: string;
}

/** Neb's default, 2026-11-03: eight hours of running and three of lifting. */
export const ATHLETIC: WeekPreset = {
  label: "Athletic",
  weeklyHours: 8,
  strengthHours: 3,
  source: "8 h running, 3 h lifting",
};

/**
 * The Evoke Endurance Military Athlete Foundational Plan v1.1, averaged over
 * its 41 weeks (2026-11-02 to 2027-08-15) from the TrainingPeaks export:
 * running 3.9 h a week (3.0 at the start, 6.6 at the peak) plus 0.3 h of
 * rucking, and 2.5 h of strength, muscular endurance and core.
 */
export const EVOKE: WeekPreset = {
  label: "Evoke plan",
  weeklyHours: 4.2,
  strengthHours: 2.5,
  source: "41-week average: 3.9 h running, 0.3 h rucking, 2.5 h strength",
};
