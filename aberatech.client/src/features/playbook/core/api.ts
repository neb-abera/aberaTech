/**
 * The playbook, from the page's side: the page tree, one page's blocks,
 * and the address a file downloads from.
 *
 * A 401 or 403 is the answer "you are a visitor", not an error, and the
 * page shows the sign-in button on it. A deployment without a Notion token
 * answers `configured: false`, and the page says Notion is not connected.
 */

import { requestJson } from "../../../site/earlyRequest";

export interface PlaybookNode {
  id: string;
  title: string;
  kind: "page" | "database";
  children: PlaybookNode[];
}

export interface PlaybookText {
  text: string;
  bold?: boolean;
  italic?: boolean;
  strikethrough?: boolean;
  underline?: boolean;
  code?: boolean;
  href?: string;
}

/** One block, in the shapes the server sends (PlaybookModel.cs). */
export interface PlaybookBlock {
  type: string;
  id?: string;
  level?: number;
  text?: PlaybookText[];
  checked?: boolean;
  icon?: string;
  language?: string;
  title?: string;
  name?: string;
  kind?: string;
  url?: string;
  caption?: PlaybookText[];
  headerRow?: boolean;
  headerColumn?: boolean;
  rows?: PlaybookText[][][];
  pages?: PlaybookNode[];
  notionType?: string;
  children?: PlaybookBlock[];
}

export interface PlaybookPage {
  id: string;
  title: string;
  blocks: PlaybookBlock[];
}

export type TreeResult =
  | { status: "visitor" }
  | { status: "unconfigured" }
  | { status: "owner"; root: PlaybookNode }
  | { status: "error"; busy: boolean };

export type PageResult =
  | { status: "visitor" }
  | { status: "missing" }
  | { status: "owner"; page: PlaybookPage }
  | { status: "error"; busy: boolean };

/** The first thing the page asks. The head script asks it first (site/meta.ts). */
export const playbookTreeUrl = "/api/playbook";

export const pageUrl = (id: string) =>
  `/api/playbook/pages/${encodeURIComponent(id)}`;

export const fileUrl = (blockId: string) =>
  `/api/playbook/files/${encodeURIComponent(blockId)}`;

export async function fetchTree(): Promise<TreeResult> {
  try {
    const response = await requestJson(playbookTreeUrl);
    if (response.status === 401 || response.status === 403)
      return { status: "visitor" };
    if (!response.ok) return { status: "error", busy: response.status === 503 };
    // A deployment with no owner answers the SPA shell, not JSON.
    if (!response.headers.get("content-type")?.includes("application/json"))
      return { status: "visitor" };
    const body = (await response.json()) as {
      configured?: boolean;
      root?: PlaybookNode;
    };
    if (body.configured !== true || !body.root)
      return { status: "unconfigured" };
    return { status: "owner", root: body.root };
  } catch {
    return { status: "error", busy: false };
  }
}

export async function fetchPage(
  id: string,
  signal?: AbortSignal,
): Promise<PageResult> {
  try {
    const response = await requestJson(pageUrl(id), signal);
    if (response.status === 401 || response.status === 403)
      return { status: "visitor" };
    if (response.status === 404) return { status: "missing" };
    if (!response.ok) return { status: "error", busy: response.status === 503 };
    return { status: "owner", page: (await response.json()) as PlaybookPage };
  } catch {
    return { status: "error", busy: false };
  }
}

/** Every page in the tree, depth first, so the page can find one by id. */
export function findPage(
  root: PlaybookNode,
  id: string,
): PlaybookNode | undefined {
  if (root.kind === "page" && root.id === id) return root;
  for (const child of root.children) {
    const found = findPage(child, id);
    if (found) return found;
  }
  return undefined;
}
