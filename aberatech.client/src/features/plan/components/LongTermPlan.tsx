import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import CircularProgress from "@mui/material/CircularProgress";
import Link from "@mui/material/Link";
import Typography from "@mui/material/Typography";
import * as React from "react";
import { notionUrl } from "../../marksmanship/core/api";
import Blocks, { FilesContext } from "../../playbook/components/Blocks";
import SignInToSee from "../../progress/components/SignInToSee";
import {
  type CareerPlanResult,
  careerPlanFileUrl,
  fetchCareerPlan,
} from "../core/careerPlan";

const files = { url: careerPlanFileUrl, images: true };

/**
 * The career plan page, read live from Notion at each visit: the long-term
 * plan as one image per slide, then the goals under it. Notion stays the
 * record, and nothing here is stored.
 */
export default function LongTermPlan() {
  const [plan, setPlan] = React.useState<
    CareerPlanResult | { status: "loading" }
  >({ status: "loading" });

  React.useEffect(() => {
    const controller = new AbortController();
    void fetchCareerPlan(controller.signal).then((next) => {
      if (!controller.signal.aborted) setPlan(next);
    });
    return () => controller.abort();
  }, []);

  if (plan.status === "loading") {
    return <CircularProgress size={28} aria-label="Loading the plan" />;
  }
  if (plan.status === "visitor") {
    return (
      <SignInToSee
        message="This page is the owner's. Sign in to see it."
        returnUrl="/plan"
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
    <Box component="article" aria-label="Plan">
      <Box
        sx={{
          display: "flex",
          alignItems: "baseline",
          flexWrap: "wrap",
          columnGap: 2,
        }}
      >
        <Typography variant="h5" component="h2">
          {plan.page.title}
        </Typography>
        <Link
          href={notionUrl(plan.page.id)}
          target="_blank"
          rel="noopener noreferrer"
          aria-label={`Open ${plan.page.title} in Notion`}
        >
          Open in Notion
        </Link>
      </Box>
      <FilesContext.Provider value={files}>
        <Blocks blocks={plan.page.blocks} />
      </FilesContext.Provider>
    </Box>
  );
}
