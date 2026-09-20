import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import Accordion from "@mui/material/Accordion";
import AccordionDetails from "@mui/material/AccordionDetails";
import AccordionSummary from "@mui/material/AccordionSummary";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import React, { Suspense, useState } from "react";
import { Link } from "react-router";
import PageShell from "../components/PageShell";
import { guides } from "../site/sections";

// Lazy-loaded components
const ZeroToThirtyDaysPostETS = React.lazy(
  () => import("../components/ZeroToThirtyDays"),
);
const TerminalLeave = React.lazy(() => import("../components/TerminalLeave"));
const ZeroToTenDays = React.lazy(() => import("../components/ZeroToTenDays"));
const ThirtyDays = React.lazy(() => import("../components/ThirtyDays"));
const ThirtyToNinetyDays = React.lazy(
  () => import("../components/ThirtyToNinetyDays"),
);
const NinetyToOneEightyDays = React.lazy(
  () => import("../components/NinetyToOneEightyDays"),
);
const SixToNineMonths = React.lazy(
  () => import("../components/SixToNineMonths"),
);
const NineToTwelveMonths = React.lazy(
  () => import("../components/NineToTwelveMonths"),
);
const TwelveToEighteenMonths = React.lazy(
  () => import("../components/TwelveToEighteenMonths"),
);
const EighteenToTwentyFourMonths = React.lazy(
  () => import("../components/EighteenToTwentyFourMonths.tsx"),
);
const LongAfterETS = React.lazy(() => import("../components/LongAfterETS"));

// Fallback loading spinner or placeholder
const LoadingFallback = () => <div>Loading...</div>;

export default function MilitaryTransitionGuide(props: {
  disableCustomTheme?: boolean;
}) {
  // Maintain open state for multiple accordions (optional)
  const [expanded, setExpanded] = useState<string | false>(false);

  const handleAccordionChange =
    (panel: string) => (_: React.SyntheticEvent, isExpanded: boolean) => {
      setExpanded(isExpanded ? panel : false);
    };
  return (
    <PageShell {...props} title={guides[0].title} intro={guides[0].blurb}>
      <Box
        sx={(theme) => ({
          p: 2,
          border: `1px solid ${(theme.vars || theme).palette.divider}`,
          backgroundColor: (theme.vars || theme).palette.background.paper,
          color: (theme.vars || theme).palette.text.primary,
          boxShadow: theme.shadows[1],
        })}
      >
        <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
          Lessons from my own transition, with the documents, links and videos I
          used. A few sections carry a personal story where it shows why a step
          matters. Most people in the system do their best inside a bureaucracy
          that does not serve you by default. You will have to push it.
          Questions and corrections are welcome.
        </Typography>
        <Typography variant="body1" component="p" sx={{ marginBottom: 2 }}>
          For a move into a technical job, see{" "}
          <Link
            to="https://abera.tech/technical"
            target="_blank"
            rel="noopener"
          >
            Everything I learned transitioning from the Army to Software
            Development
          </Link>
        </Typography>
      </Box>

      {/* Suspense-wrapped lazy-loaded components */}
      <Accordion
        expanded={expanded === "panel1"}
        onChange={handleAccordionChange("panel1")}
        sx={{ width: "100%" }}
      >
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <Typography>18 to 24 Months before ETS</Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Suspense fallback={<LoadingFallback />}>
            <EighteenToTwentyFourMonths />
          </Suspense>
        </AccordionDetails>
      </Accordion>

      <Accordion
        expanded={expanded === "panel2"}
        onChange={handleAccordionChange("panel2")}
        sx={{ width: "100%" }}
      >
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <Typography>12 to 18 Months before ETS</Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Suspense fallback={<LoadingFallback />}>
            <TwelveToEighteenMonths />
          </Suspense>
        </AccordionDetails>
      </Accordion>

      <Accordion
        expanded={expanded === "panel3"}
        onChange={handleAccordionChange("panel3")}
        sx={{ width: "100%" }}
      >
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <Typography>12 to 9 Months before ETS</Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Suspense fallback={<LoadingFallback />}>
            <NineToTwelveMonths />
          </Suspense>
        </AccordionDetails>
      </Accordion>

      <Accordion
        expanded={expanded === "panel4"}
        onChange={handleAccordionChange("panel4")}
        sx={{ width: "100%" }}
      >
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <Typography>6 to 9 Months before ETS</Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Suspense fallback={<LoadingFallback />}>
            <SixToNineMonths />
          </Suspense>
        </AccordionDetails>
      </Accordion>

      <Accordion
        expanded={expanded === "panel5"}
        onChange={handleAccordionChange("panel5")}
        sx={{ width: "100%" }}
      >
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <Typography>90 to 180 days before ETS</Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Suspense fallback={<LoadingFallback />}>
            <NinetyToOneEightyDays />
          </Suspense>
        </AccordionDetails>
      </Accordion>

      <Accordion
        expanded={expanded === "panel6"}
        onChange={handleAccordionChange("panel6")}
        sx={{ width: "100%" }}
      >
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <Typography>30 to 90 days before ETS</Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Suspense fallback={<LoadingFallback />}>
            <ThirtyToNinetyDays />
          </Suspense>
        </AccordionDetails>
      </Accordion>

      <Accordion
        expanded={expanded === "panel7"}
        onChange={handleAccordionChange("panel7")}
        sx={{ width: "100%" }} // Full width style applied
      >
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <Typography>10 to 30 days before ETS</Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Suspense fallback={<LoadingFallback />}>
            <ThirtyDays />
          </Suspense>
        </AccordionDetails>
      </Accordion>

      <Accordion
        expanded={expanded === "panel8"}
        onChange={handleAccordionChange("panel8")}
        sx={{ width: "100%" }}
      >
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <Typography>0 to 10 days before ETS</Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Suspense fallback={<LoadingFallback />}>
            <ZeroToTenDays />
          </Suspense>
        </AccordionDetails>
      </Accordion>

      <Accordion
        expanded={expanded === "panel9"}
        onChange={handleAccordionChange("panel9")}
        sx={{ width: "100%" }}
      >
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <Typography>Terminal leave</Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Suspense fallback={<LoadingFallback />}>
            <TerminalLeave />
          </Suspense>
        </AccordionDetails>
      </Accordion>

      <Accordion
        expanded={expanded === "panel10"}
        onChange={handleAccordionChange("panel10")}
        sx={{ width: "100%" }}
      >
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <Typography>0 to 30 days after ETS</Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Suspense fallback={<LoadingFallback />}>
            <ZeroToThirtyDaysPostETS />
          </Suspense>
        </AccordionDetails>
      </Accordion>

      <Accordion
        expanded={expanded === "panel11"}
        onChange={handleAccordionChange("panel11")}
        sx={{ width: "100%" }}
      >
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <Typography>Long after ETS</Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Suspense fallback={<LoadingFallback />}>
            <LongAfterETS />
          </Suspense>{" "}
        </AccordionDetails>
      </Accordion>
    </PageShell>
  );
}
