import { api, type ListSlug, type PartyKind } from '../../../api/client'
import { unwrap } from '../../../api/problem'
import type { RefSource } from './ReferencePicker'

function listSource(list: ListSlug, noun: string, translated: boolean): RefSource {
  return {
    key: `lists-${list}`,
    noun,
    search: async (text) => unwrap(await api.GET('/api/lists/{list}', { params: { path: { list }, query: text ? { search: text } : {} } })),
    add: async (name, language) =>
      unwrap(await api.POST('/api/lists/{list}', { params: { path: { list } }, body: translated ? { names: { [language]: name } } : { name } })),
  }
}

/** Pickers' sources for the managed lists, also used by the import's lists step. */
export const listSources = {
  dynasty: listSource('dynasties', 'dynasty', true),
  collectionGroup: listSource('collection-groups', 'collection group', true),
  storageLocation: listSource('storage-locations', 'storage location', false),
}

export const partySource: RefSource = {
  key: 'parties',
  noun: 'party',
  addKinds: [
    { value: 'organisation', label: 'an organisation' },
    { value: 'person', label: 'a person' },
  ],
  search: async (text) =>
    unwrap(await api.GET('/api/parties', { params: { query: text ? { search: text } : {} } })).map((p) => ({
      id: p.id,
      name: p.name,
      secondary: p.kind === 'person' ? 'Person' : 'Organisation',
    })),
  add: async (name, _language, kind) =>
    unwrap(await api.POST('/api/parties', { body: { name, kind: (kind as PartyKind | undefined) ?? 'organisation' } })),
}
