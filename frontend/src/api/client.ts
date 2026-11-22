import createClient from 'openapi-fetch'
import type { components, paths } from './schema'

/** Typed API client. Requests are same-origin (`/api/...`); the Vite dev server proxies them to the backend. */
// `fetch` is looked up per request (openapi-fetch would otherwise capture it at import), so tests can stub it.
export const api = createClient<paths>({ baseUrl: '', fetch: (request) => globalThis.fetch(request) })

export type Schemas = components['schemas']
export type ArtefactDetail = Schemas['ArtefactDetail']
export type ArtefactWriteRequest = Schemas['ArtefactWriteRequest']
export type ArtefactImage = Schemas['ArtefactImageDto']
export type ImageUrls = Schemas['ImageUrls']
export type HistoricalDate = Schemas['HistoricalDateDto']
export type HistoricalDateInput = Schemas['HistoricalDateInput']
export type Region = Schemas['RegionDto']
export type ObjectType = Schemas['ObjectTypeDto']
export type Language = Schemas['LanguageDto']
export type AcquisitionMethod = Schemas['AcquisitionMethod']
export type ReferenceEntry = Schemas['ReferenceEntryDto']
export type ListKind = Schemas['ListKind']
export type Party = Schemas['PartyDto']
export type PartyKind = Schemas['PartyKind']
export type PartyRef = Schemas['PartyRefDto']
export type NamedRef = Schemas['NamedRefDto']

/** The URL segment of each managed list (`/api/lists/{list}`). */
export type ListSlug = 'dynasties' | 'collection-groups' | 'storage-locations'
