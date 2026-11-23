import { vi } from 'vitest'

type Handler = (url: URL, init?: RequestInit) => unknown | Response | Promise<unknown | Response>

/**
 * Replaces global fetch with routes keyed by "METHOD /path" (path without query).
 * Handlers return a JSON-able value or a Response. Unmatched requests fail the test loudly.
 */
export function mockFetch(routes: Record<string, Handler>) {
  const calls: { method: string; url: URL; body?: unknown }[] = []
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = input instanceof Request ? input : undefined
    const url = new URL(request ? request.url : String(input), 'http://localhost')
    const method = (init?.method ?? request?.method ?? 'GET').toUpperCase()
    let body: unknown
    const rawBody = init?.body ?? (request && method !== 'GET' ? await request.clone().text() : undefined)
    if (typeof rawBody === 'string') {
      try {
        body = JSON.parse(rawBody)
      } catch {
        body = rawBody
      }
    } else body = rawBody
    calls.push({ method, url, body })
    const handler = routes[`${method} ${url.pathname}`]
    if (!handler) throw new Error(`Unmocked request: ${method} ${url.pathname}${url.search}`)
    const result = await handler(url, init)
    if (result instanceof Response) return result
    return new Response(JSON.stringify(result), { status: 200, headers: { 'Content-Type': 'application/json' } })
  })
  vi.stubGlobal('fetch', fn)
  return { fn, calls }
}

/** The content languages route, as the API returns it initially (English first in fallback order). */
export const languagesRoute = {
  'GET /api/languages': () => [
    { code: 'en', name: 'English', position: 1 },
    { code: 'it', name: 'Italian', position: 2 },
  ],
}

/** The seeded units of measure, smallest first, as the API lists them. */
export const unitsRoute = {
  'GET /api/units': () => [
    { id: 'u-mm', name: 'Millimetre', nameLanguage: 'en', names: { en: 'Millimetre', it: 'millimetro' }, abbreviation: 'mm', factor: 0.001, artefactCount: 0 },
    { id: 'u-cm', name: 'Centimetre', nameLanguage: 'en', names: { en: 'Centimetre', it: 'centimetro' }, abbreviation: 'cm', factor: 0.01, artefactCount: 0 },
    { id: 'u-in', name: 'Inch', nameLanguage: 'en', names: { en: 'Inch', it: 'pollice' }, abbreviation: 'in', factor: 0.0254, artefactCount: 0 },
    { id: 'u-m', name: 'Metre', nameLanguage: 'en', names: { en: 'Metre', it: 'metro' }, abbreviation: 'm', factor: 1, artefactCount: 0 },
  ],
}

export function problem(status: number, body: object): Response {
  return new Response(JSON.stringify({ status, ...body }), { status, headers: { 'Content-Type': 'application/problem+json' } })
}
