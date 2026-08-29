import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import * as React from "react";
import { describeWait, formatTime } from "../core/format";
import type { MyPlace, QueueView } from "../core/types";
import SmsConsent from "./SmsConsent";

interface Props {
  queue: QueueView;
  place: MyPlace | null;
  onJoin: (
    name: string,
    phone: string,
    smsConsent: boolean,
  ) => Promise<string | null>;
  consentDisclosure: string;
  onLeave: () => Promise<void>;
}

/**
 * The queue: either the form to join it, or where you stand in it.
 *
 * Note what this never shows: who else is waiting. Position and count only. The
 * people in this queue are being seen for counselling, and who is in line ahead
 * of you is not something the person behind you is entitled to know.
 */
export default function QueuePanel({
  queue,
  place,
  onJoin,
  onLeave,
  consentDisclosure,
}: Props) {
  const [name, setName] = React.useState("");
  const [phone, setPhone] = React.useState("");
  const [smsConsent, setSmsConsent] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [busy, setBusy] = React.useState(false);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(await onJoin(name.trim(), phone.trim(), smsConsent));
    setBusy(false);
  };

  if (place && place.state === "Waiting") {
    return (
      <Card variant="outlined">
        <CardContent>
          <Stack spacing={2}>
            <Box
              sx={{
                display: "flex",
                alignItems: "center",
                gap: 1.5,
                flexWrap: "wrap",
              }}
            >
              <Chip color="primary" label={`Position ${place.position}`} />
              <Typography variant="body1">
                {place.ahead === 0
                  ? "You are next."
                  : `${place.ahead} ${place.ahead === 1 ? "person" : "people"} ahead of you.`}
              </Typography>
            </Box>

            <Typography variant="h6" component="p">
              {describeWait(place.minutesAway)}
              {place.projectedStart
                ? ` — around ${formatTime(place.projectedStart)}`
                : ""}
            </Typography>

            {place.beyondClose ? (
              <Alert severity="warning">
                On current estimates you will not be reached before I stop
                today. You are still in the queue in case things move, but it
                may be worth booking a time instead.
              </Alert>
            ) : (
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                This updates on its own. I will text you if the estimate moves
                by more than ten minutes, and again when you are up.
              </Typography>
            )}

            <Box>
              <Button
                variant="outlined"
                color="inherit"
                size="small"
                onClick={() => void onLeave()}
              >
                Leave the queue
              </Button>
            </Box>
          </Stack>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={2} component="form" onSubmit={submit}>
          <Box>
            <Typography variant="h6" component="p">
              {queue.name}
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              {queue.waiting === 0
                ? "Nobody is waiting. You would be first."
                : `${queue.waiting} waiting${queue.nextStartsAt ? `, next at ${formatTime(queue.nextStartsAt)}` : ""}.`}
            </Typography>

            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              Open until {formatTime(queue.closesAt)}
              {queue.estimatedStartIfYouJoin && queue.acceptingJoins
                ? `. Join now and you would be seen around ${formatTime(queue.estimatedStartIfYouJoin)}.`
                : "."}
            </Typography>
          </Box>

          {error ? <Alert severity="error">{error}</Alert> : null}

          {queue.acceptingJoins ? null : (
            <Alert severity="warning">
              The queue is full for today — anyone joining now would not be
              reached before it closes. Book a time instead and I will text you
              a confirmation.
            </Alert>
          )}

          <TextField
            label="Your name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            required
            size="small"
            slotProps={{ htmlInput: { maxLength: 120 } }}
          />
          <SmsConsent
            checked={smsConsent}
            onChange={setSmsConsent}
            disclosure={consentDisclosure}
          />

          {smsConsent ? (
            <TextField
              label="Mobile number"
              value={phone}
              onChange={(event) => setPhone(event.target.value)}
              required
              size="small"
              type="tel"
              helperText="US numbers only."
              slotProps={{ htmlInput: { maxLength: 32 } }}
            />
          ) : (
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              Without texts you will need to keep this page open to see your
              place move.
            </Typography>
          )}

          <Box>
            {/* Disabled only while a request is in flight. Not disabled on an
                empty form: the theme renders a disabled label at 30% white on a
                12% white ground, which is unreadable, and a greyed button never
                tells anybody what it wants. The fields are `required`, so the
                browser explains what is missing instead. */}
            <Button
              type="submit"
              variant="contained"
              disabled={busy || !queue.acceptingJoins}
            >
              {busy ? "Joining…" : "Join the queue"}
            </Button>
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}
