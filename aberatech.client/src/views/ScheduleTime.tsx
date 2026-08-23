import PageShell from '../components/PageShell';
import SchedulePanel from '../features/scheduling/components/SchedulePanel';

export default function ScheduleTime(props: { disableCustomTheme?: boolean }) {
  return (
    <PageShell
      {...props}
      maxWidth="md"
      title="Schedule time with me"
      intro="Pick a time from my calendar, or join the queue when one is open and I will work down the line. Either way this is the same link, and every time on this page is shown in your own time zone rather than mine."
    >
      <SchedulePanel />
    </PageShell>
  );
}
