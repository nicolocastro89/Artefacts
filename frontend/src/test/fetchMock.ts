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

/** The import fields route, as the server's field registry lists them. */
export const importFieldsRoute = {
  'GET /api/imports/fields': () =>
    (
      [
        ['title', 'Title', false, false],
        ['itemNumber', 'Item number', true, false],
        ['groupNumber', 'Group number', true, false],
        ['catalogueNumber', 'Catalogue number', true, false],
        ['inventoryNumber', 'Inventory number', true, false],
        ['dimensions', 'Dimensions', false, false],
        ['medium', 'Medium', false, false],
        ['maker', 'Maker', false, false],
        ['description', 'Description', false, false],
        ['placeOfProduction', 'Place of production', false, false],
        ['creationDate', 'Date', true, false],
        ['dynasty', 'Dynasty', false, true],
        ['acquisitionSource', 'Acquired from', false, true],
        ['acquisitionDate', 'Acquisition date', true, false],
        ['acquisitionLot', 'Lot', true, false],
        ['acquisitionPrice', 'Price', true, false],
        ['acquisitionNote', 'Acquisition note', false, false],
        ['exhibitions', 'Exhibitions', false, false],
        ['catalogueEntry', 'Catalogue entry', false, false],
        ['holder', 'Holder', false, true],
        ['storageLocation', 'Storage location', false, true],
        ['valuation', 'Valuation', true, false],
        ['collectionGroup', 'Collection group', false, true],
      ] as const
    ).map(([id, label, singleValued, curated]) => ({ id, label, singleValued, curated })),
}

/** The import detectors route, as the server's detector registry lists them. */
export const importDetectorsRoute = {
  'GET /api/imports/detectors': () => [
    { id: 'itemTitle', label: 'Numbered title', example: '01. Coppa Celadon' },
    { id: 'groupNumber', label: 'Group number', example: '01.' },
    { id: 'dimensions', label: 'Dimensions', example: 'diam 17,6 x h 7' },
    { id: 'historicalDate', label: 'Historical date', example: 'fine XII inizio XIII' },
    { id: 'calendarDate', label: 'Calendar date', example: '12 06 1990' },
    { id: 'lot', label: 'Auction lot', example: 'lot 170' },
    { id: 'money', label: 'Price with currency', example: '£ 7.865' },
    { id: 'catalogueNumber', label: 'Catalogue number', example: 'W.389' },
    { id: 'locationCode', label: 'Location code', example: 'MI F3' },
  ],
}

export function problem(status: number, body: object): Response {
  return new Response(JSON.stringify({ status, ...body }), { status, headers: { 'Content-Type': 'application/problem+json' } })
}
