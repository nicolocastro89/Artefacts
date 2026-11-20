/** RFC 9457 problem details as returned by the API. */
export interface Problem {
  status?: number
  title?: string
  detail?: string
  code?: string
  errors?: Record<string, string[]>
}

/** Thrown by API helpers for non-success responses so callers (and TanStack Query) can handle them uniformly. */
export class ApiError extends Error {
  readonly problem: Problem
  readonly status: number

  constructor(status: number, problem: Problem) {
    super(problem.detail ?? problem.title ?? `Request failed (${status})`)
    this.status = status
    this.problem = problem
  }

  /** Validation errors keyed by camelCase field path, e.g. `inventoryNumber`, `provenance[0].regionId`. */
  get fieldErrors(): Record<string, string[]> {
    return this.problem.errors ?? {}
  }
}

/**
 * Maps server validation errors onto form fields.
 * Returns, for each known field, its messages (including nested paths under that field, e.g.
 * `acquisition.price.currency` is reported for `acquisition.price.currency` and its parents' consumers can ask for it directly),
 * plus `unmatched` messages that belong to no known field so they can be shown in a form-level summary.
 */
export function mapFieldErrors(
  errors: Record<string, string[]>,
  knownFields: readonly string[],
): { byField: Record<string, string[]>; unmatched: string[] } {
  const byField: Record<string, string[]> = {}
  const unmatched: string[] = []
  for (const [key, messages] of Object.entries(errors)) {
    const field = knownFields.includes(key) ? key : knownFields.find((f) => key.startsWith(`${f}.`) || key.startsWith(`${f}[`))
    if (field) byField[field] = [...(byField[field] ?? []), ...messages]
    else unmatched.push(...messages.map((m) => (key ? `${key}: ${m}` : m)))
  }
  return { byField, unmatched }
}

/** Unwraps an openapi-fetch result, throwing {@link ApiError} for problem responses. */
export function unwrap<T>(result: { data?: T; error?: unknown; response: Response }): T {
  if (result.error !== undefined || !result.response.ok) {
    throw new ApiError(result.response.status, (result.error ?? {}) as Problem)
  }
  return result.data as T
}
