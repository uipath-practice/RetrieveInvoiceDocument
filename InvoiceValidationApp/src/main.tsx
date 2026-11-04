import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'

import { pdfjs } from 'react-pdf'
import pdfWorkerUrl from '@uipath/ui-widgets-pdf-viewer/pdf.worker.min.mjs?url'

// The widget's own worker reference is lost in the Vite bundle (and in dev pre-bundling); point pdf.js at the packaged worker explicitly.
pdfjs.GlobalWorkerOptions.workerSrc = pdfWorkerUrl

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
