import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import Accordion from "@mui/material/Accordion";
import AccordionDetails from "@mui/material/AccordionDetails";
import AccordionSummary from "@mui/material/AccordionSummary";
import Typography from "@mui/material/Typography";
import {
  createContext,
  type ReactNode,
  Suspense,
  useEffect,
  useRef,
  useState,
} from "react";
import { useLocation } from "react-router";

/**
 * Whether the section around a component is open. DocumentFrame reads it to
 * mount its frame only once a reader can see it. Outside any section it is
 * true, so a frame placed in the open page loads as it always did.
 */
export const SectionOpen = createContext(true);

const LoadingFallback = () => <div>Loading...</div>;

/**
 * One collapsible section of a guide.
 *
 * The contents stay in the prerendered HTML while closed: nearly all of a
 * guide's words live in its sections, and a crawler reads only the HTML.
 * Each section opens and closes on its own, and opens when the address names
 * its id, on arrival or from a link within the page.
 */
export default function GuideSection(props: {
  id: string;
  title: ReactNode;
  children: ReactNode;
}) {
  const { id, title, children } = props;
  const { hash } = useLocation();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

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
    <Accordion
      id={id}
      ref={ref}
      expanded={open}
      onChange={(_, isOpen) => setOpen(isOpen)}
      sx={{ width: "100%" }}
    >
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Typography>{title}</Typography>
      </AccordionSummary>
      <AccordionDetails>
        <SectionOpen.Provider value={open}>
          <Suspense fallback={<LoadingFallback />}>{children}</Suspense>
        </SectionOpen.Provider>
      </AccordionDetails>
    </Accordion>
  );
}
