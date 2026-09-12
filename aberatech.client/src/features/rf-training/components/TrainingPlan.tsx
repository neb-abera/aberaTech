import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemText from "@mui/material/ListItemText";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import {
  BlockSection,
  Note,
  ProgressSummary,
  Section,
} from "../../progress/components/Curriculum";
import { allTasks, cadence, copy, gear, plan, rules } from "../core/plan";
import { useTrainingProgress } from "../hooks/useTrainingProgress";
import DrillPanel from "./DrillPanel";
import PlanTemplate from "./PlanTemplate";
import ReferenceCards from "./ReferenceCards";

/**
 * The curriculum, rendered from core/plan.ts with the site's shared plan
 * pieces.
 *
 * One column, read top to bottom: how it is scored, the drill, the rules,
 * the weekly cadence, then the seven blocks each with its checklist and
 * gate, then the plan template, the cards and the gear. What the owner and
 * a visitor each see is the shared components' business; the drill is this
 * plan's own, runnable by anyone and remembered only for the owner.
 */
export default function TrainingPlan() {
  const progress = useTrainingProgress();
  const owner = progress.status === "owner";
  const total = allTasks.length;
  const finished = allTasks.filter((task) => progress.done.has(task.id)).length;

  return (
    <Stack spacing={5}>
      <ProgressSummary
        owner={owner}
        finished={finished}
        total={total}
        saving={progress.saving}
        reset={progress.reset}
      >
        <Note>{copy.scoring}</Note>
        <Note>{copy.practice}</Note>
      </ProgressSummary>

      <DrillPanel
        history={owner ? progress.drills : undefined}
        onResult={owner ? progress.recordDrill : undefined}
      />

      <Section title="Rules">
        <List dense disablePadding>
          {rules.map((rule) => (
            <ListItem key={rule} disableGutters>
              <ListItemText primary={rule} />
            </ListItem>
          ))}
        </List>
      </Section>

      <Section title="Every week">
        <TableContainer>
          <Table size="small" aria-label="Weekly cadence">
            <TableBody>
              {cadence.map((item) => (
                <TableRow key={item.label}>
                  <TableCell
                    component="th"
                    scope="row"
                    sx={{ fontWeight: 600, whiteSpace: "nowrap" }}
                  >
                    {item.label}
                  </TableCell>
                  <TableCell>{item.detail}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </Section>

      {plan.map((block, index) => (
        <BlockSection
          key={block.id}
          block={block}
          index={index + 1}
          owner={owner}
          done={progress.done}
          toggle={progress.toggle}
          attempts={progress.gates[block.id] ?? []}
          addAttempt={progress.addAttempt}
          removeAttempt={progress.removeAttempt}
        />
      ))}

      <Section title="Communications plan template">
        <PlanTemplate />
      </Section>

      <Section title="Reference cards">
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1.5 }}>
          {copy.cards}
        </Typography>
        <ReferenceCards />
      </Section>

      <Section title="Gear">
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1.5 }}>
          Bought block by block, not up front. Costs are rough and in US
          dollars; used and borrowed is fine for all of it.
        </Typography>
        <TableContainer>
          <Table size="small" aria-label="Gear list">
            <TableHead>
              <TableRow>
                <TableCell>Item</TableCell>
                <TableCell>For</TableCell>
                <TableCell align="right">Cost</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {gear.map((line) => (
                <TableRow key={line.item}>
                  <TableCell component="th" scope="row">
                    {line.item}
                  </TableCell>
                  <TableCell>{line.purpose}</TableCell>
                  <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>
                    {line.cost}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </Section>
    </Stack>
  );
}
