import Box from "@mui/material/Box";
import Checkbox from "@mui/material/Checkbox";
import FormControlLabel from "@mui/material/FormControlLabel";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import ToggleButton from "@mui/material/ToggleButton";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";
import Typography from "@mui/material/Typography";
import * as React from "react";
import {
  addToDate,
  between,
  type CalendarDate,
  describeDate,
  formatDate,
  parseDate,
  plural,
} from "../core/dateMath";

/** Today on this computer's calendar. */
function today(): string {
  const now = new Date();
  return formatDate({
    year: now.getFullYear(),
    month: now.getMonth() + 1,
    day: now.getDate(),
  });
}

function Heading({ id, children }: { id: string; children: string }) {
  return (
    <Typography id={id} variant="h2" sx={{ fontSize: "1.25rem", mb: 1 }}>
      {children}
    </Typography>
  );
}

function BetweenResult({
  start,
  end,
  includeEnd,
}: {
  start: CalendarDate;
  end: CalendarDate;
  includeEnd: boolean;
}) {
  const result = between(start, end, includeEnd);
  const total = Math.abs(result.totalDays);
  const hours = total * 24;
  return (
    <Box role="status" aria-label="Days between" sx={{ mt: 2 }}>
      <Typography variant="h3" sx={{ fontSize: "1.5rem", fontWeight: 600 }}>
        {plural(total, "day")}
        {result.before ? " before the start" : ""}
      </Typography>
      <Typography variant="body2" sx={{ color: "text.secondary", mb: 1 }}>
        From {describeDate(start)} to {describeDate(end)},{" "}
        {includeEnd ? "end date included" : "end date not included"}.
      </Typography>
      <Box component="ul" sx={{ m: 0, pl: 2.5 }}>
        <li>
          {plural(result.years, "year")}, {plural(result.months, "month")},{" "}
          {plural(result.days, "day")}
        </li>
        <li>
          {plural(result.weeks, "week")}, {plural(result.weekRemainder, "day")}
        </li>
        <li>{plural(result.weekdays, "weekday")}, Monday to Friday</li>
        <li>
          {plural(hours, "hour")}, {plural(hours * 60, "minute")},{" "}
          {plural(hours * 3600, "second")}
        </li>
      </Box>
      <Typography variant="body2" sx={{ color: "text.secondary", mt: 1 }}>
        Hours are days × 24. A day a clock change shortens or lengthens still
        counts as 24 hours.
      </Typography>
    </Box>
  );
}

const units = ["years", "months", "weeks", "days"] as const;
type Unit = (typeof units)[number];

/**
 * The days between two dates, and a date plus or minus years, months,
 * weeks and days. Plain dates on this computer's calendar, with no time
 * and no zone. Nothing is sent to the server.
 */
export default function DateCalculator() {
  const [start, setStart] = React.useState("");
  const [end, setEnd] = React.useState("");
  const [includeEnd, setIncludeEnd] = React.useState(false);
  const [base, setBase] = React.useState("");
  const [direction, setDirection] = React.useState<"add" | "subtract">("add");
  const [amounts, setAmounts] = React.useState<Record<Unit, string>>({
    years: "",
    months: "",
    weeks: "",
    days: "",
  });

  // Today is read after the page loads, so the prerendered page never
  // carries the day it was built.
  React.useEffect(() => {
    setStart((current) => current || today());
    setBase((current) => current || today());
  }, []);

  const from = parseDate(start);
  const to = parseDate(end);

  const counts = Object.fromEntries(
    units.map((unit) => [unit, amounts[unit].trim()]),
  ) as Record<Unit, string>;
  const badUnit = units.find(
    (unit) => counts[unit] !== "" && !/^\d{1,6}$/.test(counts[unit]),
  );
  const sign = direction === "add" ? 1 : -1;
  const number = (unit: Unit) => sign * Number(counts[unit] || "0");
  const baseDate = parseDate(base);
  const moved =
    baseDate && !badUnit
      ? addToDate(baseDate, {
          years: number("years"),
          months: number("months"),
          weeks: number("weeks"),
          days: number("days"),
        })
      : null;
  const named = units
    .filter((unit) => Number(counts[unit] || "0") !== 0)
    .map((unit) => plural(Number(counts[unit]), unit.slice(0, -1)));

  return (
    <Stack spacing={5}>
      <Box component="section" aria-labelledby="between-heading">
        <Heading id="between-heading">Days between two dates</Heading>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          <TextField
            label="Start date"
            type="date"
            value={start}
            onChange={(event) => setStart(event.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <TextField
            label="End date"
            type="date"
            value={end}
            onChange={(event) => setEnd(event.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
          />
        </Stack>
        <FormControlLabel
          control={
            <Checkbox
              checked={includeEnd}
              onChange={(_, checked) => setIncludeEnd(checked)}
            />
          }
          label="Include the end date (adds 1 day)"
        />
        {from && to ? (
          <BetweenResult start={from} end={to} includeEnd={includeEnd} />
        ) : (
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Pick both dates.
          </Typography>
        )}
      </Box>

      <Box component="section" aria-labelledby="add-heading">
        <Heading id="add-heading">Add to or subtract from a date</Heading>
        <Stack spacing={2}>
          <TextField
            label="Date"
            type="date"
            value={base}
            onChange={(event) => setBase(event.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
            sx={{ maxWidth: 240 }}
          />
          <ToggleButtonGroup
            exclusive
            size="small"
            value={direction}
            onChange={(_, value: "add" | "subtract" | null) =>
              value && setDirection(value)
            }
            aria-label="Add or subtract"
          >
            <ToggleButton value="add">Add</ToggleButton>
            <ToggleButton value="subtract">Subtract</ToggleButton>
          </ToggleButtonGroup>
          <Stack
            direction="row"
            spacing={2}
            useFlexGap
            sx={{ flexWrap: "wrap" }}
          >
            {units.map((unit) => (
              <TextField
                key={unit}
                label={unit[0].toUpperCase() + unit.slice(1)}
                value={amounts[unit]}
                onChange={(event) =>
                  setAmounts((current) => ({
                    ...current,
                    [unit]: event.target.value,
                  }))
                }
                error={badUnit === unit}
                helperText={badUnit === unit ? "A whole number." : " "}
                slotProps={{
                  htmlInput: { inputMode: "numeric", pattern: "[0-9]*" },
                }}
                sx={{ width: 110 }}
              />
            ))}
          </Stack>
        </Stack>
        {baseDate && moved ? (
          <Box role="status" aria-label="Resulting date" sx={{ mt: 1 }}>
            <Typography
              variant="h3"
              sx={{ fontSize: "1.5rem", fontWeight: 600 }}
            >
              {describeDate(moved)}
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              {describeDate(baseDate)}{" "}
              {named.length === 0
                ? "unchanged"
                : `${direction === "add" ? "+" : "−"} ${named.join(", ")}`}
              . Years and months first, held to the month's last day, then weeks
              and days.
            </Typography>
          </Box>
        ) : (
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {baseDate && !badUnit
              ? "Outside the years 1 to 9999."
              : "Pick a date and enter whole numbers."}
          </Typography>
        )}
      </Box>
    </Stack>
  );
}
