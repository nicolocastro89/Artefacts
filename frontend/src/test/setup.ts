import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// Browsers resolve relative request URLs against the page; Node's Request (used by openapi-fetch
// under jsdom) rejects them. Resolve against the jsdom location so `api.*` calls behave as in the app.
const NodeRequest = globalThis.Request
globalThis.Request = class extends NodeRequest {
  constructor(input: RequestInfo | URL, init?: RequestInit) {
    super(typeof input === 'string' ? new URL(input, window.location.href) : input, init)
  }
} as typeof Request

afterEach(() => cleanup())
