/* eslint-disable  @typescript-eslint/no-non-null-assertion */
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router';
import { StyledEngineProvider } from '@mui/material/styles';
import './index.css';
import App from './App.tsx';

//ignore non-null assertion

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <StyledEngineProvider injectFirst>
        <App />
      </StyledEngineProvider>
    </BrowserRouter>
  </StrictMode>
);
