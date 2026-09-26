import CircleIcon from "@mui/icons-material/Circle";
import Timeline from "@mui/lab/Timeline";
import TimelineConnector from "@mui/lab/TimelineConnector";
import TimelineContent from "@mui/lab/TimelineContent";
import TimelineDot from "@mui/lab/TimelineDot";
import TimelineItem, { timelineItemClasses } from "@mui/lab/TimelineItem";
import TimelineSeparator from "@mui/lab/TimelineSeparator";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import { Link } from "react-router";
import GuideCard from "./GuideCard";

export default function LongAfterETS() {
  return (
    <GuideCard>
      <Typography
        variant="h2"
        component="h2"
        sx={{ marginBottom: 2, textAlign: "center" }}
      >
        <strong>Long after you ETS</strong>
      </Typography>
      <Timeline
        sx={{
          [`& .${timelineItemClasses.root}:before`]: {
            flex: 0,
            padding: 0,
          },
        }}
      >
        <TimelineItem>
          <TimelineSeparator>
            <TimelineDot color="primary">
              <CircleIcon />
            </TimelineDot>
            <TimelineConnector />
          </TimelineSeparator>
          <TimelineContent>
            <GuideCard>
              <Typography sx={{ fontWeight: "bold", marginBottom: 2 }}>
                Your first post-transition job might not be what you wanted, or
                how you thought it was going to be. It's okay to job-hop.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                Companies generally are more likely to pay higher compensation
                packages to new hires than they are to give internal employees
                raises. Therefore,{" "}
                <strong>
                  job hopping is the easiest way to get a substantial raise
                </strong>
                .
              </Typography>
              <Typography>
                If you can't meet the initial requirements to get hired into a
                company for your target job, it is likely easier to transfer to
                that role as an internal employee. For example, getting a
                software engineer role as an outside hire at Amazon is rigorous,
                but moving to another team as an employee is easier.
              </Typography>
            </GuideCard>
          </TimelineContent>
        </TimelineItem>
        <TimelineItem>
          <TimelineSeparator>
            <TimelineDot color="primary">
              <CircleIcon />
            </TimelineDot>
            <TimelineConnector />
          </TimelineSeparator>
          <TimelineContent>
            <GuideCard>
              <Typography sx={{ fontWeight: "bold", marginBottom: 2 }}>
                Attend conferences for your chosen profession.
              </Typography>
              <Typography sx={{ marginBottom: 2 }}>
                These are great places to network and learn about industry
                trends.
              </Typography>
              <Typography>
                <Link
                  to="https://www.aventri.com/strategy/top-conferences-to-attend-this-year"
                  target="_blank"
                  rel="noopener"
                >
                  Top Conferences to Attend in 2022 by Industries | Aventri
                </Link>
              </Typography>
            </GuideCard>
          </TimelineContent>
        </TimelineItem>
        <TimelineItem>
          <TimelineSeparator>
            <TimelineDot color="primary">
              <CircleIcon />
            </TimelineDot>
            <TimelineConnector />
          </TimelineSeparator>
          <TimelineContent>
            <GuideCard>
              <Typography sx={{ fontWeight: "bold", marginBottom: 2 }}>
                You have benefits that last long after you ETS.
              </Typography>
              <Typography>
                You learn about them during TAP. For example, you can use VET
                TEC a second time if you need help transitioning to another job
                again as long as it's 18 months between courses.
              </Typography>
            </GuideCard>
          </TimelineContent>
        </TimelineItem>
        <TimelineItem>
          <TimelineSeparator>
            <TimelineDot color="primary">
              <CircleIcon />
            </TimelineDot>
            <TimelineConnector />
          </TimelineSeparator>
          <TimelineContent>
            <GuideCard>
              <Box
                component="a"
                href="https://nvf.org/veteran-service-officers/"
                target="_blank"
                rel="noopener noreferrer"
                sx={{
                  textDecoration: "none",
                  color: "inherit",
                  display: "block",
                }}
              >
                <Box
                  sx={{
                    display: "flex",
                    justifyContent: "center",
                    marginBottom: 2,
                  }}
                >
                  <Box
                    component="img"
                    // The file's own size, measured 2026-09-26, so the space is held
                    // before the image arrives. Hot-linked: NVF's terms forbid copying
                    // or public display of its materials (third-party-images.json).
                    src="https://nvf.org/wp-content/uploads/2015/03/veteran-service-officers.jpg"
                    width={400}
                    height={300}
                    loading="lazy"
                    decoding="async"
                    alt="Veteran Service Officers - List of Where to Find Help"
                    sx={{
                      maxWidth: "450px",
                      width: "100%",
                      height: "auto",
                      borderRadius: 2,
                      boxShadow: 3,
                    }}
                  />
                </Box>
                <Typography
                  variant="h4"
                  sx={{ fontWeight: "bold", marginBottom: 1 }}
                >
                  Veteran Service Officers - List of Where to Find Help
                </Typography>
                <Typography sx={{ marginBottom: 1 }}>
                  Veteran Service Officers help you navigate the VA. They help
                  with gathering information necessary to support a claim
                  through the VA.
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  nvf.org
                </Typography>
              </Box>
            </GuideCard>
          </TimelineContent>
        </TimelineItem>
        <TimelineItem>
          <TimelineSeparator>
            <TimelineDot color="primary">
              <CircleIcon />
            </TimelineDot>
            <TimelineConnector />
          </TimelineSeparator>
          <TimelineContent>
            <GuideCard>
              <Box
                component="a"
                href="https://www.va.gov/disability/get-help-filing-claim/"
                target="_blank"
                rel="noopener noreferrer"
                sx={{
                  textDecoration: "none",
                  color: "inherit",
                  display: "block",
                }}
              >
                <Box
                  sx={{
                    display: "flex",
                    justifyContent: "center",
                    marginBottom: 2,
                  }}
                >
                  <Box
                    component="img"
                    // The file's own size, measured 2026-09-26, so the space is held
                    // before the image arrives. Hot-linked: it is a replica of the VA
                    // seal, and 38 CFR 1.9 limits where a replica may appear
                    // (third-party-images.json).
                    src="https://www.va.gov/img/design/logo/va-og-image.png"
                    width={1200}
                    height={630}
                    loading="lazy"
                    decoding="async"
                    alt="Get help from a VA accredited representative or VSO | Veterans Affairs"
                    sx={{
                      maxWidth: "450px",
                      width: "100%",
                      height: "auto",
                      borderRadius: 2,
                      boxShadow: 3,
                    }}
                  />
                </Box>
                <Typography
                  variant="h4"
                  sx={{ fontWeight: "bold", marginBottom: 1 }}
                >
                  Get help from a VA accredited representative or VSO | Veterans
                  Affairs
                </Typography>
                <Typography sx={{ marginBottom: 1 }}>
                  An accredited attorney, claims agent, or Veterans Service
                  Organization (VSO) representative can help you file a claim or
                  request a decision review. Learn how to find and appoint one
                  of these types of accredited representatives to help you.
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  www.va.gov
                </Typography>
              </Box>
              <Typography sx={{ marginTop: 2 }}>
                You may need to fight for benefits. Put up a fight to get the
                benefits you deserve. The VA is a bureaucratic system, but you
                have government and non-governmental support. You can reach out
                to organizations like{" "}
                <Link
                  to="https://www.dav.org/veterans/"
                  target="_blank"
                  rel="noopener"
                >
                  DAV
                </Link>{" "}
                for help while working on your disability claim with the VA.
              </Typography>
            </GuideCard>
          </TimelineContent>
        </TimelineItem>
        <TimelineItem>
          <TimelineSeparator>
            <TimelineDot color="primary">
              <CircleIcon />
            </TimelineDot>
            <TimelineConnector />
          </TimelineSeparator>
          <TimelineContent>
            <GuideCard>
              <Typography>
                If you lose your DD214, you can request a copy from{" "}
                <Link
                  to="https://www.va.gov/records/get-military-service-records/"
                  target="_blank"
                  rel="noopener"
                >
                  here
                </Link>
                .
              </Typography>
            </GuideCard>
          </TimelineContent>
        </TimelineItem>
        <TimelineItem>
          <TimelineSeparator>
            <TimelineDot color="primary">
              <CircleIcon />
            </TimelineDot>
            <TimelineConnector />
          </TimelineSeparator>
          <TimelineContent>
            <GuideCard>
              <Typography sx={{ fontWeight: "bold", marginBottom: 2 }}>
                Give back to veterans.
              </Typography>
              <Typography>
                You got a lot of help along the way. Do what you can to give
                back.
              </Typography>
            </GuideCard>
          </TimelineContent>
        </TimelineItem>
      </Timeline>
    </GuideCard>
  );
}
