import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import SignInToSee from "../../progress/components/SignInToSee";
import { useOwnerDocument } from "../../progress/hooks/useOwnerDocument";
import Markdown from "./Markdown";

const documentKey = "plan";

export interface PlanDocument {
  version: 1;
  markdown: string;
}

function coerce(value: unknown): PlanDocument {
  const markdown =
    typeof value === "object" &&
    value !== null &&
    typeof (value as { markdown?: unknown }).markdown === "string"
      ? (value as { markdown: string }).markdown
      : "";
  return { version: 1, markdown };
}

/**
 * The owner's plan: one Markdown document, kept on the server behind the
 * same sign-in as the bookmark list, shown rendered and edited in place.
 *
 * The text never enters the site's code. A visitor gets a sign-in button;
 * the owner reads the rendered page, presses Edit to change it, and can
 * bring a .md file in or take one out. Saves go through the same hook as
 * every other owner document, a beat after each change.
 */
export default function PlanPanel() {
  const { status, value, set, saving, failed } =
    useOwnerDocument<PlanDocument>(documentKey);
  const document = React.useMemo(() => coerce(value), [value]);
  const [editing, setEditing] = React.useState(false);
  const [problem, setProblem] = React.useState<string | null>(null);
  const fileInput = React.useRef<HTMLInputElement | null>(null);

  const change = React.useCallback(
    (markdown: string) => {
      set(() => ({ version: 1, markdown }));
    },
    [set],
  );

  const upload = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    const text = await file.text();
    if (text.trim() === "") {
      setProblem(`${file.name} is empty.`);
      return;
    }
    setProblem(null);
    change(text);
    setEditing(false);
  };

  const download = () => {
    const blob = new Blob([document.markdown], {
      type: "text/markdown;charset=utf-8",
    });
    const href = URL.createObjectURL(blob);
    const anchor = window.document.createElement("a");
    anchor.href = href;
    anchor.download = `plan-${new Date().toISOString().slice(0, 10)}.md`;
    anchor.click();
    URL.revokeObjectURL(href);
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
        message="This page is the owner's. Sign in to see it."
        returnUrl="/plan"
      />
    );
  }

  const saveState = failed
    ? { label: "Not saved", color: "error" as const }
    : saving
      ? { label: "Saving…", color: "default" as const }
      : { label: "Saved", color: "success" as const };

  return (
    <Stack spacing={3}>
      <Stack
        direction="row"
        spacing={1}
        sx={{ alignItems: "center", flexWrap: "wrap" }}
      >
        <Button
          variant={editing ? "contained" : "outlined"}
          size="small"
          onClick={() => setEditing((on) => !on)}
        >
          {editing ? "Done" : "Edit"}
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
          accept=".md,.markdown,.txt,text/markdown,text/plain"
          onChange={upload}
          hidden
          aria-label="Markdown file to upload"
        />
        <Button
          variant="outlined"
          size="small"
          onClick={download}
          disabled={document.markdown.trim() === ""}
        >
          Download
        </Button>
        <Chip
          size="small"
          label={saveState.label}
          color={saveState.color}
          variant="outlined"
        />
      </Stack>

      {problem && (
        <Alert severity="warning" onClose={() => setProblem(null)}>
          {problem}
        </Alert>
      )}

      {editing ? (
        <TextField
          label="Plan, in Markdown"
          value={document.markdown}
          onChange={(e) => change(e.target.value)}
          multiline
          minRows={20}
          fullWidth
          slotProps={{
            htmlInput: {
              spellCheck: false,
              style: { fontFamily: "monospace", fontSize: "0.9rem" },
            },
          }}
        />
      ) : document.markdown.trim() === "" ? (
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Nothing here yet. Press Edit and write, or upload a Markdown file.
        </Typography>
      ) : (
        <Box>
          <Markdown source={document.markdown} />
        </Box>
      )}
    </Stack>
  );
}
