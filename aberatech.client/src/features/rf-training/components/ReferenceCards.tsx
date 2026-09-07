import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import {
  bandLengths,
  decibels,
  formulas,
  phonetic,
  prowords,
  zuluOffsets,
} from "../core/reference";

const feet = (value: number): string => {
  const whole = Math.floor(value);
  const inches = Math.round((value - whole) * 12);
  return inches === 12 ? `${whole + 1}′ 0″` : `${whole}′ ${inches}″`;
};

/**
 * The cards, as compact tables. One Print button for the lot; the browser's
 * print dialog is the export, so nothing here downloads a file.
 */
export default function ReferenceCards() {
  return (
    <Stack spacing={3}>
      <Button
        variant="outlined"
        size="small"
        onClick={() => window.print()}
        sx={{ alignSelf: "flex-start" }}
      >
        Print the cards and the plan template
      </Button>

      <Card title="Formulas">
        <Table size="small" aria-label="Formulas">
          <TableBody>
            {formulas.map((formula) => (
              <TableRow key={formula.name}>
                <TableCell component="th" scope="row" sx={{ fontWeight: 600 }}>
                  {formula.name}
                </TableCell>
                <TableCell sx={{ fontFamily: "monospace" }}>
                  {formula.formula}
                </TableCell>
                <TableCell sx={{ color: "text.secondary" }}>
                  {formula.note}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>

      <Card title="Decibels">
        <Table size="small" aria-label="Decibel table">
          <TableHead>
            <TableRow>
              <TableCell>dB</TableCell>
              <TableCell align="right">Power ratio</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {decibels.map((row) => (
              <TableRow key={row.db}>
                <TableCell>{row.db}</TableCell>
                <TableCell align="right">{row.ratio}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>

      <Card title="Antenna lengths by band">
        <Table size="small" aria-label="Antenna lengths">
          <TableHead>
            <TableRow>
              <TableCell>MHz</TableCell>
              <TableCell align="right">λ (m)</TableCell>
              <TableCell align="right">Half-wave</TableCell>
              <TableCell align="right">Each leg</TableCell>
              <TableCell align="right">14% feed point</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {bandLengths.map((band) => (
              <TableRow key={band.mhz}>
                <TableCell>{band.mhz}</TableCell>
                <TableCell align="right">{band.metres.toFixed(1)}</TableCell>
                <TableCell align="right">{feet(band.halfWaveFeet)}</TableCell>
                <TableCell align="right">{feet(band.legFeet)}</TableCell>
                <TableCell align="right">{feet(band.ocfFeet)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>

      <Card title="Phonetic alphabet">
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: {
              xs: "repeat(2, 1fr)",
              sm: "repeat(4, 1fr)",
            },
            gap: 0.5,
          }}
        >
          {phonetic.map(([letter, word]) => (
            <Typography key={letter} variant="body2">
              <Box component="span" sx={{ fontWeight: 600 }}>
                {letter}
              </Box>{" "}
              {word}
            </Typography>
          ))}
        </Box>
      </Card>

      <Card title="Prowords">
        <Table size="small" aria-label="Prowords">
          <TableBody>
            {prowords.map(([word, meaning]) => (
              <TableRow key={word}>
                <TableCell
                  component="th"
                  scope="row"
                  sx={{ fontWeight: 600, whiteSpace: "nowrap" }}
                >
                  {word}
                </TableCell>
                <TableCell>{meaning}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>

      <Card title="Zulu offsets">
        <Table size="small" aria-label="Zulu offsets">
          <TableBody>
            {zuluOffsets.map(([zone, offset]) => (
              <TableRow key={zone}>
                <TableCell component="th" scope="row">
                  {zone}
                </TableCell>
                <TableCell align="right">UTC{offset}</TableCell>
                <TableCell sx={{ color: "text.secondary" }}>
                  Zulu = local + {Math.abs(offset)} h
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
    </Stack>
  );
}

function Card({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <Box component="section" aria-label={title}>
      <Typography
        variant="subtitle1"
        component="h3"
        sx={{ fontWeight: 600, mb: 0.5 }}
      >
        {title}
      </Typography>
      <TableContainer>{children}</TableContainer>
    </Box>
  );
}
