import InitColorSchemeScript from "@mui/material/InitColorSchemeScript";
import { StyledEngineProvider } from "@mui/material/styles";
import "./index.css";
import App from "./App.tsx";

/**
 * Everything inside the router, shared verbatim by the browser entry and the
 * build-time renderer. Hydration compares the prerendered markup against this
 * tree, so the two entries composing the same component is what keeps them
 * from drifting apart.
 *
 * InitColorSchemeScript applies a stored light/dark choice before first paint;
 * without it, a visitor who picked light gets a flash of the dark default on
 * every prerendered page. The attribute and default must match AppTheme's
 * cssVariables configuration. As plain client-rendered markup the script never
 * executes — React inserts it inertly — which is fine: on those pages the
 * provider sets the attribute itself, as it always has.
 */
export default function Shell() {
  return (
    <StyledEngineProvider injectFirst>
      <InitColorSchemeScript
        attribute="data-mui-color-scheme"
        defaultMode="dark"
      />
      <App />
    </StyledEngineProvider>
  );
}
