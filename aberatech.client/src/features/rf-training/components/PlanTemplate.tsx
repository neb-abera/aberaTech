import Box from "@mui/material/Box";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemText from "@mui/material/ListItemText";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { planTemplate, templateStandard } from "../core/reference";

/**
 * The communications plan template: the headings and the questions each
 * must answer, with space under each to write. It prints with the cards.
 */
export default function PlanTemplate() {
  return (
    <Stack spacing={2}>
      <Paper variant="outlined" sx={{ p: 1.5, borderColor: "primary.main" }}>
        <Typography
          variant="overline"
          component="p"
          sx={{ color: "primary.main", lineHeight: 1.5 }}
        >
          Standard
        </Typography>
        <Typography variant="body2">{templateStandard}</Typography>
      </Paper>
      {planTemplate.map((section, index) => (
        <Box component="section" key={section.title} aria-label={section.title}>
          <Typography
            variant="subtitle1"
            component="h3"
            sx={{ fontWeight: 600 }}
          >
            {index + 1}. {section.title}
          </Typography>
          <List dense disablePadding>
            {section.prompts.map((prompt) => (
              <ListItem key={prompt} disableGutters>
                <ListItemText primary={prompt} />
              </ListItem>
            ))}
          </List>
          <Box
            aria-hidden="true"
            sx={{
              height: 56,
              borderBottom: "1px dashed",
              borderColor: "divider",
              displayPrint: "block",
              display: "none",
            }}
          />
        </Box>
      ))}
    </Stack>
  );
}
