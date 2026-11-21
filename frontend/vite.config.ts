/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The API runs on the host (dotnet run, http://localhost:5032, or ARTEFACTS_API_URL as for the seed); the dev
// server proxies /api to it so the browser sees a single origin (no CORS).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': process.env.ARTEFACTS_API_URL ?? 'http://localhost:5032',
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: true,
  },
})
