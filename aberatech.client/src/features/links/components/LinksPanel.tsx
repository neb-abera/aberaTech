import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
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
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import SignInToSee from "../../progress/components/SignInToSee";
import { useOwnerDocument } from "../../progress/hooks/useOwnerDocument";
import { exportBookmarks, mergeLinks, parseBookmarks } from "../core/bookmarks";
import {
  addLink,
  coerce,
  GENERAL,
  groupsOf,
  hostOf,
  inGroup,
  type LinksDocument,
  normalizeUrl,
  removeLink,
  search,
  titleOf,
} from "../core/links";

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
  const [problem, setProblem] = React.useState<string | null>(null);
  const [report, setReport] = React.useState<string | null>(null);
  const [copied, setCopied] = React.useState<string | null>(null);
  const fileInput = React.useRef<HTMLInputElement | null>(null);

  const change = React.useCallback(
    (next: (current: LinksDocument) => LinksDocument) => {
      set((current: LinksDocument | null) => next(coerce(current)));
    },
    [set],
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
    const link = { title, url, group, note };
    change((current) => addLink(current, link));
    setTitle("");
    setUrl("");
    setNote("");
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

  const download = () => {
    const blob = new Blob([exportBookmarks(document)], {
      type: "text/html;charset=utf-8",
    });
    const href = URL.createObjectURL(blob);
    const anchor = window.document.createElement("a");
    anchor.href = href;
    anchor.download = `links-${new Date().toISOString().slice(0, 10)}.html`;
    anchor.click();
    URL.revokeObjectURL(href);
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
    const preview = mergeLinks(document, incoming);
    change((current) => mergeLinks(current, incoming).document);
    const parts = [
      `${preview.added} added`,
      `${preview.updated} updated`,
      `${preview.unchanged} already here`,
    ];
    if (preview.refused > 0) parts.push(`${preview.refused} not web addresses`);
    setReport(`${file.name}: ${parts.join(", ")}.`);
  };

  const shown = React.useMemo(() => search(document, query), [document, query]);
  const groups = React.useMemo(() => groupsOf(shown), [shown]);
  const knownGroups = React.useMemo(
    () => groupsOf(document).filter((g) => g !== GENERAL),
    [document],
  );

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

      {groups.map((heading) => (
        <Box key={heading}>
          <Typography
            variant="overline"
            component="h2"
            sx={{ color: "text.secondary", letterSpacing: 1 }}
          >
            {heading}
          </Typography>
          <List
            aria-label={`Links under ${heading}`}
            dense
            disablePadding
            sx={{ borderTop: 1, borderColor: "divider" }}
          >
            {inGroup(shown, heading).map((link) => (
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
                      aria-label={`Remove ${titleOf(link)}`}
                      onClick={() => change((d) => removeLink(d, link.id))}
                    >
                      <DeleteOutlineIcon fontSize="small" />
                    </IconButton>
                  </Stack>
                }
                sx={{ pr: 10 }}
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
                    link.note
                      ? `${hostOf(link.url)} · ${link.note}`
                      : hostOf(link.url)
                  }
                />
              </ListItem>
            ))}
          </List>
        </Box>
      ))}
    </Stack>
  );
}
