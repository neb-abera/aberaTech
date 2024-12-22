import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from "react-router";
import { StyledEngineProvider } from '@mui/material/styles';
import './index.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <BrowserRouter>
            <StyledEngineProvider injectFirst>
                <App />
            </StyledEngineProvider>
        </BrowserRouter>
  </StrictMode>,
)
