import type { FieldType } from './types'

/**
 * Binding paths use a tiny dot syntax: `a.b.c`. A segment ending in `[]` marks a list (`provenance[]`).
 * The empty path addresses the scope's own value.
 * Inside a list container, paths resolve against the current item, and type lookups are prefixed by the
 * list path (`provenance[]` + `period` → `provenance[].period`).
 */

/** Resolves a path against data. A path ending in `[]` must resolve to an array. Missing values are `undefined`. */
export function resolvePath(data: unknown, path: string): unknown {
  if (path === '') return data
  let current: unknown = data
  for (const raw of path.split('.')) {
    const segment = raw.endsWith('[]') ? raw.slice(0, -2) : raw
    if (current === null || current === undefined || typeof current !== 'object') return undefined
    current = segment === '' ? current : (current as Record<string, unknown>)[segment]
  }
  return current
}

/** Joins a scope prefix and a relative path into the key used by field-type maps. */
export function qualify(prefix: string, path: string): string {
  if (!prefix) return path
  if (!path) return prefix
  return `${prefix}.${path}`
}

/** Looks up the declared field type of `path` within a scope. */
export function typeAt(fieldTypes: Readonly<Record<string, FieldType>> | undefined, prefix: string, path: string): FieldType | undefined {
  return fieldTypes?.[qualify(prefix, path)]
}

export function isListPath(path: string): boolean {
  return path.endsWith('[]')
}
