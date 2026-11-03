/**
 * The President's Hundred plan, from the page's side: one Notion page as
 * blocks, and its training log rows.
 *
 * A 401 or 403 is the answer "you are a visitor", not an error, and the
 * page shows the sign-in button on it. A deployment without the page set
 * answers `configured: false` with what to set.
 */

import { requestJson } from "../../../site/earlyRequest";
import type { PlaybookPage } from "../../playbook/core/api";

export type CellValue = string | number | boolean;

export interface LogRow {
  id: string;
  values: Record<string, CellValue>;
}

export interface LogTable {
  id: string;
  title: string;
  rows: LogRow[];
}

export type PlanResult =
  | { status: "visitor" }
  | { status: "unconfigured"; message: string }
  | { status: "owner"; page: PlaybookPage; tables: LogTable[] }
  | { status: "error"; busy: boolean };

/** The one thing the page asks. The head script asks it first (site/meta.ts). */
export const marksmanshipUrl = "/api/marksmanship";

export async function fetchPlan(signal?: AbortSignal): Promise<PlanResult> {
  try {
    const response = await requestJson(marksmanshipUrl, signal);
    if (response.status === 401 || response.status === 403)
      return { status: "visitor" };
    if (!response.ok) return { status: "error", busy: response.status === 503 };
    // A deployment with no owner answers the SPA shell, not JSON.
    if (!response.headers.get("content-type")?.includes("application/json"))
      return { status: "visitor" };
    const body = (await response.json()) as {
      configured?: boolean;
      message?: string;
      page?: PlaybookPage | null;
      tables?: LogTable[];
    };
    if (body.configured !== true || !body.page)
      return {
        status: "unconfigured",
        message: body.message ?? "The marksmanship plan is not connected.",
      };
    return { status: "owner", page: body.page, tables: body.tables ?? [] };
  } catch {
    return { status: "error", busy: false };
  }
}

/** The log's columns, in the order the plan reads them. Others follow. */
export const logColumns = [
  "Date",
  "Session",
  "Name",
  "Type",
  "Phase",
  "Course",
  "Pistol",
  "Slow fire",
  "Timed",
  "Rapid",
  "Score",
  "Total",
  "X",
  "Group in",
  "Rounds",
  "Dry minutes",
  "Calls off",
  "EIC points",
  "Notes",
];

/** The columns any row fills, known ones first, so an empty column is left out. */
export function columnsOf(rows: LogRow[]): string[] {
  const used = new Set(rows.flatMap((row) => Object.keys(row.values)));
  const known = logColumns.filter((name) => used.has(name));
  const rest = [...used].filter((name) => !logColumns.includes(name)).sort();
  return [...known, ...rest];
}

/** Newest first. A row with no date goes last, in Notion's order. */
export function newestFirst(rows: LogRow[]): LogRow[] {
  const date = (row: LogRow) =>
    typeof row.values.Date === "string" ? row.values.Date : "";
  return [...rows].sort((a, b) => date(b).localeCompare(date(a)));
}

/** The page or database in Notion, for a device that can open it. */
export function notionUrl(id: string): string {
  return `https://www.notion.so/${id.replace(/-/g, "")}`;
}

export function show(value: CellValue | undefined): string {
  if (value === undefined) return "";
  if (typeof value === "boolean") return value ? "Yes" : "No";
  return String(value);
}
