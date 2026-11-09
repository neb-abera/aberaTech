import Link from "@mui/material/Link";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { type Syllabus, syllabus } from "../core/syllabus";

/** Each system the syllabus sends you to, once: what it is for, and how it is recorded. */
export default function ToolsView({ from = syllabus }: { from?: Syllabus }) {
  return (
    <Stack component="ul" spacing={1.5} sx={{ p: 0, m: 0 }}>
      {from.systems.map((system) => (
        <Paper
          key={system.id}
          variant="outlined"
          component="li"
          sx={{ p: 2, listStyle: "none" }}
        >
          <Typography
            variant="subtitle1"
            component="h3"
            sx={{ fontWeight: 600 }}
          >
            {system.url.startsWith("http") ? (
              <Link href={system.url} target="_blank" rel="noopener">
                {system.name}
              </Link>
            ) : (
              system.name
            )}
          </Typography>
          <Typography variant="body2">{system.for}</Typography>
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {system.records}
          </Typography>
        </Paper>
      ))}
    </Stack>
  );
}
