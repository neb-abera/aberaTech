import Divider from "@mui/material/Divider";
import PageShell from "../components/PageShell";
import CountdownsPanel from "../features/dates/components/CountdownsPanel";
import DateCalculator from "../features/dates/components/DateCalculator";

/**
 * The days between two dates, a date plus or minus a span, and the
 * owner's countdowns. The calculator is anyone's and runs in the browser.
 */
export default function Dates(props: { disableCustomTheme?: boolean }) {
  return (
    <PageShell
      {...props}
      maxWidth="md"
      title="Dates and countdowns"
      intro="Count the days between two dates, add or subtract a span, and keep countdowns that the alarms app on the phone shows too."
    >
      <DateCalculator />
      <Divider sx={{ my: 5 }} />
      <CountdownsPanel />
    </PageShell>
  );
}
