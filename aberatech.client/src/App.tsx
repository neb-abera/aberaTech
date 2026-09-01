import React, { Suspense } from "react";
import { Route, Routes } from "react-router";
import "./App.css";
import ScrollToTop from "./components/ScrollToTop";
import { routes } from "./site/routes";

// Deliberately not in site/routes.ts: that list is the site's pages, and it is
// also what the build writes to app-routes.json for the server to answer 404
// against. A catch-all belongs to the router, not to the list of what exists.
const NotFound = React.lazy(() => import("./views/NotFound"));

// Fallback loading spinner or placeholder
const LoadingFallback = () => <div>Loading...</div>;

/**
 * The routes come from site/routes.ts rather than being written out here, so
 * there is one list of the site's pages instead of a JSX block that nothing
 * else can read. routes.test.ts checks that list against the navigation.
 */
function App() {
  return (
    <div>
      <ScrollToTop />
      <Suspense fallback={<LoadingFallback />}>
        <Routes>
          {routes.map(({ path, Page }) => (
            <Route key={path} path={path} element={<Page />} />
          ))}
          <Route path="*" element={<NotFound />} />
        </Routes>
      </Suspense>
    </div>
  );
}

export default App;
