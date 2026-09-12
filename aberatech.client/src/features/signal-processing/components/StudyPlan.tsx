import Box from "@mui/material/Box";
import Chip from "@mui/material/Chip";
import Link from "@mui/material/Link";
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
import { useCurriculumProgress } from "../../progress/hooks/useCurriculumProgress";
import {
  allTasks,
  bookshelf,
  cadence,
  copy,
  documentKey,
  ideas,
  levels,
  rules,
  stages,
} from "../core/plan";

/**
 * The study plan, rendered from core/plan.ts with the site's shared plan
 * pieces.
 *
 * One column, read top to bottom: how far along and how it is scored, the
 * ideas the whole subject rests on, the rules, the weekly cadence, then the
 * three stages each with its blocks, and last the bookshelf. Ticks and gate
 * attempts are the owner's and are saved to the server; a visitor and the
 * build-time render get the same page read-only.
 */
export default function StudyPlan() {
  const progress = useCurriculumProgress(documentKey);
  const owner = progress.status === "owner";
  const total = allTasks.length;
  const finished = allTasks.filter((task) => progress.done.has(task.id)).length;
  let blockIndex = 0;

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

      <Section title="The ideas">
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1.5 }}>
          {copy.ideas}
        </Typography>
        <TableContainer>
          <Table size="small" aria-label="The ideas">
            <TableHead>
              <TableRow>
                <TableCell>Idea</TableCell>
                <TableCell>You own it when</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {ideas.map((idea) => (
                <TableRow key={idea.name}>
                  <TableCell
                    component="th"
                    scope="row"
                    sx={{ verticalAlign: "top" }}
                  >
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {idea.name}
                    </Typography>
                    <Typography variant="body2">{idea.idea}</Typography>
                  </TableCell>
                  <TableCell sx={{ verticalAlign: "top" }}>
                    {idea.test}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </Section>

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

      {stages.map((stage) => (
        <Box component="section" key={stage.id} aria-label={stage.title}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={{ xs: 0.5, sm: 2 }}
            sx={{ alignItems: { sm: "baseline" }, mb: 1 }}
          >
            <Typography
              variant="h2"
              component="h2"
              sx={{ color: "text.primary", fontSize: "1.75rem" }}
            >
              {stage.title}
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              {stage.weeks}
            </Typography>
          </Stack>
          <Typography variant="body1" sx={{ mb: 1 }}>
            {stage.why}
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mb: 3 }}>
            {stage.exit}
          </Typography>
          <Stack spacing={4}>
            {stage.blocks.map((block) => {
              blockIndex += 1;
              return (
                <BlockSection
                  key={block.id}
                  block={block}
                  index={blockIndex}
                  level="h3"
                  owner={owner}
                  done={progress.done}
                  toggle={progress.toggle}
                  attempts={progress.gates[block.id] ?? []}
                  addAttempt={progress.addAttempt}
                  removeAttempt={progress.removeAttempt}
                />
              );
            })}
          </Stack>
        </Box>
      ))}

      <Section title="The bookshelf">
        <Typography variant="body2" sx={{ color: "text.secondary", mb: 1.5 }}>
          {copy.bookshelf}
        </Typography>
        <TableContainer>
          <Table size="small" aria-label="The bookshelf">
            <TableHead>
              <TableRow>
                <TableCell>Book</TableCell>
                <TableCell>Stage</TableCell>
                <TableCell>Why this one</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {bookshelf.map((book) => (
                <TableRow key={book.title}>
                  <TableCell
                    component="th"
                    scope="row"
                    sx={{ verticalAlign: "top" }}
                  >
                    <Link
                      href={book.url}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      {book.title}
                    </Link>
                    <Typography
                      variant="body2"
                      sx={{ color: "text.secondary" }}
                    >
                      {book.authors}
                      {book.edition ? `, ${book.edition}` : ""}
                    </Typography>
                    {book.free && (
                      <Chip
                        label="Free"
                        size="small"
                        variant="outlined"
                        sx={{ mt: 0.5 }}
                      />
                    )}
                  </TableCell>
                  <TableCell
                    sx={{ verticalAlign: "top", whiteSpace: "nowrap" }}
                  >
                    {levels[book.level]}
                  </TableCell>
                  <TableCell sx={{ verticalAlign: "top" }}>
                    {book.why}
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
