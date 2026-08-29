import Box from "@mui/material/Box";
import Checkbox from "@mui/material/Checkbox";
import FormControlLabel from "@mui/material/FormControlLabel";
import Link from "@mui/material/Link";
import Typography from "@mui/material/Typography";

interface Props {
  checked: boolean;
  onChange: (checked: boolean) => void;
  /**
   * The wording to show, sent by the server.
   *
   * Not written here, because the server stores a copy of exactly this text
   * alongside each consent. Two copies of the disclosure would drift, and the
   * stored evidence would then describe something nobody was actually shown.
   */
  disclosure: string;
}

/**
 * The consent control, and the disclosures the carriers require beside it.
 *
 * One component used by both the booking form and the queue form, so the two
 * cannot drift apart. A campaign registration is reviewed against a screenshot
 * of this, and every element below is there because the review looks for it:
 * what will be sent, how often, that rates may apply, HELP and STOP, and links
 * to the terms and the privacy policy.
 *
 * The box starts unticked and nothing ticks it but the visitor. That is the
 * rule most often broken and the most common reason a registration is refused —
 * a pre-ticked box is not consent, it is an assumption with a tick next to it.
 */
export default function SmsConsent({ checked, onChange, disclosure }: Props) {
  return (
    <Box>
      <FormControlLabel
        control={
          <Checkbox
            checked={checked}
            onChange={(event) => onChange(event.target.checked)}
            size="small"
          />
        }
        label={
          <Typography variant="body2">
            Yes, text me about this appointment
          </Typography>
        }
      />

      {/* The server's wording, rendered as sent. It stores a copy of exactly
          this text alongside each consent, so a second copy written here would
          drift and the stored evidence would describe something nobody saw. */}
      <Typography
        variant="caption"
        sx={{
          display: "block",
          color: "text.secondary",
          mt: 0.5,
          lineHeight: 1.6,
        }}
      >
        {disclosure}
      </Typography>

      <Typography
        variant="caption"
        sx={{ display: "block", color: "text.secondary", mt: 0.5 }}
      >
        <Link href="/sms-terms" target="_blank" rel="noopener">
          Text message terms
        </Link>
        {" · "}
        <Link href="/sms-privacy" target="_blank" rel="noopener">
          Privacy policy
        </Link>
      </Typography>
    </Box>
  );
}
