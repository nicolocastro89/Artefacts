/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { leafType } from '../../scripts/fieldTypeRules.mjs'

// The same cases the backend checks for query results: both sides must describe a value alike.
const { cases } = JSON.parse(readFileSync('../backend/openapi/field-type-rules.json', 'utf8')) as {
  cases: { graphql: string; openapi: unknown; type: string }[]
}

describe('field type rules shared with the backend', () => {
  it.each(cases)('describes $graphql as $type', ({ openapi, type }) => {
    expect(leafType(openapi)).toBe(type)
  })

  it('describes an enum as text', () => {
    expect(leafType({ type: 'string', enum: ['a', 'b'] })).toBe('text')
  })
})
