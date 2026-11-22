export interface RunError {
  message: string
  line: number | null
  column: number | null
}

export interface RunResult {
  data: unknown
  errors: RunError[]
}

/** Runs a document that is not saved, as the server runs any ad-hoc query, in the reader's language. */
export async function runDocument(document: string, variables: Record<string, unknown>, signal?: AbortSignal): Promise<RunResult> {
  const response = await fetch('/graphql', {
    method: 'POST',
    signal,
    headers: { 'Content-Type': 'application/json', Accept: 'application/graphql-response+json, application/json' },
    body: JSON.stringify({ query: document, variables }),
  })
  type Body = { data?: unknown; errors?: { message: string; locations?: { line: number; column: number }[] }[] }
  let body: Body = {}
  try {
    body = (await response.json()) as Body
  } catch {
    return { data: null, errors: [{ message: `The server answered ${response.status} without a result.`, line: null, column: null }] }
  }
  return {
    data: body.data ?? null,
    errors: (body.errors ?? []).map((e) => ({ message: e.message, line: e.locations?.[0]?.line ?? null, column: e.locations?.[0]?.column ?? null })),
  }
}

/** Where an error is, for a message: ` (line 3, column 5)`, or nothing when it has no position. */
export const position = (e: { line: number | null; column: number | null }) => (e.line !== null ? ` (line ${e.line}, column ${e.column})` : '')
