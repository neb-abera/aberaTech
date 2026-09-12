import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

export interface SignInToSeeProps {
  /** Why this page needs the owner, in one sentence. */
  message: string;
  /** The page to come back to once Google has answered. */
  returnUrl: string;
}

/**
 * What a visitor sees on an owner-only page: one sentence and the Google
 * sign-in button, which comes back to the page it was pressed on.
 *
 * Three pages had grown three copies of this block. The sign-in route is the
 * scheduling admin's, shared by every owner-only surface, so the one place
 * to change it is here.
 */
export default function SignInToSee({ message, returnUrl }: SignInToSeeProps) {
  return (
    <Stack spacing={2} sx={{ maxWidth: 420 }}>
      <Typography variant="body1" sx={{ color: "text.secondary" }}>
        {message}
      </Typography>
      <Box>
        <Button
          variant="contained"
          href={`/api/scheduling/admin/sign-in?returnUrl=${returnUrl}`}
        >
          Sign in with Google
        </Button>
      </Box>
    </Stack>
  );
}
