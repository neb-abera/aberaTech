import CircleIcon from "@mui/icons-material/Circle";
import Timeline from "@mui/lab/Timeline";
import TimelineConnector from "@mui/lab/TimelineConnector";
import TimelineContent from "@mui/lab/TimelineContent";
import TimelineDot from "@mui/lab/TimelineDot";
import TimelineItem, { timelineItemClasses } from "@mui/lab/TimelineItem";
import TimelineSeparator from "@mui/lab/TimelineSeparator";
import Typography from "@mui/material/Typography";
import GuideCard from "./GuideCard";

export default function TerminalLeave() {
  return (
    <GuideCard>
      <Typography
        variant="h2"
        component="h2"
        sx={{ marginBottom: 2, textAlign: "center" }}
      >
        <strong>30-90 days</strong>
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
                If you're a Reservist then check on the status of your ETS PAR.
                Submit RSTs in case your orders are delayed. If you're Active
                Duty, at 30 days out apply for your installation clearance
                papers through your BN S1.
              </Typography>
              <Typography>
                Fort Moore G1 won't process installation clearance paper
                requests until you've completed CSP.
              </Typography>
            </GuideCard>
          </TimelineContent>
        </TimelineItem>
      </Timeline>
    </GuideCard>
  );
}
