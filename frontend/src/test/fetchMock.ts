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

export function problem(status: number, body: object): Response {
  return new Response(JSON.stringify({ status, ...body }), { status, headers: { 'Content-Type': 'application/problem+json' } })
}
