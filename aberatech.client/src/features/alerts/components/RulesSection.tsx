import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import {
  type AlertsState,
  type AlertType,
  isUnreachable,
  type StateChangeResult,
  type TypeRule,
} from "../core/api";
import {
  maxRules,
  maxRuleTextLength,
  ruleFields,
  ruleProblems,
  saveTypeRules,
} from "../core/rules";
import { typeLabels } from "../core/settings";

/** What the section calls. The page passes the real one, a test its own. */
export interface RulesApi {
  save: (rules: TypeRule[]) => Promise<StateChangeResult>;
}

export const rulesApi: RulesApi = { save: saveTypeRules };

const placeholders: Record<TypeRule["field"], string> = {
  title: "standup",
  organizer: "army.mil (blank: any organizer)",
  location: "blank: any location",
};

const types: AlertType[] = ["alarm", "notification", "none"];

type Status =
  | { kind: "idle" }
  | { kind: "sending" }
  | { kind: "saved" }
  | { kind: "error"; message: string };

function same(a: TypeRule[], b: TypeRule[]): boolean {
  return (
    a.length === b.length &&
    a.every(
      (rule, i) =>
        rule.field === b[i].field &&
        rule.text.trim() === b[i].text &&
        rule.type === b[i].type,
    )
  );
}

/**
 * The rules for what an event sends with no choice of its own and no
 * #critical mark. Edited here as a list and saved whole. The phone edits
 * the same list.
 */
export default function RulesSection({
  rules,
  api = rulesApi,
  onState,
}: {
  rules: TypeRule[];
  api?: RulesApi;
  onState: (state: AlertsState) => void;
}) {
  const [draft, setDraft] = React.useState<TypeRule[]>(rules);
  const [status, setStatus] = React.useState<Status>({ kind: "idle" });
  const savedRules = React.useRef(rules);

  // A save elsewhere (the phone, another tab) arrives with the next status.
  // An edit in progress here is kept.
  React.useEffect(() => {
    setDraft((current) =>
      same(current, savedRules.current) ? rules : current,
    );
    savedRules.current = rules;
  }, [rules]);

  const problems = ruleProblems(draft);
  const changed = !same(draft, rules);

  const change = (index: number, next: Partial<TypeRule>) => {
    setStatus({ kind: "idle" });
    setDraft((current) =>
      current.map((rule, i) => (i === index ? { ...rule, ...next } : rule)),
    );
  };
  const move = (index: number, by: -1 | 1) => {
    setStatus({ kind: "idle" });
    setDraft((current) => {
      const next = [...current];
      [next[index], next[index + by]] = [next[index + by], next[index]];
      return next;
    });
  };
  const remove = (index: number) => {
    setStatus({ kind: "idle" });
    setDraft((current) => current.filter((_, i) => i !== index));
  };
  const add = () => {
    setStatus({ kind: "idle" });
    setDraft((current) => [
      ...current,
      { field: "organizer", text: "", type: "alarm" },
    ]);
  };

  const save = async () => {
    setStatus({ kind: "sending" });
    const result = await api.save(draft);
    if (result.ok) {
      const stored =
        result.state?.rules ??
        draft.map((rule) => ({ ...rule, text: rule.text.trim() }));
      savedRules.current = stored;
      setDraft(stored);
      setStatus({ kind: "saved" });
      if (result.state) onState(result.state);
      return;
    }
    setStatus({
      kind: "error",
      message: isUnreachable(result)
        ? "No connection. Your rules are kept on this page. Press Save rules again."
        : result.reason === "invalid"
          ? Object.values(result.errors ?? {})
              .flat()
              .join(" ")
          : result.reason === "visitor"
            ? "Sign in again to save rules."
            : "abera.tech did not save the rules. Try again.",
    });
  };

  return (
    <Box component="section" aria-labelledby="rules-heading">
      <Typography
        id="rules-heading"
        variant="h2"
        sx={{ fontSize: "1.25rem", mb: 1 }}
      >
        Rules
      </Typography>
      <Typography variant="body2" sx={{ color: "text.secondary", mb: 1.5 }}>
        A rule sets what an event sends when it has no type of its own and no
        #critical mark. The first rule that matches wins. Matching ignores case.
        Organizer and location rules with no text match any event that has one.
      </Typography>
      <Stack spacing={1.5}>
        {draft.map((rule, index) => (
          <Stack
            // A rule has no id: its place in the list is what the server keeps.
            // biome-ignore lint/suspicious/noArrayIndexKey: the order is the identity
            key={index}
            direction={{ xs: "column", sm: "row" }}
            spacing={1}
            role="group"
            aria-label={`Rule ${index + 1}`}
            sx={{ alignItems: { sm: "center" } }}
          >
            <TextField
              select
              size="small"
              label="When"
              slotProps={{ select: { native: true } }}
              value={rule.field}
              onChange={(event) =>
                change(index, {
                  field: event.target.value as TypeRule["field"],
                })
              }
              sx={{ minWidth: 190 }}
            >
              {ruleFields.map((field) => (
                <option key={field.value} value={field.value}>
                  {field.label}
                </option>
              ))}
            </TextField>
            <TextField
              size="small"
              label="Text"
              value={rule.text}
              placeholder={placeholders[rule.field]}
              onChange={(event) => change(index, { text: event.target.value })}
              slotProps={{ htmlInput: { maxLength: maxRuleTextLength } }}
              sx={{ flex: 1, minWidth: 160 }}
            />
            <TextField
              select
              size="small"
              label="Sends"
              slotProps={{ select: { native: true } }}
              value={rule.type}
              onChange={(event) =>
                change(index, { type: event.target.value as AlertType })
              }
              sx={{ minWidth: 190 }}
            >
              {types.map((type) => (
                <option key={type} value={type}>
                  {typeLabels[type]}
                </option>
              ))}
            </TextField>
            <Stack direction="row" spacing={0.5}>
              <Button
                size="small"
                disabled={index === 0}
                onClick={() => move(index, -1)}
                aria-label={`Move rule ${index + 1} up`}
              >
                Up
              </Button>
              <Button
                size="small"
                disabled={index === draft.length - 1}
                onClick={() => move(index, 1)}
                aria-label={`Move rule ${index + 1} down`}
              >
                Down
              </Button>
              <Button
                size="small"
                color="error"
                onClick={() => remove(index)}
                aria-label={`Remove rule ${index + 1}`}
              >
                Remove
              </Button>
            </Stack>
          </Stack>
        ))}
        {draft.length === 0 && (
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            No rules. Events follow their own type, #critical, and the default.
          </Typography>
        )}
      </Stack>
      <Stack direction="row" spacing={1} sx={{ mt: 1.5, alignItems: "center" }}>
        <Button
          variant="outlined"
          onClick={add}
          disabled={draft.length >= maxRules}
        >
          Add rule
        </Button>
        <Button
          variant="contained"
          onClick={save}
          disabled={
            !changed || problems.length > 0 || status.kind === "sending"
          }
        >
          Save rules
        </Button>
        <Typography
          variant="body2"
          role="status"
          sx={{ color: "text.secondary" }}
        >
          {status.kind === "sending"
            ? "Saving"
            : status.kind === "saved"
              ? "Saved"
              : changed
                ? "Not saved yet"
                : ""}
        </Typography>
      </Stack>
      {problems.map((problem) => (
        <Alert key={problem} severity="warning" sx={{ mt: 1 }}>
          {problem}
        </Alert>
      ))}
      {status.kind === "error" && (
        <Alert severity="error" sx={{ mt: 1 }}>
          {status.message}
        </Alert>
      )}
    </Box>
  );
}
