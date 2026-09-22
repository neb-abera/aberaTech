import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { Conflict, LinkEntry } from "../core/links";
import { GENERAL, titleOf } from "../core/links";

export interface ConflictsProps {
  conflicts: Conflict[];
  links: LinkEntry[];
  onKeepMine: (conflict: Conflict) => void;
  onTakeTheirs: (conflict: Conflict) => void;
  onEdit: (conflict: Conflict, link: LinkEntry) => void;
}

/**
 * What an upload said differently about links already here, one card
 * each, with both versions side by side. Nothing has changed on the link
 * yet. Keep mine drops the file's version, Take the file's applies it,
 * Edit opens the link's form with the file's values filled in so the owner
 * can settle it by hand.
 */
export default function Conflicts({
  conflicts,
  links,
  onKeepMine,
  onTakeTheirs,
  onEdit,
}: ConflictsProps) {
  if (conflicts.length === 0) return null;
  const byId = new Map(links.map((l) => [l.id, l]));

  return (
    <Stack spacing={2} aria-label="To resolve">
      <Alert severity="warning">
        {conflicts.length === 1
          ? "One upload said something different about a link that was already here. "
          : `${conflicts.length} uploads said something different about links that were already here. `}
        Nothing changed. Choose for each.
      </Alert>
      {conflicts.map((conflict) => {
        const link = byId.get(conflict.linkId);
        if (link === undefined) return null;
        const rows: Array<[string, string, string]> = [];
        if (conflict.theirs.title !== undefined)
          rows.push(["Title", titleOf(link), conflict.theirs.title]);
        if (conflict.theirs.group !== undefined)
          rows.push(["Group", link.group || GENERAL, conflict.theirs.group]);
        if (conflict.theirs.note !== undefined)
          rows.push(["Note", link.note || "(none)", conflict.theirs.note]);
        return (
          <Box
            key={conflict.id}
            component="section"
            aria-label={`Conflict on ${titleOf(link)}`}
            sx={{
              border: 1,
              borderColor: "divider",
              borderRadius: 1,
              p: 2,
              display: "grid",
              gap: 1,
            }}
          >
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              {link.url}
              {conflict.source ? ` · from ${conflict.source}` : ""}
              {conflict.seenAt ? ` on ${conflict.seenAt}` : ""}
            </Typography>
            <Box
              component="table"
              sx={{
                borderCollapse: "collapse",
                "& td, & th": {
                  textAlign: "left",
                  verticalAlign: "top",
                  py: 0.5,
                  pr: 2,
                },
                "& th": { color: "text.secondary", fontWeight: 500 },
              }}
            >
              <thead>
                <tr>
                  <th />
                  <th>Mine</th>
                  <th>The file&apos;s</th>
                </tr>
              </thead>
              <tbody>
                {rows.map(([field, mine, theirs]) => (
                  <tr key={field}>
                    <th>{field}</th>
                    <td>{mine}</td>
                    <td>{theirs}</td>
                  </tr>
                ))}
              </tbody>
            </Box>
            <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap" }}>
              <Button
                variant="outlined"
                size="small"
                onClick={() => onKeepMine(conflict)}
              >
                Keep mine
              </Button>
              <Button
                variant="contained"
                size="small"
                onClick={() => onTakeTheirs(conflict)}
              >
                Take the file&apos;s
              </Button>
              <Button
                variant="text"
                size="small"
                onClick={() => onEdit(conflict, link)}
              >
                Edit
              </Button>
            </Stack>
          </Box>
        );
      })}
    </Stack>
  );
}
