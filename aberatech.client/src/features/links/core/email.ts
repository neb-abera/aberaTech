import { exportBookmarks } from "./bookmarks";
import { type LinksDocument, slugOf } from "./links";

/**
 * Emailing the list to yourself, from the page's side.
 *
 * The site has no mail sender of its own, and does not need one for this:
 * the phone's share sheet takes a file and hands it to Mail, and everything
 * else takes a mailto link. Two shapes, then, and the page picks:
 *
 * - a file, `links-<date>.html`, where the browser can share files;
 * - the file's text in the mail body, under instructions for saving it as
 *   that file, where it cannot.
 *
 * A mail body has a ceiling (mail clients drop or truncate a mailto past a
 * few tens of kilobytes), so a list too long for it goes to the clipboard
 * instead, with the same instructions, and the page says so.
 */

/** Characters of body a mailto link is trusted to carry. */
export const mailtoLimit = 30_000;

export type EmailPlan =
  | { kind: "share"; file: File; title: string; text: string }
  | { kind: "mailto"; href: string; fileName: string }
  | { kind: "copy"; text: string; fileName: string };

/** `links-<date>.html`, or `links-<tag>-<date>.html` for one tag's links. */
export function fileNameFor(date: Date, tag: string | null = null): string {
  const part = tag === null || slugOf(tag) === "" ? "" : `${slugOf(tag)}-`;
  return `links-${part}${date.toISOString().slice(0, 10)}.html`;
}

export function subjectFor(date: Date, tag: string | null = null): string {
  const part = tag === null || tag.trim() === "" ? "" : `${tag.trim()} `;
  return `Links ${part}${date.toISOString().slice(0, 10)}`;
}

/** What to do with the text below the line, in the mail itself. */
export function instructionsFor(fileName: string): string {
  return [
    `Your links from abera.tech, as a bookmark file (${fileName}).`,
    "",
    "To save it: copy everything below the line into a text editor, save it",
    `as ${fileName} (plain text, .html), then import it in any browser under`,
    "Bookmarks, Import, or upload it at https://abera.tech/links.",
    "",
    "The file begins at <!DOCTYPE NETSCAPE-Bookmark-file-1>.",
    "",
    "-----",
    "",
  ].join("\n");
}

/** The mail body: the instructions, a line, the file. */
export function bodyFor(document: LinksDocument, fileName: string): string {
  return instructionsFor(fileName) + exportBookmarks(document);
}

export function mailtoHref(subject: string, body: string): string {
  return `mailto:?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`;
}

/** Whether this browser can hand a file to the share sheet. */
export function canShareFiles(
  navigator: Pick<Navigator, "share" | "canShare"> | undefined,
  file: File,
): boolean {
  try {
    return (
      typeof navigator?.share === "function" &&
      typeof navigator.canShare === "function" &&
      navigator.canShare({ files: [file] })
    );
  } catch {
    return false;
  }
}

export function planEmail(
  document: LinksDocument,
  date: Date,
  navigator: Pick<Navigator, "share" | "canShare"> | undefined,
  tag: string | null = null,
): EmailPlan {
  const fileName = fileNameFor(date, tag);
  const subject = subjectFor(date, tag);
  const html = exportBookmarks(document);
  const file = new File([html], fileName, { type: "text/html" });

  if (canShareFiles(navigator, file)) {
    return {
      kind: "share",
      file,
      title: subject,
      text: `Your links from abera.tech. Import ${fileName} in any browser, or upload it at https://abera.tech/links.`,
    };
  }

  const body = instructionsFor(fileName) + html;
  if (body.length > mailtoLimit) {
    return { kind: "copy", text: body, fileName };
  }
  return { kind: "mailto", href: mailtoHref(subject, body), fileName };
}
