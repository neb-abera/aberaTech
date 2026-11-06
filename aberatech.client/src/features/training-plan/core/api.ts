/**
 * The training plan, from the page's side: the plan page, its files, and
 * every row of its Workouts and Plan notes databases, read live from
 * Notion by the server (TrainingPlanReader.cs).
 *
 * A 401 or 403 is the answer "you are a visitor", not an error. A
 * deployment without the plan page answers `configured: false`.
 */

import { requestJson } from "../../../site/earlyRequest";
import type { PlaybookPage } from "../../playbook/core/api";

export interface PlanFile {
  id: string;
  name: string;
}

export interface PlanSection {
  heading: string;
  paragraphs: string[];
}

export interface PlanPlace {
  date?: string;
  week?: number;
  planDay?: number;
  weekday?: string;
  block?: string;
  weekLabel?: string;
}

export interface PlanWorkout {
  id: string;
  title: string;
  place: PlanPlace;
  sport?: string;
  order?: number;
  minutes?: number;
  kilometers?: number;
  tss?: number;
  intensityFactor?: number;
  description: string;
  comments: string;
  structure: string;
  changed: boolean;
  url?: string;
}

export interface PlanNote {
  id: string;
  title: string;
  place: PlanPlace;
  description: string;
  files: PlanFile[];
  changed: boolean;
  url?: string;
}

/** A page the plan page links: a subpage, or a page mentioned on it. */
export interface PlanLink {
  id: string;
  title: string;
}

export interface TrainingPlan {
  id: string;
  title: string;
  url?: string;
  sections: PlanSection[];
  files: PlanFile[];
  workouts: PlanWorkout[];
  notes: PlanNote[];
  pages?: PlanLink[];
}

export type PlanResult =
  | { status: "visitor" }
  | { status: "unconfigured"; message?: string }
  | { status: "owner"; plan: TrainingPlan }
  | { status: "error"; busy: boolean };

/** The first thing the page asks. The head script asks it first (site/meta.ts). */
export const trainingPlanUrl = "/api/training-plan";

export const planFileUrl = (id: string) =>
  `/api/training-plan/files/${encodeURIComponent(id)}`;

export async function fetchPlan(): Promise<PlanResult> {
  try {
    const response = await requestJson(trainingPlanUrl);
    if (response.status === 401 || response.status === 403)
      return { status: "visitor" };
    if (!response.ok) return { status: "error", busy: response.status === 503 };
    // A deployment with no owner answers the SPA shell, not JSON.
    if (!response.headers.get("content-type")?.includes("application/json"))
      return { status: "visitor" };
    const body = (await response.json()) as {
      configured?: boolean;
      message?: string;
      plan?: TrainingPlan;
    };
    if (body.configured !== true || !body.plan)
      return { status: "unconfigured", message: body.message };
    return { status: "owner", plan: body.plan };
  } catch {
    return { status: "error", busy: false };
  }
}

export type PlanPageResult =
  | { status: "visitor" }
  | { status: "missing" }
  | { status: "owner"; page: PlaybookPage }
  | { status: "error"; busy: boolean };

export const planPageUrl = (id: string) =>
  `/api/training-plan/pages/${encodeURIComponent(id)}`;

/** One linked page, as blocks the playbook's renderer draws. */
export async function fetchPlanPage(
  id: string,
  signal?: AbortSignal,
): Promise<PlanPageResult> {
  try {
    const response = await requestJson(planPageUrl(id), signal);
    if (response.status === 401 || response.status === 403)
      return { status: "visitor" };
    if (response.status === 404) return { status: "missing" };
    if (!response.ok) return { status: "error", busy: response.status === 503 };
    return { status: "owner", page: (await response.json()) as PlaybookPage };
  } catch {
    return { status: "error", busy: false };
  }
}
