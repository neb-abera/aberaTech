/**
 * The shapes the planner works with.
 *
 * A course's `groups` field is an AND of ORs. `[['A','B'],['C']]` reads as
 * "(A or B) and C". That shape comes straight from the catalog parser, which
 * only produces an OR group when course codes appear on both sides of "or".
 */

export interface Course {
  code: string;
  title: string;
  credits: number;
  desc: string;
  /** The prerequisite sentence exactly as the JHU catalog prints it. */
  prereq_text: string;
  /** AND of ORs. Empty means no enforceable prerequisite. */
  groups: string[][];
  areas: string[];
  /** 2 for a 200 level course, 7 for a 700 level course, 0 for preparation. */
  level: number;
  /** False for the 100 to 500 level courses, which confer no graduate credit. */
  gradeable: boolean;
  /** True when the course sits outside EN.525 and EN.520. */
  external: boolean;
  /** Courses that may not be taken alongside this one. */
  excl: string[];
  /** Background assumptions stated in prose rather than course numbers. */
  bg: string[];
  /** Set on the synthetic preparation courses built by background.ts. */
  prep?: boolean;
  bgKey?: string;
  jhuEquivalent?: string | null;
}

export type Catalog = Record<string, Course>;

/** Course code to term index. */
export type Placement = Map<string, number>;

export interface RawCatalog {
  courses: Catalog;
  areas: Record<string, string[]>;
  concentrations: Record<string, string[]>;
  /** Identifier and human label for each background assumption. */
  background: [string, string][];
}

export interface TrackStage {
  name: string;
  courses: string[];
}

export interface RawTrack {
  name: string;
  kind: "degree" | "mastery";
  length: string;
  goal: string;
  tradeoff: string;
  stages: TrackStage[];
}

export interface Track extends RawTrack {
  id: string;
  /** Every course in the track, in curated order. */
  courses: string[];
}

export type RawTracks = Record<string, RawTrack>;

export interface CourseLink {
  label: string;
  href: string;
  /** True only when the address has actually been requested and checked. */
  verified: boolean;
  note: string;
}
