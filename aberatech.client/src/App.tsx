import { Routes, Route } from 'react-router';
import Home from './views/Home';
import MarketingPage from './views/MarketingPage';
import MilitaryTransitionGuide from './views/MilitaryTransitionGuide';
import './App.css';

function App() {
  return (
    <div>
      <Routes>
        <Route index element={<Home />} />
        <Route path="marketing" element={<MarketingPage />} />
        <Route path="transition" element={<MilitaryTransitionGuide />} />
      </Routes>
    </div>
  );
}

export default App;
