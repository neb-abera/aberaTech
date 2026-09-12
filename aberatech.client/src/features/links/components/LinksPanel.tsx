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
import { useOwnerDocument } from "../../progress/hooks/useOwnerDocument";
import {
  addLink,
  coerce,
  groupsOf,
  hostOf,
  inGroup,
  type LinksDocument,
  removeLink,
  search,
  titleOf,
} from "../core/links";

export const documentKey = "links";

/**
 * The bookmark list, from the owner's chair and from a visitor's.
 *
 * The document arrives through the same hook the study plans use, so a
 * visitor is told to sign in and never sends a write, and the owner's
 * changes are saved a beat after each one and reloaded when the page comes
 * back into view from another device. The list itself is arithmetic in
 * core/links.ts; this component only renders it and hands changes back.
 */
export default function LinksPanel() {
  const { status, value, set, saving } =
    useOwnerDocument<LinksDocument>(documentKey);
  const document = React.useMemo(() => coerce(value), [value]);
  const [query, setQuery] = React.useState("");
  const [title, setTitle] = React.useState("");
  const [url, setUrl] = React.useState("");
  const [group, setGroup] = React.useState("");
  const [note, setNote] = React.useState("");
  const [problem, setProblem] = React.useState<string | null>(null);
  const [copied, setCopied] = React.useState<string | null>(null);

  const change = React.useCallback(
    (next: (current: LinksDocument) => LinksDocument) => {
      set((current: LinksDocument | null) => next(coerce(current)));
    },
    [set],
  );

  const add = (event: React.FormEvent) => {
    event.preventDefault();
    const next = addLink(document, { title, url, group, note });
    if (next === document) {
      setProblem(
        "That is not a web address. It needs a host, like abera.tech.",
      );
      return;
    }
    setProblem(null);
    change(() => next);
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
      <Stack spacing={2} sx={{ maxWidth: 420 }}>
        <Typography variant="body1" sx={{ color: "text.secondary" }}>
          This list is the owner&apos;s. Sign in to see it.
        </Typography>
        <Box>
          <Button
            variant="contained"
            href="/api/scheduling/admin/sign-in?returnUrl=/links"
          >
            Sign in with Google
          </Button>
        </Box>
      </Stack>
    );
  }

  const shown = search(document, query);
  const groups = groupsOf(shown);
  const knownGroups = groupsOf(document).filter((g) => g !== "General");

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

      <Stack direction="row" spacing={1.5} sx={{ alignItems: "center" }}>
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
        <Chip
          size="small"
          label={saving ? "Saving…" : "Saved"}
          color={saving ? "default" : "success"}
          variant="outlined"
          sx={{ flexShrink: 0 }}
        />
      </Stack>

      {document.links.length === 0 && (
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Nothing here yet. Paste an address above and it will be here on every
          device you sign in from.
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
