import Box from "@mui/material/Box";
import type { SxProps, Theme } from "@mui/material/styles";
import type { ReactNode } from "react";

/**
 * The bordered paper surface every card in the guides sits on. For the few
 * cards that set their own width, margin or alignment.
 */
export const cardSurface = (theme: Theme) => ({
  p: 2,
  border: `1px solid ${(theme.vars || theme).palette.divider}`,
  backgroundColor: (theme.vars || theme).palette.background.paper,
  color: (theme.vars || theme).palette.text.primary,
  boxShadow: theme.shadows[1],
});

/**
 * One card of a guide: the surface, full width, left aligned, with space
 * below. The transition guide's sections repeated this style 98 times.
 */
export default function GuideCard(props: {
  children: ReactNode;
  sx?: SxProps<Theme>;
}) {
  const { children, sx } = props;
  return (
    <Box
      sx={[
        (theme) => ({
          ...cardSurface(theme),
          width: "100%",
          marginBottom: 2,
          textAlign: "left",
        }),
        ...(Array.isArray(sx) ? sx : sx ? [sx] : []),
      ]}
    >
      {children}
    </Box>
  );
}
