import { useQuery } from '@tanstack/react-query'
import type { PageDefinition, PageNode } from '../widgets/core/page'
import { api, type Schemas } from './client'
import { unwrap } from './problem'

export type StoredPage = Schemas['StoredPageDto']
export type StoredPageSummary = Schemas['StoredPageSummary']

export const pageKeys = {
  all: ['pages'] as const,
  slug: (slug: string) => ['pages', 'slug', slug] as const,
  navigation: ['pages', 'navigation'] as const,
}

/** The page definition a stored page holds, under the stored page's identifier and name. */
export function toDefinition(page: StoredPage): PageDefinition {
  const { root } = page.definition as unknown as { root: PageNode }
  return { id: page.id, title: page.name, root }
}

/** A stored page by the slug in its address. */
export function usePage(slug: string) {
  return useQuery({
    queryKey: pageKeys.slug(slug),
    queryFn: async ({ signal }) => unwrap(await api.GET('/api/pages/by-slug/{slug}', { params: { path: { slug } }, signal })),
  })
}

/** The pages shown in the sidebar. */
export function useNavigationPages() {
  return useQuery({
    queryKey: pageKeys.navigation,
    queryFn: async ({ signal }) => unwrap(await api.GET('/api/pages', { params: { query: { navigation: true } }, signal })),
    staleTime: 30_000,
  })
}

/** Every stored page, system pages first. */
export function usePageList() {
  return useQuery({
    queryKey: [...pageKeys.all, 'list'],
    queryFn: async ({ signal }) => unwrap(await api.GET('/api/pages', { signal })),
  })
}

export function usePageById(id: string) {
  return useQuery({
    queryKey: [...pageKeys.all, 'id', id],
    queryFn: async ({ signal }) => unwrap(await api.GET('/api/pages/{id}', { params: { path: { id } }, signal })),
  })
}

export type CreatePageRequest = Schemas['CreateStoredPageRequest']
export type ChangePageRequest = Schemas['ChangeStoredPageRequest']

export async function createPage(body: CreatePageRequest) {
  return unwrap(await api.POST('/api/pages', { body }))
}

export async function updatePage(id: string, body: ChangePageRequest) {
  return unwrap(await api.PUT('/api/pages/{id}', { params: { path: { id } }, body }))
}

export async function deletePage(id: string) {
  const result = await api.DELETE('/api/pages/{id}', { params: { path: { id } } })
  if (result.error !== undefined) unwrap(result)
}

export async function duplicatePage(id: string, body: { name: string; slug: string }) {
  return unwrap(await api.POST('/api/pages/{id}/duplicate', { params: { path: { id } }, body }))
}

/** The address a page is shown at: the system pages keep their own routes, custom pages live under `/p/`. */
const systemAddresses: Record<string, string> = { 'artefact-list': '/artefacts', 'artefact-detail': '/artefacts/:id' }

export function addressOf(page: Pick<StoredPageSummary, 'id' | 'slug' | 'kind'>): string {
  return (page.kind === 'system' ? systemAddresses[page.id] : undefined) ?? `/p/${page.slug}`
}
