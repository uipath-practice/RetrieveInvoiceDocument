import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// base './' is required: the platform mounts the app under a non-root prefix and handles routing.
export default defineConfig({
  plugins: [react()],
  base: './',
})
