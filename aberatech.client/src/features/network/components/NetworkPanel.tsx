import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Link from "@mui/material/Link";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import { accountSignedIn } from "../../../hooks/useAccount";
import SignInToSee from "../../progress/components/SignInToSee";
import { useOwnerDocument } from "../../progress/hooks/useOwnerDocument";
import { networkDocumentKey } from "../core/document";
import { layout as place } from "../core/layout";
import {
  coerce,
  counts,
  example,
  type NetworkDocument,
  type Person,
  parse,
  type Status,
  statuses,
  stringify,
} from "../core/model";
import NetworkGraph from "./NetworkGraph";

/** One colour per sector, in the order the document lists them. */
const palette = [
  "hsl(210, 90%, 60%)",
  "hsl(28, 90%, 58%)",
  "hsl(150, 60%, 45%)",
  "hsl(285, 70%, 65%)",
  "hsl(0, 75%, 60%)",
  "hsl(185, 70%, 45%)",
  "hsl(55, 85%, 55%)",
  "hsl(330, 70%, 60%)",
];

const statusLabel: Record<Status, string> = {
  connected: "Connected",
  pending: "Invited",
  followed: "Followed",
  withdrawn: "Withdrawn",
  blocked: "Blocked",
  none: "Not yet",
};

const statusColor: Record<Status, "success" | "warning" | "default" | "error"> =
  {
    connected: "success",
    pending: "warning",
    followed: "default",
    withdrawn: "error",
    blocked: "error",
    none: "default",
  };

const bars = (tier: number) => "▮".repeat(tier) + "▯".repeat(5 - tier);

/**
 * The owner's network from two chairs. A visitor is asked to sign in and
 * the document is never requested. The owner gets the picture, the
 * filters, the detail of whatever is selected, the list, and the editor:
 * the whole document as JSON, checked before it is applied, saved a beat
 * later through the same hook as every other owner document.
 */
export default function NetworkPanel() {
  const { status, value, set, saving, failed } =
    useOwnerDocument<NetworkDocument>(networkDocumentKey, 800, accountSignedIn);
  const document = React.useMemo(() => coerce(value), [value]);
  const graph = React.useMemo(() => place(document), [document]);
  const summary = React.useMemo(() => counts(document), [document]);
  const [sectorsOff, setSectorsOff] = React.useState<ReadonlySet<string>>(
    new Set(),
  );
  const [statusesOff, setStatusesOff] = React.useState<ReadonlySet<Status>>(
    new Set(),
  );
  const [query, setQuery] = React.useState("");
  const [selected, setSelected] = React.useState<string | null>(null);
  const [editing, setEditing] = React.useState(false);
  const [draft, setDraft] = React.useState("");
  const [problems, setProblems] = React.useState<string[]>([]);
  const fileInput = React.useRef<HTMLInputElement | null>(null);

  const colorOf = React.useCallback(
    (sector: string) => {
      const index = document.sectors.findIndex((s) => s.id === sector);
      return palette[(index < 0 ? palette.length - 1 : index) % palette.length];
    },
    [document],
  );

  const visiblePeople = React.useMemo(() => {
    const needle = query.trim().toLowerCase();
    return document.people.filter(
      (person) =>
        !sectorsOff.has(person.sector) &&
        !statusesOff.has(person.status) &&
        (needle === "" ||
          `${person.name} ${person.role} ${person.why ?? ""}`
            .toLowerCase()
            .includes(needle)),
    );
  }, [document, sectorsOff, statusesOff, query]);

  const dimmed = React.useMemo(() => {
    const shown = new Set(visiblePeople.map((person) => person.id));
    const orgsShown = new Set(visiblePeople.flatMap((person) => person.orgs));
    const faint = new Set<string>();
    for (const person of document.people)
      if (!shown.has(person.id)) faint.add(person.id);
    for (const org of document.organizations)
      if (
        !orgsShown.has(org.id) &&
        (sectorsOff.has(org.sector) ||
          query.trim() !== "" ||
          statusesOff.size > 0)
      )
        faint.add(org.id);
    return faint;
  }, [document, visiblePeople, sectorsOff, statusesOff, query]);

  const apply = React.useCallback(
    (next: NetworkDocument) => {
      set(() => next);
      setSelected(null);
    },
    [set],
  );

  const openEditor = () => {
    setDraft(stringify(document));
    setProblems([]);
    setEditing(true);
  };

  const applyDraft = () => {
    const parsed = parse(draft);
    if (!parsed.ok) {
      setProblems(parsed.problems);
      return;
    }
    setProblems([]);
    apply(parsed.document);
    setEditing(false);
  };

  const upload = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    const text = await file.text();
    const parsed = parse(text);
    if (!parsed.ok) {
      setProblems([
        `${file.name}: ${parsed.problems[0]}`,
        ...parsed.problems.slice(1),
      ]);
      setDraft(text);
      setEditing(true);
      return;
    }
    setProblems([]);
    apply(parsed.document);
    setEditing(false);
  };

  const download = () => {
    const blob = new Blob([stringify(document)], { type: "application/json" });
    const href = URL.createObjectURL(blob);
    const anchor = window.document.createElement("a");
    anchor.href = href;
    anchor.download = `network-${new Date().toISOString().slice(0, 10)}.json`;
    anchor.click();
    URL.revokeObjectURL(href);
  };

  if (status === "loading") {
    return <CircularProgress size={28} aria-label="Loading network" />;
  }

  if (status === "error") {
    return (
      <Alert severity="error">The server did not answer. Try a reload.</Alert>
    );
  }

  if (status === "visitor") {
    return (
      <SignInToSee
        message="This page is the owner's. Sign in to see it."
        returnUrl="/network"
      />
    );
  }

  const saveState = failed
    ? { label: "Not saved", color: "error" as const }
    : saving
      ? { label: "Saving…", color: "default" as const }
      : { label: "Saved", color: "success" as const };

  const chosen = selected
    ? (document.people.find((p) => p.id === selected) ??
      document.organizations.find((o) => o.id === selected) ??
      null)
    : null;
  const orgName = (id: string) =>
    document.organizations.find((o) => o.id === id)?.name ?? id;
  const nameOf = (id: string) =>
    document.people.find((p) => p.id === id)?.name ?? orgName(id);
  const members = (orgId: string) =>
    document.people.filter((p) => p.orgs.includes(orgId));
  const listed = [...visiblePeople].sort(
    (a, b) => b.tier - a.tier || a.name.localeCompare(b.name),
  );
  const empty =
    document.people.length === 0 && document.organizations.length === 0;

  return (
    <Stack spacing={3}>
      <Stack
        direction="row"
        spacing={1}
        sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 1 }}
      >
        <Button
          variant={editing ? "contained" : "outlined"}
          size="small"
          onClick={editing ? () => setEditing(false) : openEditor}
        >
          {editing ? "Close editor" : "Edit data"}
        </Button>
        <Button
          variant="outlined"
          size="small"
          onClick={() => fileInput.current?.click()}
        >
          Upload
        </Button>
        <input
          ref={fileInput}
          type="file"
          accept="application/json,.json"
          hidden
          aria-label="Upload a network document"
          onChange={upload}
        />
        <Button
          variant="outlined"
          size="small"
          onClick={download}
          disabled={empty}
        >
          Download
        </Button>
        {empty && (
          <Button
            variant="outlined"
            size="small"
            onClick={() => apply(example())}
          >
            Start from the example
          </Button>
        )}
        <Chip
          size="small"
          label={saveState.label}
          color={saveState.color}
          variant="outlined"
        />
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {summary.people} people, {summary.organizations} organizations
          {document.updated ? `, edited ${document.updated}` : ""}.
        </Typography>
      </Stack>

      {problems.length > 0 && (
        <Alert severity="error" role="alert">
          <Typography variant="subtitle2">
            Not applied.{" "}
            {problems.length === 1
              ? "One problem"
              : `${problems.length} problems`}
            :
          </Typography>
          <Box component="ul" sx={{ m: 0, pl: 2.5 }}>
            {problems.map((problem) => (
              <li key={problem}>{problem}</li>
            ))}
          </Box>
        </Alert>
      )}

      {document.plan && !editing && (
        <Box
          component="section"
          aria-label="Plan"
          sx={{
            border: 1,
            borderColor: "divider",
            borderRadius: 2,
            p: 2,
            whiteSpace: "pre-wrap",
          }}
        >
          <Typography variant="subtitle2" gutterBottom>
            Plan
          </Typography>
          <Typography variant="body2">{document.plan}</Typography>
        </Box>
      )}

      {editing && (
        <Stack
          spacing={1}
          component="section"
          aria-label="Edit the network document"
        >
          <TextField
            label="Document JSON"
            multiline
            minRows={14}
            maxRows={30}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            slotProps={{
              htmlInput: {
                spellCheck: false,
                style: { fontFamily: "monospace", fontSize: 13 },
              },
            }}
          />
          <Stack direction="row" spacing={1}>
            <Button variant="contained" size="small" onClick={applyDraft}>
              Apply
            </Button>
            <Button
              variant="text"
              size="small"
              onClick={() => setDraft(stringify(example()))}
            >
              Paste the example
            </Button>
          </Stack>
        </Stack>
      )}

      {empty ? (
        <Alert severity="info">
          No network yet. Start from the example, upload a document, or open the
          editor.
        </Alert>
      ) : (
        <>
          <Stack spacing={1}>
            <Stack
              direction="row"
              spacing={1}
              sx={{ flexWrap: "wrap", rowGap: 1 }}
              aria-label="Sectors"
            >
              {document.sectors.map((sector) => {
                const off = sectorsOff.has(sector.id);
                return (
                  <Chip
                    key={sector.id}
                    label={sector.label}
                    size="small"
                    variant={off ? "outlined" : "filled"}
                    aria-pressed={!off}
                    sx={{
                      bgcolor: off ? undefined : colorOf(sector.id),
                      color: off ? undefined : "#101418",
                      borderColor: colorOf(sector.id),
                    }}
                    onClick={() =>
                      setSectorsOff((current) => {
                        const next = new Set(current);
                        if (next.has(sector.id)) next.delete(sector.id);
                        else next.add(sector.id);
                        return next;
                      })
                    }
                  />
                );
              })}
            </Stack>
            <Stack
              direction="row"
              spacing={1}
              sx={{ flexWrap: "wrap", rowGap: 1, alignItems: "center" }}
              aria-label="Standing"
            >
              {statuses
                .filter((s) => summary.byStatus[s] > 0)
                .map((s) => {
                  const off = statusesOff.has(s);
                  return (
                    <Chip
                      key={s}
                      label={`${statusLabel[s]} ${summary.byStatus[s]}`}
                      size="small"
                      color={off ? "default" : statusColor[s]}
                      variant={off ? "outlined" : "filled"}
                      aria-pressed={!off}
                      onClick={() =>
                        setStatusesOff((current) => {
                          const next = new Set(current);
                          if (next.has(s)) next.delete(s);
                          else next.add(s);
                          return next;
                        })
                      }
                    />
                  );
                })}
              <TextField
                size="small"
                label="Find"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                sx={{ minWidth: 180 }}
              />
            </Stack>
          </Stack>

          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr", md: "minmax(0, 1fr) 300px" },
              gap: 2,
              alignItems: "start",
            }}
          >
            <NetworkGraph
              layout={graph}
              dimmed={dimmed}
              selected={selected}
              onSelect={setSelected}
              colorOf={colorOf}
              summary={`${summary.people} people and ${summary.organizations} organizations`}
            />
            <Box
              component="aside"
              aria-label="Selected"
              sx={{
                border: 1,
                borderColor: "divider",
                borderRadius: 2,
                p: 2,
                minHeight: 120,
              }}
            >
              {chosen === null ? (
                <Typography variant="body2" sx={{ color: "text.secondary" }}>
                  Select a node for the detail. Bigger is sooner. A solid ring
                  is connected, a dashed ring is invited, a thin ring is
                  followed.
                </Typography>
              ) : "role" in chosen ? (
                <PersonDetail
                  person={chosen}
                  orgName={orgName}
                  nameOf={nameOf}
                />
              ) : (
                <Stack spacing={1}>
                  <Typography variant="h6" component="h2">
                    {chosen.name}
                  </Typography>
                  <Typography variant="body2" sx={{ color: "text.secondary" }}>
                    {chosen.kind ?? "organization"}
                    {chosen.when ? `. ${chosen.when}` : ""}
                    {chosen.note ? `. ${chosen.note}` : ""}
                  </Typography>
                  <Typography variant="body2">
                    {members(chosen.id).length}{" "}
                    {members(chosen.id).length === 1 ? "person" : "people"}:{" "}
                    {members(chosen.id)
                      .map((p) => p.name)
                      .join(", ") || "none"}
                  </Typography>
                  {chosen.url && (
                    <Link
                      href={chosen.url}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      Open page
                    </Link>
                  )}
                </Stack>
              )}
            </Box>
          </Box>

          <Box sx={{ overflowX: "auto" }}>
            <Table size="small" aria-label="People">
              <TableHead>
                <TableRow>
                  <TableCell>Person</TableCell>
                  <TableCell>Role</TableCell>
                  <TableCell>Organizations</TableCell>
                  <TableCell>Standing</TableCell>
                  <TableCell>Priority</TableCell>
                  <TableCell>Next step</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {listed.map((person) => (
                  <TableRow
                    key={person.id}
                    hover
                    selected={selected === person.id}
                    onClick={() => setSelected(person.id)}
                    sx={{ cursor: "pointer" }}
                  >
                    <TableCell>
                      {person.url ? (
                        <Link
                          href={person.url}
                          target="_blank"
                          rel="noopener noreferrer"
                          onClick={(event) => event.stopPropagation()}
                        >
                          {person.name}
                        </Link>
                      ) : (
                        person.name
                      )}
                    </TableCell>
                    <TableCell>{person.role}</TableCell>
                    <TableCell>{person.orgs.map(orgName).join(", ")}</TableCell>
                    <TableCell>
                      <Chip
                        size="small"
                        label={statusLabel[person.status]}
                        color={statusColor[person.status]}
                        variant="outlined"
                      />
                    </TableCell>
                    <TableCell
                      aria-label={`Priority ${person.tier} of 5`}
                      sx={{ fontFamily: "monospace", letterSpacing: 1 }}
                    >
                      {bars(person.tier)}
                    </TableCell>
                    <TableCell sx={{ maxWidth: 280 }}>
                      {person.next ?? ""}
                    </TableCell>
                  </TableRow>
                ))}
                {listed.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={6}>
                      Nobody matches the filters.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </Box>
        </>
      )}
    </Stack>
  );
}

function PersonDetail({
  person,
  orgName,
  nameOf,
}: {
  person: Person;
  orgName: (id: string) => string;
  nameOf: (id: string) => string;
}) {
  return (
    <Stack spacing={1}>
      <Typography variant="h6" component="h2">
        {person.name}
      </Typography>
      <Typography variant="body2">{person.role}</Typography>
      <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", rowGap: 1 }}>
        <Chip
          size="small"
          label={statusLabel[person.status]}
          color={statusColor[person.status]}
        />
        <Chip
          size="small"
          variant="outlined"
          label={`Priority ${person.tier} of 5`}
        />
        {typeof person.mutuals === "number" && (
          <Chip
            size="small"
            variant="outlined"
            label={`${person.mutuals} mutual`}
          />
        )}
      </Stack>
      {person.orgs.length > 0 && (
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {person.orgs.map(orgName).join(", ")}
        </Typography>
      )}
      {person.location && (
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {person.location}
        </Typography>
      )}
      {person.why && <Typography variant="body2">{person.why}</Typography>}
      {person.next && (
        <Typography variant="body2">
          <b>Next:</b> {person.next}
        </Typography>
      )}
      {person.via && person.via.length > 0 && (
        <Typography variant="body2">
          <b>Through:</b> {person.via.map(nameOf).join(", ")}
        </Typography>
      )}
      {person.url && (
        <Link href={person.url} target="_blank" rel="noopener noreferrer">
          Open profile
        </Link>
      )}
    </Stack>
  );
}
