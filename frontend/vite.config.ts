/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The API runs on the host (dotnet run, http://localhost:5032, or ARTEFACTS_API_URL as for the seed); the dev
// server proxies /api and /graphql to it so the browser sees a single origin (no CORS). The port can be changed
// with ARTEFACTS_WEB_PORT (for a second copy of the app next to this one).
const api = process.env.ARTEFACTS_API_URL ?? 'http://localhost:5032'

export default defineConfig({
  plugins: [react()],
  server: {
    port: Number(process.env.ARTEFACTS_WEB_PORT ?? 5173),
    proxy: {
      '/api': api,
      '/graphql': api,
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: true,
  },
})
