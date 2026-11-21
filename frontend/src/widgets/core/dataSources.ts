import { operations, responseFieldTypes, type OperationId } from '../../api/fieldTypes.generated'
import { ApiError, type Problem } from '../../api/problem'
import type { ParamValue } from './page'
import type { FieldType } from './types'

export interface ParamContext {
  route: Readonly<Record<string, string | undefined>>
  search: URLSearchParams
}

/** `objectType` → `OBJECT_TYPE`: how GraphQL enums name a value that the URL writes in camel case. */
export function enumName(value: string): string {
  return value.replace(/([a-z0-9])([A-Z])/g, '$1_$2').toUpperCase()
}

export function resolveParams(params: Record<string, ParamValue> | undefined, ctx: ParamContext): Record<string, string> {
  const out: Record<string, string> = {}
  for (const [name, value] of Object.entries(params ?? {})) {
    let resolved: string | undefined
    if (typeof value === 'object') {
      resolved = value.from === 'route' ? ctx.route[value.name] : (ctx.search.get(value.name) ?? value.default?.toString())
      if (resolved && value.from === 'search' && value.enum) resolved = enumName(resolved)
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

/** What a stored query returns and takes, as the API describes it (`GET /api/queries/{id}/description`). */
export interface QueryDescription {
  fields: Readonly<Record<string, FieldType>>
  variables: Readonly<Record<string, { type: FieldType; required: boolean }>>
}

export async function fetchQueryDescription(queryId: string, signal?: AbortSignal): Promise<QueryDescription> {
  const response = await fetch(`/api/queries/${encodeURIComponent(queryId)}/description`, { signal, headers: { Accept: 'application/json' } })
  if (!response.ok) throw await problemOf(response)
  return (await response.json()) as QueryDescription
}

/**
 * Converts resolved variable texts to the types the query declares: numbers parsed, booleans from `true` / `false`.
 * A text that does not read as its type is sent as it is, so the server reports the variable it does not fit.
 */
export function coerceVariables(values: Record<string, string>, declared: QueryDescription['variables']): Record<string, unknown> {
  const out: Record<string, unknown> = {}
  for (const [name, text] of Object.entries(values)) {
    switch (declared[name]?.type) {
      case 'number':
        out[name] = text.trim() !== '' && Number.isFinite(Number(text)) ? Number(text) : text
        break
      case 'boolean':
        out[name] = text === 'true' ? true : text === 'false' ? false : text
        break
      default:
        out[name] = text
    }
  }
  return out
}

/** A GraphQL response with errors: its messages, as one error. */
export class QueryError extends Error {
  messages: string[]

  constructor(messages: string[]) {
    super(messages.join(' '))
    this.name = 'QueryError'
    this.messages = messages
  }
}

/** Runs a stored query by its identifier and returns the `data` of the response; GraphQL errors are thrown. */
export async function runStoredQuery(queryId: string, variables: Record<string, unknown>, signal?: AbortSignal): Promise<unknown> {
  // The reader's language travels as the browser's Accept-Language, as for the REST reads.
  const response = await fetch('/graphql', {
    method: 'POST',
    signal,
    headers: { 'Content-Type': 'application/json', Accept: 'application/graphql-response+json, application/json' },
    body: JSON.stringify({ id: queryId, variables }),
  })
  let body: { data?: unknown; errors?: { message: string }[] } = {}
  try {
    body = (await response.json()) as typeof body
  } catch {
    if (!response.ok) throw new ApiError(response.status, {})
  }
  if (body.errors?.length) throw new QueryError(body.errors.map((e) => e.message))
  if (!response.ok) throw new ApiError(response.status, {})
  return body.data
}

async function problemOf(response: Response): Promise<ApiError> {
  let problem: Problem = {}
  try {
    problem = (await response.json()) as Problem
  } catch {
    /* non-JSON error body */
  }
  return new ApiError(response.status, problem)
}

export function fieldTypesFor(operationId: OperationId | undefined): Readonly<Record<string, FieldType>> | undefined {
  return operationId ? (responseFieldTypes[operationId] as Readonly<Record<string, FieldType>>) : undefined
}
