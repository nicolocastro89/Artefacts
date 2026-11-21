import type { ListSlug, PartyKind } from '../api/client'
import { ApiError } from '../api/problem'

/** What a managed list page shows: its title, its noun, and whether entries have a name per language. */
export interface ListConfig {
  slug: ListSlug
  title: string
  noun: string
  /** Names per content language (dynasties, collection groups); otherwise one name (storage locations). */
  translated: boolean
  description: string
  placeholder: Record<string, string>
}

export const listConfigs: Record<ListSlug, ListConfig> = {
  dynasties: {
    slug: 'dynasties',
    title: 'Dynasties',
    noun: 'dynasty',
    translated: true,
    description: 'Dynasties and periods, named in each language. Renaming one updates every artefact of that dynasty.',
    placeholder: { en: 'e.g. Safavid', it: 'es. Safavide' },
  },
  'collection-groups': {
    slug: 'collection-groups',
    title: 'Collection groups',
    noun: 'collection group',
    translated: true,
    description: 'The groups the collection files artefacts under, named in each language.',
    placeholder: { en: 'e.g. China', it: 'es. CINA' },
  },
  'storage-locations': {
    slug: 'storage-locations',
    title: 'Storage locations',
    noun: 'storage location',
    translated: false,
    description: 'Where artefacts are kept. Locations are codes, so each has a single name.',
    placeholder: { '': 'e.g. MI F3' },
  },
}

export function message(e: unknown): string {
  if (e instanceof ApiError) return Object.values(e.fieldErrors).flat()[0] ?? e.message
  return e instanceof Error ? e.message : 'Something went wrong.'
}

export const partyKindLabels: Record<PartyKind, string> = { organisation: 'Organisation', person: 'Person' }
