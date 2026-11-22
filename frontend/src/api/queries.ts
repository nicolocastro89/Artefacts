import { useQuery } from '@tanstack/react-query'
import { api, type Schemas } from './client'
import { unwrap } from './problem'

export type StoredQuery = Schemas['StoredQueryDto']
export type StoredQuerySummary = Schemas['StoredQuerySummary']
export type QueryDescription = Schemas['QueryDescriptionDto']
export type QueryProblem = Schemas['QueryProblemDto']
export type DocumentDescription = Schemas['DocumentDescriptionDto']

export const queryKeys = { all: ['queries'] as const }

/** Every stored query, system queries first. */
export function useQueryList() {
  return useQuery({
    queryKey: [...queryKeys.all, 'list'],
    queryFn: async ({ signal }) => unwrap(await api.GET('/api/queries', { signal })),
  })
}

export function useStoredQuery(id: string) {
  return useQuery({
    queryKey: [...queryKeys.all, 'id', id],
    queryFn: async ({ signal }) => unwrap(await api.GET('/api/queries/{id}', { params: { path: { id } }, signal })),
  })
}

export async function createQuery(body: { name: string; document: string }) {
  return unwrap(await api.POST('/api/queries', { body }))
}

export async function updateQuery(id: string, body: { name?: string; document?: string }) {
  return unwrap(await api.PUT('/api/queries/{id}', { params: { path: { id } }, body }))
}

export async function deleteQuery(id: string) {
  const result = await api.DELETE('/api/queries/{id}', { params: { path: { id } } })
  if (result.error !== undefined) unwrap(result)
}

export async function duplicateQuery(id: string, name: string) {
  return unwrap(await api.POST('/api/queries/{id}/duplicate', { params: { path: { id } }, body: { name } }))
}

/** Describes a document that is not saved: what it returns and takes, or where it is wrong. */
export async function describeDocument(document: string, signal?: AbortSignal) {
  return unwrap(await api.POST('/api/queries/description', { body: { document }, signal }))
}
