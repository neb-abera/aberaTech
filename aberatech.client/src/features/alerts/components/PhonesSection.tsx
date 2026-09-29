import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Link from "@mui/material/Link";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import QRCode from "qrcode";
import * as React from "react";
import {
  type Device,
  type DevicesResult,
  listDevices,
  type PairedDevice,
  type PairResult,
  pairDevice,
  revokeDevice,
} from "../core/api";

/** What the section calls. The page passes the real ones, a test its own. */
export interface PhonesApi {
  list: () => Promise<DevicesResult>;
  pair: (name: string) => Promise<PairResult>;
  revoke: (id: string) => ReturnType<typeof revokeDevice>;
}

export const phonesApi: PhonesApi = {
  list: listDevices,
  pair: pairDevice,
  revoke: revokeDevice,
};

/** The longest name the server takes. */
const maxName = 60;

/**
 * The pairing link as a QR code, drawn as one SVG path from the code's
 * modules. Nothing is fetched and no image source is set, so the page's
 * content security policy needs nothing new.
 */
export function PairingQr({ value }: { value: string }) {
  const path = React.useMemo(() => {
    const { modules } = QRCode.create(value, { errorCorrectionLevel: "M" });
    const parts: string[] = [];
    for (let row = 0; row < modules.size; row++) {
      for (let column = 0; column < modules.size; column++) {
        if (modules.get(row, column))
          parts.push(`M${column + 4} ${row + 4}h1v1h-1z`);
      }
    }
    return { d: parts.join(""), size: modules.size + 8 };
  }, [value]);

  return (
    <Box
      component="svg"
      role="img"
      aria-label="QR code that pairs a phone"
      viewBox={`0 0 ${path.size} ${path.size}`}
      shapeRendering="crispEdges"
      sx={{
        width: 224,
        height: 224,
        display: "block",
        bgcolor: "#fff",
        borderRadius: 1,
      }}
    >
      <rect width={path.size} height={path.size} fill="#fff" />
      <path d={path.d} fill="#000" />
    </Box>
  );
}

/**
 * The phones that mirror alarms. Pairing makes a token and shows it once,
 * as a QR code, a link and text. The list names each phone, when it was
 * last seen and whether it takes pushes, and Revoke stops its token on its
 * next request.
 */
export default function PhonesSection({
  api = phonesApi,
  when,
  pushMissing = [],
}: {
  api?: PhonesApi;
  when: (iso: string) => string;
  /** The push secrets the server lacks. Empty when pushes go. */
  pushMissing?: string[];
}) {
  const [devices, setDevices] = React.useState<Device[] | null>(null);
  const [name, setName] = React.useState("");
  const [paired, setPaired] = React.useState<PairedDevice | null>(null);
  const [confirming, setConfirming] = React.useState<string | null>(null);
  const [busy, setBusy] = React.useState(false);
  const [problem, setProblem] = React.useState<string | null>(null);

  const load = React.useCallback(async () => {
    const result = await api.list();
    if (result.ok) setDevices(result.devices);
    else setProblem("The list of phones did not load. Refresh to try again.");
  }, [api]);

  React.useEffect(() => {
    void load();
  }, [load]);

  const pair = async () => {
    setBusy(true);
    setProblem(null);
    const result = await api.pair(name.trim());
    setBusy(false);
    if (result.ok) {
      setPaired(result.device);
      setName("");
      await load();
      return;
    }
    setProblem(
      result.reason === "full"
        ? (result.detail ?? "At most 5 phones. Revoke one first.")
        : result.reason === "invalid"
          ? `A name of 1 to ${maxName} characters.`
          : result.reason === "throttled"
            ? "Too many presses. Wait a minute."
            : result.reason === "visitor"
              ? "The session expired. Reload and sign in again."
              : "The server did not take it. Try again.",
    );
  };

  const revoke = async (device: Device) => {
    setBusy(true);
    setProblem(null);
    const result = await api.revoke(device.id);
    setBusy(false);
    setConfirming(null);
    if (paired?.id === device.id) setPaired(null);
    if (!result.ok) {
      setProblem("The server did not revoke it. Try again.");
      return;
    }
    await load();
  };

  const trimmed = name.trim();

  return (
    <Box component="section" aria-labelledby="phones-heading">
      <Typography
        id="phones-heading"
        variant="h2"
        sx={{ fontSize: "1.25rem", mb: 1 }}
      >
        Phones
      </Typography>
      <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
        A paired iPhone running Abera Alarms rings every alarm itself, offline
        too. Up to 5 phones.
      </Typography>
      <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
        A push asks the phone to update its alarms at once. iOS can delay or
        drop it, most of all after the app is swiped away, so the phone also
        updates when opened and in the background.
      </Typography>
      {pushMissing.length > 0 && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Pushes are off. The server is missing {pushMissing.join(", ")}.
        </Alert>
      )}

      <Stack
        direction="row"
        spacing={1}
        useFlexGap
        sx={{ alignItems: "flex-start", flexWrap: "wrap" }}
      >
        <TextField
          label="Phone name"
          size="small"
          value={name}
          onChange={(event) => setName(event.target.value)}
          slotProps={{ htmlInput: { maxLength: maxName } }}
          sx={{ width: "16rem", maxWidth: "100%" }}
        />
        <Button
          variant="contained"
          disabled={busy || trimmed.length === 0}
          onClick={() => void pair()}
        >
          Pair a phone
        </Button>
      </Stack>

      {problem && (
        <Alert
          severity="warning"
          sx={{ mt: 2 }}
          onClose={() => setProblem(null)}
        >
          {problem}
        </Alert>
      )}

      {paired && (
        <Box
          role="region"
          aria-label={`Pairing ${paired.name}`}
          sx={{
            mt: 2,
            p: 2,
            border: 1,
            borderColor: "divider",
            borderRadius: 1,
          }}
        >
          <Stack spacing={1.5}>
            <Typography variant="body1">
              Scan this with the phone's camera, or open the link on the phone
              itself.
            </Typography>
            <PairingQr value={paired.pairUrl} />
            <Link href={paired.pairUrl} underline="always">
              Open on this phone
            </Link>
            <Typography variant="body2">
              Token, shown this once:{" "}
              <Box
                component="code"
                aria-label="Token"
                sx={{ wordBreak: "break-all" }}
              >
                {paired.token}
              </Box>
            </Typography>
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              The server keeps only a hash of the token. Once this is closed it
              cannot be shown again. Pair again to make a new one.
            </Typography>
            <Box>
              <Button size="small" onClick={() => setPaired(null)}>
                Done
              </Button>
            </Box>
          </Stack>
        </Box>
      )}

      {devices !== null && devices.length === 0 && (
        <Typography variant="body1" sx={{ mt: 2, color: "text.secondary" }}>
          No phones paired.
        </Typography>
      )}
      {devices !== null && devices.length > 0 && (
        <Stack
          component="ul"
          aria-label="Paired phones"
          spacing={1}
          sx={{ listStyle: "none", p: 0, m: 0, mt: 2 }}
        >
          {devices.map((device) => (
            <Box
              component="li"
              key={device.id}
              sx={{
                border: 1,
                borderColor: "divider",
                borderRadius: 1,
                p: 1.5,
                display: "flex",
                gap: 1,
                alignItems: "center",
                flexWrap: "wrap",
              }}
            >
              <Box sx={{ flex: "1 1 14rem", minWidth: 0 }}>
                <Typography variant="body1" sx={{ fontWeight: 600 }}>
                  {device.name}
                </Typography>
                <Typography variant="body2" sx={{ color: "text.secondary" }}>
                  Paired {when(device.createdAt)}.{" "}
                  {device.lastSeenAt
                    ? `Last seen ${when(device.lastSeenAt)}.`
                    : "Not seen yet."}
                </Typography>
                <Typography variant="body2" sx={{ color: "text.secondary" }}>
                  {device.push ? "Push on" : "Push off"}
                </Typography>
              </Box>
              {confirming === device.id ? (
                <>
                  <Button
                    size="small"
                    color="error"
                    variant="contained"
                    disabled={busy}
                    onClick={() => void revoke(device)}
                    aria-label={`Yes, revoke ${device.name}`}
                  >
                    Yes, revoke
                  </Button>
                  <Button size="small" onClick={() => setConfirming(null)}>
                    Cancel
                  </Button>
                </>
              ) : (
                <Button
                  size="small"
                  variant="outlined"
                  color="error"
                  disabled={busy}
                  onClick={() => setConfirming(device.id)}
                  aria-label={`Revoke ${device.name}`}
                >
                  Revoke
                </Button>
              )}
            </Box>
          ))}
        </Stack>
      )}
    </Box>
  );
}
