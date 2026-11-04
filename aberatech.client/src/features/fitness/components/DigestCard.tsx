import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import * as React from "react";
import { type Digest, fetchDigest } from "../core/api";

/**
 * The week in one page, as the morning brief will see it: the same text the
 * digest endpoint serves, so the console and the brief never disagree.
 */
export default function DigestCard() {
  const [digest, setDigest] = React.useState<Digest | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [copied, setCopied] = React.useState(false);
  // Closed by default: the cards below show the same numbers (Neb, 2026-11-04).
  const [open, setOpen] = React.useState(false);

  React.useEffect(() => {
    let cancelled = false;
    fetchDigest()
      .then((d) => {
        if (!cancelled) setDigest(d);
      })
      .catch((e: Error) => {
        if (!cancelled) setError(e.message);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const copy = async () => {
    if (digest === null) return;
    try {
      await navigator.clipboard.writeText(digest.text);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  };

  if (error !== null) {
    return <Alert severity="warning">The digest did not answer: {error}</Alert>;
  }

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack
          direction="row"
          spacing={1}
          sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 1, mb: 0.5 }}
        >
          <Typography variant="h6">This week, in one page</Typography>
          <Button
            size="small"
            onClick={() => setOpen((v) => !v)}
            aria-expanded={open}
            aria-controls="weekly-digest-text"
          >
            {open ? "Hide" : "Show"}
          </Button>
          <Button
            size="small"
            onClick={() => void copy()}
            disabled={digest === null}
          >
            {copied ? "Copied" : "Copy"}
          </Button>
        </Stack>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          The text your morning brief gets. The cards below show the same
          numbers.
        </Typography>
        {!open ? null : digest === null ? (
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Writing it up…
          </Typography>
        ) : (
          <Typography
            id="weekly-digest-text"
            component="pre"
            variant="body2"
            sx={{
              whiteSpace: "pre-wrap",
              fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
              fontSize: 12,
              m: 0,
              mt: 1,
            }}
          >
            {digest.text}
          </Typography>
        )}
      </CardContent>
    </Card>
  );
}
