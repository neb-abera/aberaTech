import React, { Suspense } from "react";
import { Route, Routes } from "react-router";
import "./App.css";
import ScrollToTop from "./components/ScrollToTop";
import TechnicalTransitionGuide from "./views/TechnicalTransitionGuide.tsx";

// Lazy loading the pages
const Home = React.lazy(() => import("./views/Home"));
const MarketingPage = React.lazy(() => import("./views/MarketingPage"));
const MilitaryTransitionGuide = React.lazy(
  () => import("./views/MilitaryTransitionGuide"),
);
const CoursePlanner = React.lazy(() => import("./views/CoursePlanner"));
const ScheduleTime = React.lazy(() => import("./views/ScheduleTime"));
const Fitness = React.lazy(() => import("./views/Fitness"));
const ScheduleAdmin = React.lazy(() => import("./views/ScheduleAdmin"));
const Guides = React.lazy(() => import("./views/Guides"));
const Projects = React.lazy(() => import("./views/Projects"));

// Fallback loading spinner or placeholder
const LoadingFallback = () => <div>Loading...</div>;

function App() {
  return (
    <div>
      <ScrollToTop />
      <Suspense fallback={<LoadingFallback />}>
        <Routes>
          <Route index element={<Home />} />
          <Route path="marketing" element={<MarketingPage />} />
          <Route path="guides" element={<Guides />} />
          <Route path="projects" element={<Projects />} />
          <Route path="transition" element={<MilitaryTransitionGuide />} />
          <Route path="technical" element={<TechnicalTransitionGuide />} />
          <Route path="planner" element={<CoursePlanner />} />
          <Route path="schedule" element={<ScheduleTime />} />
          <Route path="fitness" element={<Fitness />} />
          <Route path="schedule/admin" element={<ScheduleAdmin />} />
        </Routes>
      </Suspense>
    </div>
  );
}

export default App;
