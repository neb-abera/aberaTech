import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import React from "react";
import { Link } from "react-router";
import { cardSurface } from "../components/GuideCard";
import GuideSection from "../components/GuideSection";
import PageShell from "../components/PageShell";
import { guides } from "../site/sections";

// Each section's contents, loaded with the section.
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

export default function MilitaryTransitionGuide(props: {
  disableCustomTheme?: boolean;
}) {
  return (
    <PageShell {...props} title={guides[0].title} intro={guides[0].blurb}>
      <Box sx={cardSurface}>
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

      <GuideSection id="18-to-24-months" title="18 to 24 Months before ETS">
        <EighteenToTwentyFourMonths />
      </GuideSection>

      <GuideSection id="12-to-18-months" title="12 to 18 Months before ETS">
        <TwelveToEighteenMonths />
      </GuideSection>

      <GuideSection id="9-to-12-months" title="9 to 12 Months before ETS">
        <NineToTwelveMonths />
      </GuideSection>

      <GuideSection id="6-to-9-months" title="6 to 9 Months before ETS">
        <SixToNineMonths />
      </GuideSection>

      <GuideSection id="90-to-180-days" title="90 to 180 days before ETS">
        <NinetyToOneEightyDays />
      </GuideSection>

      <GuideSection id="30-to-90-days" title="30 to 90 days before ETS">
        <ThirtyToNinetyDays />
      </GuideSection>

      <GuideSection id="10-to-30-days" title="10 to 30 days before ETS">
        <ThirtyDays />
      </GuideSection>

      <GuideSection id="0-to-10-days" title="0 to 10 days before ETS">
        <ZeroToTenDays />
      </GuideSection>

      <GuideSection id="terminal-leave" title="Terminal leave">
        <TerminalLeave />
      </GuideSection>

      <GuideSection id="0-to-30-days-after" title="0 to 30 days after ETS">
        <ZeroToThirtyDaysPostETS />
      </GuideSection>

      <GuideSection id="long-after-ets" title="Long after ETS">
        <LongAfterETS />
      </GuideSection>
    </PageShell>
  );
}
