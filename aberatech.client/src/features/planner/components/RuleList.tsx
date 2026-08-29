/** The four degree rules, with what you have against what the regulations require. */

import CheckCircleOutline from "@mui/icons-material/CheckCircleOutlineOutlined";
import RadioButtonUnchecked from "@mui/icons-material/RadioButtonUnchecked";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import type { RuleRow } from "../core/rules";

export default function RuleList({ rules }: { rules: RuleRow[] }) {
  return (
    <Box>
      {rules.map((r) => (
        <Box
          key={r.id}
          sx={{
            display: "grid",
            gridTemplateColumns: "1fr 56px 20px",
            gap: 1,
            alignItems: "center",
            py: 0.4,
          }}
        >
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {r.label}
          </Typography>
          <Typography
            variant="body2"
            sx={{
              color: "text.secondary",
              textAlign: "right",
              fontVariantNumeric: "tabular-nums",
            }}
          >
            {r.have} / {r.need}
          </Typography>
          {r.met ? (
            <CheckCircleOutline color="success" sx={{ fontSize: 17 }} />
          ) : (
            <RadioButtonUnchecked
              sx={{ fontSize: 17, color: "text.disabled" }}
            />
          )}
        </Box>
      ))}
    </Box>
  );
}
