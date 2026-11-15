import {
  type AlertItem,
  type AlertType,
  changeState,
  type StateChangeResult,
  type TypeRule,
} from "./api";

/** The bounds the server checks: AlertTypeRuleBounds in AlertTypeRules.cs. */
export const maxRules = 50;
export const maxRuleTextLength = 100;

export const ruleFields: { value: TypeRule["field"]; label: string }[] = [
  { value: "title", label: "Title contains" },
  { value: "organizer", label: "Organizer contains" },
  { value: "location", label: "Location contains" },
];

/** As AlertTypeRule.Matches on the server: the field contains the text, any case. */
export function ruleMatches(rule: TypeRule, alert: AlertItem): boolean {
  const value =
    rule.field === "title"
      ? alert.title
      : rule.field === "organizer"
        ? (alert.organizer ?? null)
        : alert.location;
  if (value === null || value.trim() === "") return false;
  return (
    rule.text === "" || value.toLowerCase().includes(rule.text.toLowerCase())
  );
}

/**
 * What an event sends with no choice of its own: #critical, then the first
 * rule that matches, then the default. AlertTypes.Resolve on the server.
 */
export function unchosenType(
  alert: AlertItem,
  rules: TypeRule[],
  defaultType: AlertType,
): { type: AlertType; typeFrom: "critical" | "rule" | "default" } {
  if (alert.critical) return { type: "alarm", typeFrom: "critical" };
  const rule = rules.find((candidate) => ruleMatches(candidate, alert));
  return rule
    ? { type: rule.type, typeFrom: "rule" }
    : { type: defaultType, typeFrom: "default" };
}

/** Problems the server would refuse, by rule, so the page says them before it sends. */
export function ruleProblems(rules: TypeRule[]): string[] {
  const problems: string[] = [];
  if (rules.length > maxRules) problems.push(`At most ${maxRules} rules.`);
  rules.forEach((rule, index) => {
    const text = rule.text.trim();
    if (rule.field === "title" && text === "")
      problems.push(
        `Rule ${index + 1}: a title rule needs a word to look for.`,
      );
    if (text.length > maxRuleTextLength)
      problems.push(
        `Rule ${index + 1}: at most ${maxRuleTextLength} characters.`,
      );
  });
  return problems;
}

/** Replaces every rule with these, in this order, and answers with the stored state. */
export function saveTypeRules(rules: TypeRule[]): Promise<StateChangeResult> {
  return changeState("PUT", "/api/alerts/rules", {
    rules: rules.map((rule) => ({ ...rule, text: rule.text.trim() })),
  });
}
