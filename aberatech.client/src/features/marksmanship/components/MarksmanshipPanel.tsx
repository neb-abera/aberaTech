import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import CircularProgress from "@mui/material/CircularProgress";
import Link from "@mui/material/Link";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import * as React from "react";
import Blocks from "../../playbook/components/Blocks";
import SignInToSee from "../../progress/components/SignInToSee";
import {
  columnsOf,
  fetchPlan,
  type LogTable,
  newestFirst,
  notionUrl,
  type PlanResult,
  show,
} from "../core/api";

/**
 * The President's Hundred plan and its training log, read live from
 * Notion at each visit. Notion stays the record: nothing here is stored.
 */
export default function MarksmanshipPanel() {
  const [plan, setPlan] = React.useState<PlanResult | { status: "loading" }>({
    status: "loading",
  });

  React.useEffect(() => {
    const controller = new AbortController();
    void fetchPlan(controller.signal).then((next) => {
      if (!controller.signal.aborted) setPlan(next);
    });
    return () => controller.abort();
  }, []);

  if (plan.status === "loading") {
    return <CircularProgress size={28} aria-label="Loading" />;
  }
  if (plan.status === "visitor") {
    return (
      <SignInToSee
        message="This page is the owner's. Sign in to see it."
        returnUrl="/marksmanship"
      />
    );
  }
  if (plan.status === "unconfigured") {
    return <Alert severity="info">{plan.message}</Alert>;
  }
  if (plan.status === "error") {
    return (
      <Alert severity="warning">
        {plan.busy
          ? "Notion is busy. Reload in a minute."
          : "Notion did not answer. Reload to try again."}
      </Alert>
    );
  }

  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 4 }}>
      {plan.tables.map((table) => (
        <Log key={table.id} table={table} />
      ))}
      <Box component="article" aria-label="Plan">
        <Heading title={plan.page.title} id={plan.page.id} />
        <Blocks blocks={plan.page.blocks} />
      </Box>
    </Box>
  );
}

function Log({ table }: { table: LogTable }) {
  const rows = newestFirst(table.rows);
  const columns = columnsOf(rows);
  const heading = `log-${table.id}`;
  return (
    <Box component="section" aria-labelledby={heading}>
      <Heading title={table.title} id={table.id} headingId={heading} />
      {rows.length === 0 ? (
        <Typography variant="body2">No sessions logged yet.</Typography>
      ) : (
        <TableContainer sx={{ overflowX: "auto" }}>
          <Table size="small" aria-labelledby={heading}>
            <TableHead>
              <TableRow>
                {columns.map((column) => (
                  <TableCell key={column} sx={{ whiteSpace: "nowrap" }}>
                    {column}
                  </TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map((row) => (
                <TableRow key={row.id}>
                  {columns.map((column) => (
                    <TableCell
                      key={column}
                      align={
                        typeof row.values[column] === "number"
                          ? "right"
                          : "left"
                      }
                    >
                      {show(row.values[column])}
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}

/** A section's title, with a link to the same page or database in Notion. */
function Heading(props: { title: string; id: string; headingId?: string }) {
  return (
    <Box
      sx={{
        display: "flex",
        alignItems: "baseline",
        flexWrap: "wrap",
        columnGap: 2,
        mb: 1,
      }}
    >
      <Typography variant="h5" component="h2" id={props.headingId}>
        {props.title}
      </Typography>
      <Link
        href={notionUrl(props.id)}
        target="_blank"
        rel="noopener noreferrer"
        aria-label={`Open ${props.title} in Notion`}
      >
        Open in Notion
      </Link>
    </Box>
  );
}
