import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Checkbox from "@mui/material/Checkbox";
import Divider from "@mui/material/Divider";
import FormControlLabel from "@mui/material/FormControlLabel";
import FormGroup from "@mui/material/FormGroup";
import LinearProgress from "@mui/material/LinearProgress";
import Link from "@mui/material/Link";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemText from "@mui/material/ListItemText";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import type { Attempt } from "../core/gates";
import {
  allTasks,
  type Block,
  cadence,
  copy,
  gear,
  plan,
  type Resource,
  rules,
} from "../core/plan";
import { useGateLog } from "../hooks/useGateLog";
import { useProgress } from "../hooks/useProgress";
import DrillPanel from "./DrillPanel";
import GateLog from "./GateLog";
import PlanTemplate from "./PlanTemplate";
import ReferenceCards from "./ReferenceCards";

/**
 * The curriculum, rendered from core/plan.ts.
 *
 * One column, read top to bottom: how it is scored, the rules, the weekly
 * cadence, then the seven blocks each with its checklist and gate, then the
 * gear. Ticks are kept in the visitor's browser by useProgress; the page
 * itself holds no state of its own.
 */
export default function TrainingPlan() {
  const { done, toggle, reset } = useProgress();
  const gates = useGateLog();
  const total = allTasks.length;
  const finished = allTasks.filter((task) => done.has(task.id)).length;

  return (
    <Stack spacing={5}>
      <Paper variant="outlined" sx={{ p: 2.5 }}>
        <Stack spacing={1.5}>
          <Stack
            direction="row"
            spacing={2}
            sx={{ alignItems: "center", justifyContent: "space-between" }}
          >
            <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
              {finished} of {total} done
            </Typography>
            <Button size="small" variant="text" onClick={reset}>
              Start over
            </Button>
          </Stack>
          <LinearProgress
            variant="determinate"
            value={total === 0 ? 0 : (finished / total) * 100}
            aria-label="Tasks done"
          />
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {copy.scoring}
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {copy.practice}
          </Typography>
        </Stack>
      </Paper>

      <DrillPanel />

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
          done={done}
          toggle={toggle}
          attempts={gates.log[block.id] ?? []}
          addAttempt={gates.add}
          removeAttempt={gates.remove}
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

function Section({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <Box component="section">
      <Typography
        variant="h2"
        component="h2"
        sx={{ color: "text.primary", fontSize: "1.5rem", mb: 1.5 }}
      >
        {title}
      </Typography>
      {children}
    </Box>
  );
}

function BlockSection({
  block,
  index,
  done,
  toggle,
  attempts,
  addAttempt,
  removeAttempt,
}: {
  block: Block;
  index: number;
  done: Set<string>;
  toggle: (id: string) => void;
  attempts: Attempt[];
  addAttempt: (blockId: string, attempt: Attempt) => void;
  removeAttempt: (blockId: string, id: string) => void;
}) {
  return (
    <Box component="section">
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={{ xs: 0.5, sm: 2 }}
        sx={{ alignItems: { sm: "baseline" }, mb: 1 }}
      >
        <Typography
          variant="h2"
          component="h2"
          sx={{ color: "text.primary", fontSize: "1.5rem" }}
        >
          {index}. {block.title}
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {block.weeks}
        </Typography>
      </Stack>
      <Typography variant="body1" sx={{ mb: 1.5 }}>
        {block.why}
      </Typography>

      <FormGroup>
        {block.tasks.map((task) => (
          <FormControlLabel
            key={task.id}
            control={
              <Checkbox
                checked={done.has(task.id)}
                onChange={() => toggle(task.id)}
              />
            }
            label={task.text}
            sx={{
              alignItems: "flex-start",
              mb: 0.5,
              "& .MuiCheckbox-root": { pt: 0.25 },
            }}
          />
        ))}
      </FormGroup>

      <Typography
        variant="overline"
        component="p"
        sx={{ color: "text.secondary", mt: 1.5, lineHeight: 1.5 }}
      >
        Practice
      </Typography>
      <ResourceList resources={block.practice} />

      <GateLog
        blockId={block.id}
        gate={block.gate}
        attempts={attempts}
        add={addAttempt}
        remove={removeAttempt}
      />

      <Typography
        variant="overline"
        component="p"
        sx={{ color: "text.secondary", mt: 1.5, lineHeight: 1.5 }}
      >
        Reading
      </Typography>
      <ResourceList resources={block.resources} />
      <Divider sx={{ mt: 3 }} />
    </Box>
  );
}

function ResourceList({ resources }: { resources: Resource[] }) {
  return (
    <List dense disablePadding>
      {resources.map((resource) => (
        <ListItem key={resource.url} disableGutters>
          <ListItemText
            primary={
              <Link
                href={resource.url}
                target="_blank"
                rel="noopener noreferrer"
              >
                {resource.title}
              </Link>
            }
            secondary={resource.note}
          />
        </ListItem>
      ))}
    </List>
  );
}
