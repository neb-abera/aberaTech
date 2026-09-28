import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import type { AlertSettings } from "../core/api";
import { count } from "../core/settings";

/**
 * Which events alert and when, with the saved values in the sentences.
 * Each rule is what AlertPlanner.cs does. The Google Calendar menu names
 * are from support.google.com/calendar/answer/37242, checked 2026-09-28.
 */
export default function HowEventsAlert({
  settings,
}: {
  settings: AlertSettings;
}) {
  const lead = count(settings.defaultLeadMinutes, "minute");
  const list = {
    m: 0,
    pl: 3,
    "& li": { mb: 0.75 },
  } as const;
  return (
    <Box component="section" aria-labelledby="how-alerts-heading">
      <Typography
        id="how-alerts-heading"
        variant="h2"
        sx={{ fontSize: "1.25rem", mb: 1 }}
      >
        How events become alarms
      </Typography>
      <Typography variant="body1" sx={{ fontWeight: 600, mb: 0.5 }}>
        Which events alert
      </Typography>
      <Box component="ul" sx={list}>
        <li>
          Every event with a start time in the next{" "}
          {count(settings.lookaheadHours, "hour")} alerts. Nothing needs
          marking.
        </li>
        <li>Each occurrence of a repeating event alerts on its own.</li>
        <li>
          {settings.includeAllDay
            ? "All-day events alert too. Their start is midnight in the calendar's zone."
            : "All-day events are left out. The switch under Settings turns them on."}
        </li>
        <li>Cancelled events and invitations you declined are left out.</li>
      </Box>
      <Typography variant="body1" sx={{ fontWeight: 600, mt: 2, mb: 0.5 }}>
        When
      </Typography>
      <Box component="ul" sx={list}>
        <li>
          An event alerts at its earliest notification in Google Calendar. Email
          notifications do not count.
        </li>
        <li>
          An event with no notification in the feed alerts {lead} before it
          starts, the default lead.
        </li>
        <li>
          An alert whose time passed between two reads still goes if the event
          has not started.
        </li>
      </Box>
      <Typography variant="body1" sx={{ fontWeight: 600, mt: 2, mb: 0.5 }}>
        Setting notifications in Google Calendar
      </Typography>
      <Box component="ul" sx={list}>
        <li>
          One event: open it and click Edit event. Next to Notifications, change
          the time or click Add notification. Click Save.
        </li>
        <li>
          The calendar's default: open Settings. Under Settings for my
          calendars, click the calendar. Under Event notifications, change the
          time or click Add notification.
        </li>
        <li>
          Google does not say whether the secret address carries the calendar's
          default notifications. Each alert above says where its time came from.
          An event that shows the default lead has no notification in the feed.
          Set the default lead to the same minutes as the calendar's default,
          and every event alerts on time either way.
        </li>
      </Box>
    </Box>
  );
}
