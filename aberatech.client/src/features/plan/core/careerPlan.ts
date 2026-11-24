/**
 * The career plan, from the page's side: one Notion page as blocks, and
 * the address one of its files is served from.
 *
 * A 401 or 403 is the answer "you are a visitor", not an error, and the
 * page shows the sign-in button on it. A deployment without the page set
 * answers `configured: false` with what to set.
 */

import { requestJson } from "../../../site/earlyRequest";
import type { PlaybookPage } from "../../playbook/core/api";

export type CareerPlanResult =
  | { status: "visitor" }
  | { status: "unconfigured"; message: string }
  | { status: "owner"; page: PlaybookPage }
  | { status: "error"; busy: boolean };

export const careerPlanUrl = "/api/career-plan";

/** A file on the plan page. An image comes back to be drawn, anything else to be saved. */
export const careerPlanFileUrl = (blockId: string) =>
  `${careerPlanUrl}/files/${encodeURIComponent(blockId)}`;

export async function fetchCareerPlan(
  signal?: AbortSignal,
): Promise<CareerPlanResult> {
  try {
    const response = await requestJson(careerPlanUrl, signal);
    if (response.status === 401 || response.status === 403)
      return { status: "visitor" };
    if (
      !response.ok ||
      !response.headers.get("content-type")?.includes("application/json")
    )
      return { status: "error", busy: response.status === 503 };
    const body = (await response.json()) as {
      configured?: boolean;
      message?: string;
      page?: PlaybookPage | null;
    } | null;
    if (body?.configured !== true || !body.page)
      return {
        status: "unconfigured",
        message: body?.message ?? "The career plan is not connected.",
      };
    return { status: "owner", page: body.page };
  } catch {
    return { status: "error", busy: false };
  }
}
