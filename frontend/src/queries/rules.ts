import { useQuery } from '@tanstack/react-query'
import { api, type Schemas } from '../api/client'
import { unwrap } from '../api/problem'

/** What the server says a query builder may offer (`GET /api/queries/builder-schema`). */
export type BuilderRules = Schemas['BuilderRulesDto']
export type BuilderRoot = Schemas['BuilderRootDto']
export type BuilderArgument = Schemas['BuilderArgumentDto']
export type BuilderField = Schemas['BuilderFieldDto']
export type BuilderFilterField = Schemas['BuilderFilterFieldDto']

export function useBuilderRules() {
  return useQuery({
    queryKey: ['query-builder-rules'],
    queryFn: async ({ signal }) => unwrap(await api.GET('/api/queries/builder-schema', { signal })),
    staleTime: 10 * 60 * 1000,
  })
}

export const rootOf = (rules: BuilderRules, name: string) => rules.roots.find((r) => r.name === name)
export const fieldsOf = (rules: BuilderRules, type: string) => rules.types[type]?.fields ?? []
