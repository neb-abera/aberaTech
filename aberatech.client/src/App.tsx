import { Suspense } from "react";
import { Route, Routes } from "react-router";
import "./App.css";
import ScrollToTop from "./components/ScrollToTop";
import { routes } from "./site/routes";

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
        </Routes>
      </Suspense>
    </div>
  );
}

export default App;
