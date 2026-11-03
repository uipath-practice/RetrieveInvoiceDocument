import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'

// Dev only: Vite's pre-bundling breaks the PDF viewer's packaged worker URL; point pdf.js at the same worker file explicitly.
if (import.meta.env.DEV) import('./devWorker')

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
