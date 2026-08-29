/* eslint-disable  @typescript-eslint/no-non-null-assertion */
import { StrictMode } from 'react';
import { createRoot, hydrateRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router';
import Shell from './Shell.tsx';

const root = document.getElementById('root')!;

const app = (
  <StrictMode>
    <BrowserRouter>
      <Shell />
    </BrowserRouter>
  </StrictMode>
);

// Prerendered pages arrive with their markup already in the root, so React
// adopts it instead of rebuilding it; pages that are not prerendered (the
// scheduling app) arrive with an empty root and render as they always have.
if (root.hasChildNodes()) {
  hydrateRoot(root, app);
} else {
  createRoot(root).render(app);
}
