/** The counts that answer "where am I" at a glance, beside the controls that change them. */
import Box from "@mui/material/Box";
import Paper from "@mui/material/Paper";
import Typography from "@mui/material/Typography";
import { green, orange, red } from "../../../theme/themePrimitives";
import type { DegreeAudit } from "../core/rules";
import type { PlannerModel } from "../model/PlannerModel";

export interface StatusPillsProps {
  model: PlannerModel;
  audit: DegreeAudit;
}

type Tone = "plain" | "good" | "bad" | "warn";

export default function StatusPills({ model, audit }: StatusPillsProps) {
  const violations = model.plan.violations();
  const withErrors = new Set(violations.map((v) => v.code)).size;
  const overCapacity = model.plan.terms.filter(
    (t) => t.length > model.perTerm,
  ).length;
  const auto = model.autoAdded();
  const usedTerms = model.plan.terms.filter((t) => t.length).length;

  const pills: [string, string | number, Tone][] = [
    ["Courses planned", model.plan.courses().length, "plain"],
    ["Terms", usedTerms, "plain"],
    ["Years", (usedTerms / model.termsPerYear).toFixed(1), "plain"],
    [
      "Applied to degree",
      `${audit.counted.length} / 10`,
      audit.counted.length === 10 ? "good" : "plain",
    ],
    ["Courses with errors", withErrors, violations.length ? "bad" : "good"],
    ["Preparation", model.preparation().length, "plain"],
    ["In the pool", model.unplaced().length, "plain"],
  ];
  if (auto.size) pills.push(["Pulled in", auto.size, "plain"]);
  if (overCapacity) pills.push(["Over capacity", overCapacity, "warn"]);

  // The palette's main shades are for fills. As 18px text on the pill they
  // measured 2.24:1 (light) and 3.5:1 (dark) for success, under WCAG AA's
  // 4.5:1, so each scheme gets the shade that clears it: 5.8 to 6.1:1 on the
  // light paper, 5.3 to 7.9:1 on the dark one.
  const shades: Record<Tone, { light: string; dark: string }> = {
    plain: { light: "text.primary", dark: "text.primary" },
    good: { light: green[600], dark: green[400] },
    bad: { light: red[400], dark: red[300] },
    warn: { light: orange[600], dark: orange[500] },
  };

  return (
    <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}>
      {pills.map(([label, value, tone]) => (
        <Paper
          key={label}
          variant="outlined"
          sx={{ px: 1.5, py: 0.75, minWidth: 104 }}
        >
          <Typography
            variant="caption"
            sx={{ color: "text.secondary", display: "block" }}
          >
            {label}
          </Typography>
          <Typography
            variant="h6"
            sx={[
              { color: shades[tone].light, fontWeight: 600, lineHeight: 1.2 },
              (theme) =>
                theme.applyStyles("dark", { color: shades[tone].dark }),
            ]}
          >
            {value}
          </Typography>
        </Paper>
      ))}
    </Box>
  );
}
