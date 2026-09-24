import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import DownloadIcon from "@mui/icons-material/Download";
import DriveFileMoveOutlinedIcon from "@mui/icons-material/DriveFileMoveOutlined";
import EditIcon from "@mui/icons-material/Edit";
import SearchIcon from "@mui/icons-material/Search";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import IconButton from "@mui/material/IconButton";
import InputAdornment from "@mui/material/InputAdornment";
import Link from "@mui/material/Link";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemText from "@mui/material/ListItemText";
import Menu from "@mui/material/Menu";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import SignInToSee from "../../progress/components/SignInToSee";
import { useOwnerDocument } from "../../progress/hooks/useOwnerDocument";
import { exportBookmarks, mergeLinks, parseBookmarks } from "../core/bookmarks";
import { fileNameFor, planEmail } from "../core/email";
import {
  addFolder,
  addLink,
  type Conflict,
  coerce,
  folderPath,
  GENERAL,
  groupsOf,
  hostOf,
  inFolder,
  inGroup,
  type LinkEntry,
  type LinksDocument,
  moveLink,
  type NewLink,
  normalizeUrl,
  removeFolder,
  removeLink,
  renameFolder,
  resolveConflict,
  search,
  tagsOf,
  titleOf,
  updateLink,
  withTag,
} from "../core/links";
import Conflicts from "./Conflicts";

const documentKey = "links";

/**
 * The bookmark list, from the owner's chair and from a visitor's.
 *
 * The document arrives through the same hook the study plans use, so a
 * visitor is told to sign in and never sends a write, and the owner's
 * changes are saved a beat after each one and reloaded when the page comes
 * back into view from another device. Every change is applied to the
 * document the hook holds at that moment, never to the one this render
 * saw, so a change made while a reload is landing cannot put the old copy
 * back. The list itself is arithmetic in core/links.ts and the file
 * handling in core/bookmarks.ts; this component renders and hands back.
 */
export default function LinksPanel() {
  const { status, value, set, saving, failed } =
    useOwnerDocument<LinksDocument>(documentKey);
  const document = React.useMemo(() => coerce(value), [value]);
  const [query, setQuery] = React.useState("");
  const [title, setTitle] = React.useState("");
  const [url, setUrl] = React.useState("");
  // Kept between adds on purpose: filing several links under one heading is
  // the common case, and the field is in plain view.
  const [group, setGroup] = React.useState("");
  const [note, setNote] = React.useState("");
  // Kept between adds like the group, for the same reason.
  const [tags, setTags] = React.useState("");
  // One tag narrows the page, and Download and Email with it.
  const [tag, setTag] = React.useState<string | null>(null);
  const [problem, setProblem] = React.useState<string | null>(null);
  const [report, setReport] = React.useState<string | null>(null);
  const [copied, setCopied] = React.useState<string | null>(null);
  const [editing, setEditing] = React.useState<string | null>(null);
  const [draft, setDraft] = React.useState<NewLink>({
    title: "",
    url: "",
    group: "",
    note: "",
    tags: "",
  });
  // Folders: one being made, one being renamed, and the link whose move
  // menu is open. A folder is a group's name, so all three are strings.
  const [folder, setFolder] = React.useState("");
  const [renaming, setRenaming] = React.useState<string | null>(null);
  const [renamed, setRenamed] = React.useState("");
  const [moving, setMoving] = React.useState<{
    id: string;
    anchor: HTMLElement;
  } | null>(null);
  const fileInput = React.useRef<HTMLInputElement | null>(null);

  const change = React.useCallback(
    (next: (current: LinksDocument) => LinksDocument) => {
      set((current: LinksDocument | null) => next(coerce(current)));
    },
    [set],
  );

  // What the page shows: one tag's links when a tag is chosen, then the
  // search on top. Download and Email take the same view, so "MITRE, then
  // Download" is a file of just those.
  const shown = React.useMemo(
    () => search(withTag(document, tag), query),
    [document, tag, query],
  );
  const narrowed = tag !== null || query.trim() !== "";
  const groups = React.useMemo(() => groupsOf(shown), [shown]);
  const knownGroups = React.useMemo(
    () => groupsOf(document).filter((g) => g !== GENERAL),
    [document],
  );
  const knownTags = React.useMemo(() => tagsOf(document), [document]);
  // Where a link can go: the general list is always a choice, whether or
  // not anything is in it at the moment.
  const moveTargets = React.useMemo(
    () => [GENERAL, ...groupsOf(document).filter((g) => g !== GENERAL)],
    [document],
  );

  const add = (event: React.FormEvent) => {
    event.preventDefault();
    if (normalizeUrl(url) === null) {
      setProblem(
        "That is not a web address. It needs a host, like abera.tech.",
      );
      return;
    }
    setProblem(null);
    const link = { title, url, group, note, tags };
    change((current) => addLink(current, link));
    setTitle("");
    setUrl("");
    setNote("");
  };

  const makeFolder = (event: React.FormEvent) => {
    event.preventDefault();
    if (folder.trim() === "") return;
    change((current) => addFolder(current, folder));
    setFolder("");
  };

  const saveRename = (event: React.FormEvent) => {
    event.preventDefault();
    if (renaming === null) return;
    const from = renaming;
    const to = renamed;
    change((current) => renameFolder(current, from, to));
    setRenaming(null);
  };

  const move = (id: string, group: string) => {
    change((current) => moveLink(current, id, group));
    setMoving(null);
  };

  const startEdit = (id: string, link: NewLink) => {
    setEditing(id);
    setDraft({
      title: link.title,
      url: link.url,
      group: link.group ?? "",
      note: link.note ?? "",
      tags: Array.isArray(link.tags) ? link.tags.join(", ") : (link.tags ?? ""),
    });
  };

  const keepMine = (conflict: Conflict) =>
    change((d) => resolveConflict(d, conflict.id, { choice: "mine" }));
  const takeTheirs = (conflict: Conflict) =>
    change((d) => resolveConflict(d, conflict.id, { choice: "theirs" }));
  // Edit opens the link's own form with the file's values in it; saving
  // the form settles every conflict on that link (see saveEdit).
  const editConflict = (conflict: Conflict, link: LinkEntry) =>
    startEdit(link.id, {
      title: conflict.theirs.title ?? link.title,
      url: link.url,
      group: conflict.theirs.group ?? link.group,
      note: conflict.theirs.note ?? link.note,
      tags: link.tags,
    });

  const saveEdit = (event: React.FormEvent) => {
    event.preventDefault();
    if (editing === null) return;
    if (normalizeUrl(draft.url) === null) {
      setProblem(
        "That is not a web address. It needs a host, like abera.tech.",
      );
      return;
    }
    setProblem(null);
    const id = editing;
    const fields = draft;
    change((current) => {
      const updated = updateLink(current, id, fields);
      return {
        ...updated,
        conflicts: updated.conflicts.filter((c) => c.linkId !== id),
      };
    });
    setEditing(null);
  };

  const copy = async (id: string, href: string) => {
    try {
      await navigator.clipboard.writeText(href);
      setCopied(id);
      window.setTimeout(() => setCopied(null), 1500);
    } catch {
      setCopied(null);
    }
  };

  const save = (what: LinksDocument, named: string | null) => {
    const blob = new Blob([exportBookmarks(what)], {
      type: "text/html;charset=utf-8",
    });
    const href = URL.createObjectURL(blob);
    const anchor = window.document.createElement("a");
    anchor.href = href;
    anchor.download = fileNameFor(new Date(), named);
    anchor.click();
    URL.revokeObjectURL(href);
  };

  const download = () => save(narrowed ? shown : document, tag);

  // One folder as its own file, with everything inside it: the MITRE
  // computer takes links-mitre-<date>.html and nothing else.
  const downloadFolder = (heading: string) =>
    save(
      {
        ...document,
        links: document.links.filter((l) => inFolder(l.group, heading)),
      },
      heading,
    );

  // Email the list to yourself: the file through the share sheet where the
  // browser has one, otherwise a mailto whose body is the file under
  // instructions for saving it. See core/email.ts for the ceiling.
  const email = async () => {
    const plan = planEmail(
      narrowed ? shown : document,
      new Date(),
      window.navigator,
      tag,
    );
    if (plan.kind === "share") {
      try {
        await window.navigator.share({
          files: [plan.file],
          title: plan.title,
          text: plan.text,
        });
      } catch {
        // The sheet was dismissed, or refused the file. Nothing to report.
      }
      return;
    }
    if (plan.kind === "mailto") {
      const anchor = window.document.createElement("a");
      anchor.href = plan.href;
      anchor.click();
      return;
    }
    try {
      await navigator.clipboard.writeText(plan.text);
      setProblem(null);
      setReport(
        `Too long for a mail body, so it is on the clipboard. Paste it into an email to yourself; the instructions at the top say how to save it as ${plan.fileName}.`,
      );
    } catch {
      setProblem("Could not copy to the clipboard. Use Download instead.");
    }
  };

  const upload = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    const incoming = parseBookmarks(await file.text());
    if (incoming.length === 0) {
      setReport(null);
      setProblem(
        `No bookmarks found in ${file.name}. A browser's bookmark export, or a download from this page, will work.`,
      );
      return;
    }
    setProblem(null);
    // Counted against this render's copy for the message; the change itself
    // is applied to whatever the hook holds, and merging is idempotent.
    const preview = mergeLinks(document, incoming, new Date(), file.name);
    change(
      (current) =>
        mergeLinks(current, incoming, new Date(), file.name).document,
    );
    const parts = [
      `${preview.added} added`,
      `${preview.updated} updated`,
      `${preview.unchanged} already here`,
    ];
    if (preview.refused > 0) parts.push(`${preview.refused} not web addresses`);
    if (preview.conflicts > 0) parts.push(`${preview.conflicts} to resolve`);
    setReport(`${file.name}: ${parts.join(", ")}.`);
  };

  if (status === "loading") {
    return <CircularProgress size={28} aria-label="Loading" />;
  }

  if (status === "error") {
    return (
      <Alert severity="error">The server did not answer. Try a reload.</Alert>
    );
  }

  if (status === "visitor") {
    return (
      <SignInToSee
        message="This list is the owner's. Sign in to see it."
        returnUrl="/links"
      />
    );
  }

  const saveState = failed
    ? { label: "Not saved", color: "error" as const }
    : saving
      ? { label: "Saving…", color: "default" as const }
      : { label: "Saved", color: "success" as const };

  return (
    <Stack spacing={4}>
      <Box
        component="form"
        onSubmit={add}
        aria-label="Add a link"
        sx={{ display: "grid", gap: 1.5 }}
      >
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
          <TextField
            label="Address"
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            required
            fullWidth
            size="small"
            slotProps={{
              htmlInput: { inputMode: "url", autoCapitalize: "none" },
            }}
          />
          <TextField
            label="Title"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            fullWidth
            size="small"
          />
        </Stack>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
          <TextField
            label="Group"
            value={group}
            onChange={(e) => setGroup(e.target.value)}
            size="small"
            sx={{ minWidth: { sm: 200 } }}
            slotProps={{ htmlInput: { list: "links-groups" } }}
          />
          <datalist id="links-groups">
            {knownGroups.map((g) => (
              <option key={g} value={g} />
            ))}
          </datalist>
          <TextField
            label="Tags"
            value={tags}
            onChange={(e) => setTags(e.target.value)}
            size="small"
            placeholder="MITRE, army"
            sx={{ minWidth: { sm: 180 } }}
            slotProps={{ htmlInput: { list: "links-tags" } }}
          />
          <datalist id="links-tags">
            {knownTags.map((t) => (
              <option key={t} value={t} />
            ))}
          </datalist>
          <TextField
            label="Note"
            value={note}
            onChange={(e) => setNote(e.target.value)}
            fullWidth
            size="small"
          />
          <Button type="submit" variant="contained" sx={{ flexShrink: 0 }}>
            Add
          </Button>
        </Stack>
        {problem && (
          <Alert severity="warning" onClose={() => setProblem(null)}>
            {problem}
          </Alert>
        )}
      </Box>

      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={1.5}
        sx={{ alignItems: { sm: "center" } }}
      >
        <TextField
          label="Find"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          size="small"
          fullWidth
          slotProps={{
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" />
                </InputAdornment>
              ),
            },
          }}
        />
        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
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
            accept=".html,.htm,.json,text/html,application/json"
            onChange={upload}
            hidden
            aria-label="Bookmark file to upload"
          />
          <Button
            variant="outlined"
            size="small"
            onClick={download}
            disabled={document.links.length === 0}
          >
            Download
          </Button>
          <Button
            variant="outlined"
            size="small"
            onClick={email}
            disabled={document.links.length === 0}
          >
            Email
          </Button>
          <Chip
            size="small"
            label={saveState.label}
            color={saveState.color}
            variant="outlined"
            sx={{ flexShrink: 0 }}
          />
        </Stack>
      </Stack>

      {report && (
        <Alert severity="success" onClose={() => setReport(null)}>
          {report}
        </Alert>
      )}

      <Conflicts
        conflicts={document.conflicts}
        links={document.links}
        onKeepMine={keepMine}
        onTakeTheirs={takeTheirs}
        onEdit={editConflict}
      />

      <Box
        component="form"
        onSubmit={makeFolder}
        aria-label="New folder"
        sx={{ display: "flex", gap: 1.5, alignItems: "center" }}
      >
        <TextField
          label="New folder"
          value={folder}
          onChange={(e) => setFolder(e.target.value)}
          size="small"
          placeholder="Work / Tools"
          helperText="A slash makes a folder inside another."
          sx={{ minWidth: { sm: 280 } }}
        />
        <Button type="submit" variant="outlined" size="small">
          Make folder
        </Button>
      </Box>

      {knownTags.length > 0 && (
        <Stack
          direction="row"
          spacing={1}
          role="group"
          aria-label="Tags"
          sx={{ flexWrap: "wrap", rowGap: 1 }}
        >
          <Chip
            label="All"
            size="small"
            color={tag === null ? "primary" : "default"}
            variant={tag === null ? "filled" : "outlined"}
            onClick={() => setTag(null)}
          />
          {knownTags.map((t) => (
            <Chip
              key={t}
              label={t}
              size="small"
              color={tag === t ? "primary" : "default"}
              variant={tag === t ? "filled" : "outlined"}
              onClick={() => setTag(tag === t ? null : t)}
            />
          ))}
        </Stack>
      )}

      {document.links.length === 0 && (
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Nothing here yet. Paste an address above, or upload a browser's
          bookmark export, and it will be here on every device you sign in from.
        </Typography>
      )}

      {document.links.length > 0 && shown.links.length === 0 && (
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Nothing matches “{query}”.
        </Typography>
      )}

      {groups.map((heading) => {
        const held = inGroup(shown, heading);
        // A folder with nothing in it is a place to move links to, so it is
        // on the page. While a search or a tag is narrowing it, it is not.
        if (held.length === 0 && (narrowed || heading === GENERAL)) return null;
        const depth = folderPath(heading).length;
        const leaf = depth === 0 ? heading : folderPath(heading)[depth - 1];
        return (
          <Box key={heading} sx={{ ml: depth > 1 ? (depth - 1) * 2 : 0 }}>
            {renaming === heading ? (
              <Box
                component="form"
                onSubmit={saveRename}
                aria-label={`Rename ${heading}`}
                sx={{ display: "flex", gap: 1, alignItems: "center", py: 1 }}
              >
                <TextField
                  label="Folder"
                  value={renamed}
                  onChange={(e) => setRenamed(e.target.value)}
                  size="small"
                  autoFocus
                  sx={{ minWidth: { sm: 280 } }}
                />
                <Button type="submit" variant="contained" size="small">
                  Save
                </Button>
                <Button
                  type="button"
                  variant="outlined"
                  size="small"
                  onClick={() => setRenaming(null)}
                >
                  Cancel
                </Button>
              </Box>
            ) : (
              <Stack
                direction="row"
                spacing={0.5}
                sx={{ alignItems: "center", minHeight: 36 }}
              >
                <Typography
                  variant="overline"
                  component="h2"
                  sx={{ color: "text.secondary", letterSpacing: 1 }}
                >
                  {leaf}
                </Typography>
                {heading !== GENERAL && (
                  <>
                    <IconButton
                      size="small"
                      aria-label={`Download folder ${heading}`}
                      onClick={() => downloadFolder(heading)}
                    >
                      <DownloadIcon fontSize="small" />
                    </IconButton>
                    <IconButton
                      size="small"
                      aria-label={`Rename folder ${heading}`}
                      onClick={() => {
                        setRenaming(heading);
                        setRenamed(heading);
                      }}
                    >
                      <EditIcon fontSize="small" />
                    </IconButton>
                    <IconButton
                      size="small"
                      aria-label={`Remove folder ${heading}`}
                      onClick={() => change((d) => removeFolder(d, heading))}
                    >
                      <DeleteOutlineIcon fontSize="small" />
                    </IconButton>
                  </>
                )}
              </Stack>
            )}
            {held.length === 0 && (
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                Nothing in here yet. Move a link in, or remove the folder: its
                links go up one level, never away.
              </Typography>
            )}
            <List
              aria-label={`Links under ${heading}`}
              dense
              disablePadding
              sx={{ borderTop: 1, borderColor: "divider" }}
            >
              {held.map((link) =>
                editing === link.id ? (
                  <ListItem key={link.id} divider disableGutters>
                    <Box
                      component="form"
                      onSubmit={saveEdit}
                      aria-label={`Edit ${titleOf(link)}`}
                      sx={{ display: "grid", gap: 1, width: "100%", py: 1 }}
                    >
                      <Stack
                        direction={{ xs: "column", sm: "row" }}
                        spacing={1}
                      >
                        <TextField
                          label="Address"
                          value={draft.url}
                          onChange={(e) =>
                            setDraft({ ...draft, url: e.target.value })
                          }
                          required
                          fullWidth
                          size="small"
                        />
                        <TextField
                          label="Title"
                          value={draft.title}
                          onChange={(e) =>
                            setDraft({ ...draft, title: e.target.value })
                          }
                          fullWidth
                          size="small"
                        />
                      </Stack>
                      <Stack
                        direction={{ xs: "column", sm: "row" }}
                        spacing={1}
                      >
                        <TextField
                          label="Group"
                          value={draft.group ?? ""}
                          onChange={(e) =>
                            setDraft({ ...draft, group: e.target.value })
                          }
                          size="small"
                          sx={{ minWidth: { sm: 200 } }}
                          slotProps={{ htmlInput: { list: "links-groups" } }}
                        />
                        <TextField
                          label="Tags"
                          value={
                            Array.isArray(draft.tags)
                              ? draft.tags.join(", ")
                              : (draft.tags ?? "")
                          }
                          onChange={(e) =>
                            setDraft({ ...draft, tags: e.target.value })
                          }
                          size="small"
                          sx={{ minWidth: { sm: 180 } }}
                          slotProps={{ htmlInput: { list: "links-tags" } }}
                        />
                        <TextField
                          label="Note"
                          value={draft.note ?? ""}
                          onChange={(e) =>
                            setDraft({ ...draft, note: e.target.value })
                          }
                          fullWidth
                          size="small"
                        />
                        <Button type="submit" variant="contained" size="small">
                          Save
                        </Button>
                        <Button
                          type="button"
                          variant="outlined"
                          size="small"
                          onClick={() => setEditing(null)}
                        >
                          Cancel
                        </Button>
                      </Stack>
                    </Box>
                  </ListItem>
                ) : (
                  <ListItem
                    key={link.id}
                    divider
                    disableGutters
                    secondaryAction={
                      <Stack direction="row" spacing={0.5}>
                        <IconButton
                          size="small"
                          aria-label={`Copy ${titleOf(link)}`}
                          onClick={() => copy(link.id, link.url)}
                          color={copied === link.id ? "success" : "default"}
                        >
                          <ContentCopyIcon fontSize="small" />
                        </IconButton>
                        <IconButton
                          size="small"
                          aria-label={`Move ${titleOf(link)}`}
                          onClick={(e) =>
                            setMoving({ id: link.id, anchor: e.currentTarget })
                          }
                        >
                          <DriveFileMoveOutlinedIcon fontSize="small" />
                        </IconButton>
                        <IconButton
                          size="small"
                          aria-label={`Edit ${titleOf(link)}`}
                          onClick={() => startEdit(link.id, link)}
                        >
                          <EditIcon fontSize="small" />
                        </IconButton>
                        <IconButton
                          size="small"
                          aria-label={`Remove ${titleOf(link)}`}
                          onClick={() => change((d) => removeLink(d, link.id))}
                        >
                          <DeleteOutlineIcon fontSize="small" />
                        </IconButton>
                      </Stack>
                    }
                    sx={{ pr: 18 }}
                  >
                    <ListItemText
                      primary={
                        <Link
                          href={link.url}
                          target="_blank"
                          rel="noopener noreferrer"
                          underline="hover"
                          sx={{ fontWeight: 500 }}
                        >
                          {titleOf(link)}
                        </Link>
                      }
                      secondary={
                        // A row of its own, laid out across the line. The
                        // chips are children of a block, so without this
                        // each one stretches to the row's whole width.
                        <Box
                          component="span"
                          sx={{
                            display: "flex",
                            flexWrap: "wrap",
                            alignItems: "center",
                            gap: 1,
                          }}
                        >
                          <Box component="span">
                            {link.note
                              ? `${hostOf(link.url)} · ${link.note}`
                              : hostOf(link.url)}
                          </Box>
                          {link.tags.map((t) => (
                            <Chip
                              key={t}
                              label={t}
                              size="small"
                              variant="outlined"
                              component="span"
                              onClick={() => setTag(t)}
                              sx={{ height: 20, fontSize: "0.7rem" }}
                            />
                          ))}
                        </Box>
                      }
                    />
                  </ListItem>
                ),
              )}
            </List>
          </Box>
        );
      })}

      <Menu
        open={moving !== null}
        anchorEl={moving?.anchor ?? null}
        onClose={() => setMoving(null)}
        aria-label="Move to folder"
      >
        {moveTargets.map((target) => (
          <MenuItem
            key={target}
            onClick={() =>
              move(moving?.id ?? "", target === GENERAL ? "" : target)
            }
          >
            {target}
          </MenuItem>
        ))}
      </Menu>
    </Stack>
  );
}
