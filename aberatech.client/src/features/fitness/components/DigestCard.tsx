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
            onClick={() => void copy()}
            disabled={digest === null}
          >
            {copied ? "Copied" : "Copy"}
          </Button>
        </Stack>
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
          The same text the digest endpoint serves to the morning brief.
        </Typography>
        {digest === null ? (
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Writing it up…
          </Typography>
        ) : (
          <Typography
            component="pre"
            variant="body2"
            sx={{
              whiteSpace: "pre-wrap",
              fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
              fontSize: 12,
              m: 0,
            }}
          >
            {digest.text}
          </Typography>
        )}
      </CardContent>
    </Card>
  );
}
