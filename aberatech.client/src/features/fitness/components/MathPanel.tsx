import Accordion from "@mui/material/Accordion";
import AccordionDetails from "@mui/material/AccordionDetails";
import AccordionSummary from "@mui/material/AccordionSummary";
import Link from "@mui/material/Link";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import type { Step } from "../core/api";

/**
 * The arithmetic behind whatever is above it, with this athlete's numbers
 * substituted rather than symbols. Every result the engine produces ships one
 * of these: a model that will not show its working is a model you have to take
 * on faith, and this one is asking for training years.
 */
export default function MathPanel({
  steps,
  title = "Show the maths",
}: {
  steps: Step[];
  title?: string;
}) {
  if (steps.length === 0) {
    return null;
  }

  return (
    <Accordion disableGutters variant="outlined" sx={{ mt: 1 }}>
      <AccordionSummary aria-controls="maths-content">
        <Typography variant="body2">{title}</Typography>
      </AccordionSummary>
      <AccordionDetails sx={{ overflowX: "auto" }}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Step</TableCell>
              <TableCell>With your numbers</TableCell>
              <TableCell>Gives</TableCell>
              <TableCell>Source</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {steps.map((step) => (
              <TableRow key={`${step.label}|${step.expression}`}>
                <TableCell>{step.label}</TableCell>
                <TableCell sx={{ fontFamily: "monospace", fontSize: 12 }}>
                  {step.expression}
                </TableCell>
                <TableCell>
                  <strong>{step.value}</strong>
                </TableCell>
                <TableCell>
                  {step.citationId === null ? (
                    "—"
                  ) : (
                    <Link href={`#source-${step.citationId}`} underline="hover">
                      {step.citationId}
                    </Link>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </AccordionDetails>
    </Accordion>
  );
}
