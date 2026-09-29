import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import type { AlertSettings } from "../core/api";
import { alarmLine, count } from "../core/settings";

/**
 * Which events alert, as what, and when, with the saved values in the
 * sentences. Each rule is what AlertPlanner.cs and AlertTypes.cs do. The
 * Google Calendar menu names are from
 * support.google.com/calendar/answer/37242, checked 2026-09-28.
 */
export default function HowEventsAlert({
  settings,
  maxSounds,
}: {
  settings: AlertSettings;
  maxSounds: number;
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
        Three types
      </Typography>
      <Box component="ul" sx={list}>
        <li>Alarm: {alarmLine(settings, maxSounds)}</li>
        <li>
          Notification: one message with one sound, at the notification priority
          and sound under Settings.
        </li>
        <li>None: nothing is sent. The event is still listed above.</li>
      </Box>
      <Typography variant="body1" sx={{ fontWeight: 600, mt: 2, mb: 0.5 }}>
        Which type an event gets
      </Typography>
      <Box component="ul" sx={list}>
        <li>
          A type chosen on this page wins. Each alert above has None,
          Notification and Alarm. The choice holds for every occurrence of a
          repeating event. Use default removes it.
        </li>
        <li>
          Otherwise an event becomes an alarm when its title or description in
          Google Calendar has #critical as a word of its own, in any case. The
          mark is left off the title the phone shows.
        </li>
        <li>
          {settings.defaultType === "notification"
            ? "Every other event gets one notification, the default for unmarked events under Settings."
            : "Every other event sends nothing, the default for unmarked events under Settings."}
        </li>
      </Box>
      <Typography variant="body1" sx={{ fontWeight: 600, mt: 2, mb: 0.5 }}>
        Which events are planned
      </Typography>
      <Box component="ul" sx={list}>
        <li>
          Every event with a start time in the next{" "}
          {count(settings.lookaheadHours, "hour")} is planned and listed above.
        </li>
        <li>Each occurrence of a repeating event is planned on its own.</li>
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
          The secret address carries only notifications set on the event itself.
          The calendar's default notifications are not in it. Set the default
          lead to the same minutes as the calendar's default, and every event
          alerts at that time. Each alert above says where its time came from.
        </li>
      </Box>
    </Box>
  );
}
