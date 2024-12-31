import { Route, Routes } from 'react-router';
import React, { Suspense } from 'react';
import './App.css';
import TechnicalTransitionGuide from './views/TechnicalTransitionGuide.tsx';

// Lazy loading the pages
const Home = React.lazy(() => import('./views/Home'));
const MarketingPage = React.lazy(() => import('./views/MarketingPage'));
const MilitaryTransitionGuide = React.lazy(() => import('./views/MilitaryTransitionGuide'));

// Fallback loading spinner or placeholder
const LoadingFallback = () => <div>Loading...</div>;

function App() {
  return (
    <div>
      <Suspense fallback={<LoadingFallback />}>
        <Routes>
          <Route index element={<Home />} />
          <Route path="marketing" element={<MarketingPage />} />
          <Route path="transition" element={<MilitaryTransitionGuide />} />
          <Route path="technical" element={<TechnicalTransitionGuide />} />
        </Routes>
      </Suspense>
    </div>
  );
}

export default App;
