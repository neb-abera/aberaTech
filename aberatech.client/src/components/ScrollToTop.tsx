import { useEffect } from 'react';
import { useLocation } from 'react-router';

/**
 * Send every navigation to the top of the page.
 *
 * A browser resets the scroll position when it loads a document, but a client
 * side router never loads one: it swaps the view underneath a window that is
 * still scrolled wherever the last page left it. Read halfway down the
 * transition guide, tap through to the planner, and you arrive 1800 pixels into
 * a page you have never seen, with no indication there is anything above you.
 *
 * Keyed on pathname rather than the whole location, so an in-page #anchor still
 * works and a query string change does not throw the reader back to the top.
 */
export default function ScrollToTop() {
  const { pathname } = useLocation();
  useEffect(() => {
    window.scrollTo({ top: 0, left: 0, behavior: 'instant' });
  }, [pathname]);
  return null;
}
