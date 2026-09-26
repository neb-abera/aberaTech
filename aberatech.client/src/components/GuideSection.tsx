import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import {
  createContext,
  type ReactNode,
  Suspense,
  useCallback,
  useEffect,
  useRef,
  useState,
} from "react";
import { useLocation } from "react-router";
import { gray } from "../theme/themePrimitives";

/**
 * Whether the section around a component is open. DocumentFrame reads it to
 * mount its frame only once a reader can see it. Outside any section it is
 * true, so a frame placed in the open page loads as it always did.
 */
export const SectionOpen = createContext(true);

const LoadingFallback = () => <div>Loading...</div>;

/** Calls back once the contents around it have hydrated. */
function Hydrated(props: { onHydrated: () => void }) {
  const { onHydrated } = props;
  useEffect(onHydrated, [onHydrated]);
  return null;
}

/**
 * One collapsible section of a guide.
 *
 * The contents stay in the prerendered HTML while closed: nearly all of a
 * guide's words live in its sections, and a crawler reads only the HTML.
 * Each section opens and closes on its own, and opens when the address names
 * its id, on arrival or from a link within the page.
 *
 * The section is a native `<details>`, so the browser opens it without any
 * script: with JavaScript off, and when a reader clicks before the page has
 * hydrated. A click in that window used to reach a button with no handler
 * and do nothing. React adopts whatever the browser opened when it hydrates.
 */
export default function GuideSection(props: {
  id: string;
  title: ReactNode;
  children: ReactNode;
}) {
  const { id, title, children } = props;
  const { hash } = useLocation();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDetailsElement>(null);

  // A section the reader opened before hydration is open in the page but
  // not in React's state, and hydration leaves the attribute as it found it.
  useEffect(() => {
    if (ref.current?.open) setOpen(true);
  }, []);

  // The readiness signal: `data-hydrated` once the script behind the
  // section's contents has run. The end-to-end tests wait on it before
  // asserting that nothing loaded, never before a click.
  const markHydrated = useCallback(() => {
    ref.current?.setAttribute("data-hydrated", "");
  }, []);

  useEffect(() => {
    if (hash !== `#${id}`) return;
    setOpen(true);
    // After ScrollToTop, which sends every new page to the top in its own
    // effect, and after the section has laid out its first frame.
    const frame = requestAnimationFrame(() =>
      ref.current?.scrollIntoView({ block: "start" }),
    );
    return () => cancelAnimationFrame(frame);
  }, [hash, id]);

  return (
    <Box
      component="details"
      id={id}
      ref={ref}
      open={open}
      // A reader may open the section before hydration, so `open` can
      // differ from the HTML on purpose. The effect above adopts it.
      suppressHydrationWarning
      onToggle={(event) => setOpen(event.currentTarget.open)}
      sx={(theme) => ({
        width: "100%",
        padding: "4px",
        overflow: "clip",
        backgroundColor: (theme.vars || theme).palette.background.default,
        border: "1px solid",
        borderColor: (theme.vars || theme).palette.divider,
        "&:not(:last-of-type)": { borderBottom: "none" },
        "&:first-of-type": {
          borderTopLeftRadius: theme.shape.borderRadius,
          borderTopRightRadius: theme.shape.borderRadius,
        },
        "&:last-of-type": {
          borderBottomLeftRadius: theme.shape.borderRadius,
          borderBottomRightRadius: theme.shape.borderRadius,
        },
        "&[open] > summary > svg": { transform: "rotate(180deg)" },
      })}
    >
      <Box
        component="summary"
        sx={(theme) => ({
          // The browser slots a section's children through its own shadow
          // tree, which does not inherit the page's border-box. Both
          // children set it again, or a full-width card overflows by its
          // padding.
          boxSizing: "border-box",
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 2,
          minHeight: 48,
          paddingX: 2,
          borderRadius: "8px",
          cursor: "pointer",
          listStyle: "none",
          "&::-webkit-details-marker": { display: "none" },
          "&:hover": { backgroundColor: gray[50] },
          ...theme.applyStyles("dark", {
            "&:hover": { backgroundColor: gray[800] },
          }),
          "& > svg": {
            flexShrink: 0,
            color: (theme.vars || theme).palette.action.active,
            transition: theme.transitions.create("transform"),
          },
        })}
      >
        <Typography component="span">{title}</Typography>
        <ExpandMoreIcon />
      </Box>
      <Box sx={{ boxSizing: "border-box", padding: "8px 16px 16px" }}>
        <SectionOpen.Provider value={open}>
          <Suspense fallback={<LoadingFallback />}>
            {children}
            <Hydrated onHydrated={markHydrated} />
          </Suspense>
        </SectionOpen.Provider>
      </Box>
    </Box>
  );
}
