/**
 * The owner's network: people, the organizations they belong to, and the
 * sectors those organizations fall in. One JSON document, kept on the
 * server behind the owner's sign-in, never in the site's code.
 *
 * The page draws it (layout.ts) and lists it. The owner edits it as JSON,
 * so a session can hand over a new version without a deploy. `parse`
 * is what the editor runs before a save: a document the page could not
 * draw is refused with the reasons, and the saved copy stays as it was.
 */

export const tiers = [1, 2, 3, 4, 5] as const;
export type Tier = (typeof tiers)[number];

export const statuses = [
  "connected",
  "pending",
  "followed",
  "withdrawn",
  "blocked",
  "none",
] as const;
export type Status = (typeof statuses)[number];

export interface Sector {
  id: string;
  label: string;
}

export interface Organization {
  id: string;
  name: string;
  sector: string;
  /** What kind of place it is: company, fund, agency, lab, association, event. */
  kind?: string;
  url?: string;
  note?: string;
  /** For an event: when and where it meets. */
  when?: string;
}

export interface Person {
  id: string;
  name: string;
  role: string;
  sector: string;
  /** The organizations this person belongs to. The first is drawn nearest. */
  orgs: string[];
  /** How soon to act, 1 to 5. Drawn as the size of the node. */
  tier: Tier;
  status: Status;
  url?: string;
  why?: string;
  location?: string;
  mutuals?: number;
  /** The next step with this person, in the owner's words. */
  next?: string;
  /** The way in: ids of the people or organizations an introduction runs through. */
  via?: string[];
}

export interface NetworkDocument {
  version: 1;
  /** The day the document was last edited, YYYY-MM-DD. */
  updated: string;
  /** The plan behind the picture, free text, shown on the page. */
  plan?: string;
  sectors: Sector[];
  organizations: Organization[];
  people: Person[];
}

export const emptyNetwork = (): NetworkDocument => ({
  version: 1,
  updated: "",
  sectors: [],
  organizations: [],
  people: [],
});

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);

const text = (value: unknown): string =>
  typeof value === "string" ? value : "";

const isTier = (value: unknown): value is Tier =>
  typeof value === "number" && (tiers as readonly number[]).includes(value);

const isStatus = (value: unknown): value is Status =>
  typeof value === "string" && (statuses as readonly string[]).includes(value);

const isWebAddress = (value: string): boolean => /^https?:\/\/\S+$/.test(value);

/**
 * Whatever the server holds, as a document the page can draw. Fields that
 * are missing or the wrong shape are dropped, never guessed: a person with
 * no name is not a node.
 */
export function coerce(value: unknown): NetworkDocument {
  if (!isRecord(value)) return emptyNetwork();
  const sectors: Sector[] = [];
  if (Array.isArray(value.sectors))
    for (const entry of value.sectors)
      if (isRecord(entry) && text(entry.id) && text(entry.label))
        sectors.push({ id: text(entry.id), label: text(entry.label) });
  const organizations: Organization[] = [];
  if (Array.isArray(value.organizations))
    for (const entry of value.organizations)
      if (isRecord(entry) && text(entry.id) && text(entry.name))
        organizations.push({
          id: text(entry.id),
          name: text(entry.name),
          sector: text(entry.sector),
          ...(text(entry.kind) ? { kind: text(entry.kind) } : {}),
          ...(text(entry.url) ? { url: text(entry.url) } : {}),
          ...(text(entry.note) ? { note: text(entry.note) } : {}),
          ...(text(entry.when) ? { when: text(entry.when) } : {}),
        });
  const people: Person[] = [];
  if (Array.isArray(value.people))
    for (const entry of value.people) {
      if (!isRecord(entry) || !text(entry.id) || !text(entry.name)) continue;
      const orgs = Array.isArray(entry.orgs)
        ? entry.orgs.filter((org): org is string => typeof org === "string")
        : [];
      people.push({
        id: text(entry.id),
        name: text(entry.name),
        role: text(entry.role),
        sector: text(entry.sector),
        orgs,
        tier: isTier(entry.tier) ? entry.tier : 1,
        status: isStatus(entry.status) ? entry.status : "none",
        ...(text(entry.url) ? { url: text(entry.url) } : {}),
        ...(text(entry.why) ? { why: text(entry.why) } : {}),
        ...(text(entry.location) ? { location: text(entry.location) } : {}),
        ...(typeof entry.mutuals === "number"
          ? { mutuals: entry.mutuals }
          : {}),
        ...(text(entry.next) ? { next: text(entry.next) } : {}),
        ...(Array.isArray(entry.via)
          ? { via: entry.via.filter((v): v is string => typeof v === "string") }
          : {}),
      });
    }
  return {
    version: 1,
    updated: text(value.updated),
    ...(text(value.plan) ? { plan: text(value.plan) } : {}),
    sectors,
    organizations,
    people,
  };
}

export type Parsed =
  | { ok: true; document: NetworkDocument }
  | { ok: false; problems: string[] };

/**
 * The editor's check. Every problem is named, so one Apply shows all of
 * them, and a document with any problem is not applied.
 */
export function parse(json: string): Parsed {
  let value: unknown;
  try {
    value = JSON.parse(json);
  } catch (error) {
    return { ok: false, problems: [`Not JSON: ${(error as Error).message}`] };
  }
  if (!isRecord(value))
    return { ok: false, problems: ["The document is a JSON object."] };

  const problems: string[] = [];
  for (const list of ["sectors", "organizations", "people"] as const)
    if (!Array.isArray(value[list])) problems.push(`"${list}" is a list.`);
  if (problems.length > 0) return { ok: false, problems };

  const document = coerce(value);
  const sectorIds = new Set<string>();
  for (const [index, entry] of (value.sectors as unknown[]).entries()) {
    if (!isRecord(entry) || !text(entry.id) || !text(entry.label))
      problems.push(`sectors[${index}] needs an id and a label.`);
    else if (sectorIds.has(text(entry.id)))
      problems.push(`Sector "${text(entry.id)}" appears twice.`);
    else sectorIds.add(text(entry.id));
  }
  const orgIds = new Set<string>();
  for (const [index, entry] of (value.organizations as unknown[]).entries()) {
    if (!isRecord(entry) || !text(entry.id) || !text(entry.name)) {
      problems.push(`organizations[${index}] needs an id and a name.`);
      continue;
    }
    const id = text(entry.id);
    if (orgIds.has(id)) problems.push(`Organization "${id}" appears twice.`);
    orgIds.add(id);
    if (!sectorIds.has(text(entry.sector)))
      problems.push(
        `Organization "${id}" names a sector that is not listed: "${text(entry.sector)}".`,
      );
    if (text(entry.url) && !isWebAddress(text(entry.url)))
      problems.push(
        `Organization "${id}" has a url that is not http or https.`,
      );
  }
  const personIds = new Set<string>();
  for (const [index, entry] of (value.people as unknown[]).entries()) {
    if (!isRecord(entry) || !text(entry.id) || !text(entry.name)) {
      problems.push(`people[${index}] needs an id and a name.`);
      continue;
    }
    const id = text(entry.id);
    if (personIds.has(id)) problems.push(`Person "${id}" appears twice.`);
    personIds.add(id);
    if (!sectorIds.has(text(entry.sector)))
      problems.push(
        `Person "${id}" names a sector that is not listed: "${text(entry.sector)}".`,
      );
    if (!isTier(entry.tier))
      problems.push(`Person "${id}" has a tier that is not 1 to 5.`);
    if (!isStatus(entry.status))
      problems.push(
        `Person "${id}" has a status that is not one of ${statuses.join(", ")}.`,
      );
    if (entry.orgs !== undefined && !Array.isArray(entry.orgs))
      problems.push(`Person "${id}" has orgs that is not a list.`);
    else
      for (const org of (entry.orgs as unknown[] | undefined) ?? [])
        if (typeof org !== "string" || !orgIds.has(org))
          problems.push(
            `Person "${id}" names an organization that is not listed: "${String(org)}".`,
          );
    if (text(entry.url) && !isWebAddress(text(entry.url)))
      problems.push(`Person "${id}" has a url that is not http or https.`);
  }
  const known = new Set([...personIds, ...orgIds]);
  for (const person of document.people)
    for (const via of person.via ?? [])
      if (!known.has(via))
        problems.push(
          `Person "${person.id}" names a way in that is not listed: "${via}".`,
        );
  return problems.length > 0 ? { ok: false, problems } : { ok: true, document };
}

/** The document as the editor shows it. */
export const stringify = (document: NetworkDocument): string =>
  JSON.stringify(document, null, 2);

export interface Counts {
  people: number;
  organizations: number;
  byStatus: Record<Status, number>;
}

export function counts(document: NetworkDocument): Counts {
  const byStatus = Object.fromEntries(statuses.map((s) => [s, 0])) as Record<
    Status,
    number
  >;
  for (const person of document.people) byStatus[person.status] += 1;
  return {
    people: document.people.length,
    organizations: document.organizations.length,
    byStatus,
  };
}

/**
 * A document to start from: the shape, with made-up names, so the first
 * edit is a replacement and not a blank page.
 */
export const example = (): NetworkDocument => ({
  version: 1,
  updated: "2026-01-01",
  plan: "Meet the engineering sector in person this quarter. Capital after the first introductions land.",
  sectors: [
    { id: "engineering", label: "Engineering" },
    { id: "capital", label: "Capital" },
  ],
  organizations: [
    {
      id: "acme",
      name: "Acme Radio",
      sector: "engineering",
      kind: "company",
      url: "https://example.com/acme",
    },
    { id: "north-fund", name: "North Fund", sector: "capital", kind: "fund" },
    {
      id: "radio-day",
      name: "Radio Day",
      sector: "engineering",
      kind: "event",
      when: "Monthly, Annapolis Junction",
    },
  ],
  people: [
    {
      id: "ada",
      name: "Ada Example",
      role: "Chief Engineer, Acme Radio",
      sector: "engineering",
      orgs: ["acme"],
      tier: 5,
      status: "pending",
      url: "https://example.com/ada",
      why: "Runs the team that builds what you build.",
      location: "Arlington, VA",
      mutuals: 3,
      next: "Say hello at Radio Day.",
      via: ["radio-day"],
    },
    {
      id: "bo",
      name: "Bo Example",
      role: "Partner, North Fund",
      sector: "capital",
      orgs: ["north-fund", "acme"],
      tier: 3,
      status: "followed",
      why: "Backs early companies in the field.",
    },
  ],
});
