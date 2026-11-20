import { operations, responseFieldTypes, type OperationId } from '../../api/fieldTypes.generated'
import { ApiError, type Problem } from '../../api/problem'
import type { ParamValue } from './page'
import type { FieldType } from './types'

export interface ParamContext {
  route: Readonly<Record<string, string | undefined>>
  search: URLSearchParams
}

export function resolveParams(params: Record<string, ParamValue> | undefined, ctx: ParamContext): Record<string, string> {
  const out: Record<string, string> = {}
  for (const [name, value] of Object.entries(params ?? {})) {
    let resolved: string | undefined
    if (typeof value === 'object') {
      resolved = value.from === 'route' ? ctx.route[value.name] : (ctx.search.get(value.name) ?? value.default?.toString())
    } else {
      resolved = String(value)
    }
    if (resolved !== undefined && resolved !== '') out[name] = resolved
  }
  return out
}

/** Builds the request URL for an operation: route-template params fill `{name}`, the rest become the query string. */
export function buildUrl(operationId: OperationId, params: Record<string, string>): string {
  const op = operations[operationId]
  const query = new URLSearchParams()
  let path: string = op.path
  for (const [name, value] of Object.entries(params)) {
    if (path.includes(`{${name}}`)) path = path.replace(`{${name}}`, encodeURIComponent(value))
    else query.set(name, value)
  }
  if (/\{[^}]+\}/.test(path)) throw new Error(`Missing route parameter for ${operationId}: ${path}`)
  const qs = query.toString()
  return qs ? `${path}?${qs}` : path
}

export async function fetchOperation(operationId: OperationId, params: Record<string, string>, signal?: AbortSignal): Promise<unknown> {
  const op = operations[operationId]
  if (op.method !== 'GET') throw new Error(`Data sources only read: ${operationId} is ${op.method}.`)
  const response = await fetch(buildUrl(operationId, params), { signal, headers: { Accept: 'application/json' } })
  if (!response.ok) {
    let problem: Problem = {}
    try {
      problem = (await response.json()) as Problem
    } catch {
      /* non-JSON error body */
    }
    throw new ApiError(response.status, problem)
  }
  return response.json()
}

export function fieldTypesFor(operationId: OperationId | undefined): Readonly<Record<string, FieldType>> | undefined {
  return operationId ? (responseFieldTypes[operationId] as Readonly<Record<string, FieldType>>) : undefined
}
