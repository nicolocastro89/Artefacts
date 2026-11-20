import createClient from 'openapi-fetch'
import type { components, paths } from './schema'

/** Typed API client. Requests are same-origin (`/api/...`); the Vite dev server proxies them to the backend. */
// `fetch` is looked up per request (openapi-fetch would otherwise capture it at import), so tests can stub it.
export const api = createClient<paths>({ baseUrl: '', fetch: (request) => globalThis.fetch(request) })

export type Schemas = components['schemas']
export type ArtefactDetail = Schemas['ArtefactDetail']
export type ArtefactListItem = Schemas['ArtefactListItem']
export type ArtefactWriteRequest = Schemas['ArtefactWriteRequest']
export type ArtefactImage = Schemas['ArtefactImageDto']
export type ImageUrls = Schemas['ImageUrls']
export type HistoricalDate = Schemas['HistoricalDateDto']
export type HistoricalDateInput = Schemas['HistoricalDateInput']
export type Region = Schemas['RegionDto']
export type ArtefactSort = Schemas['ArtefactSort']
export type SortDirection = Schemas['SortDirection']
export type AcquisitionMethod = Schemas['AcquisitionMethod']
