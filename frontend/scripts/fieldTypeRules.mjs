// The value types of REST responses: shared by generate-field-types.mjs and its test, and the counterpart of the
// backend's FieldTypeRules for query results (the cases are in ../backend/openapi/field-type-rules.json).

/** OpenAPI object schemas that are one value of their own. */
export const SPECIAL = { ImageUrls: 'image', HistoricalDateDto: 'historical-date', MoneyDto: 'money' }

/** The type of a leaf schema (a primitive, or one of the special objects), or undefined when it is neither. */
export function leafType(schema) {
  const name = schema?.$ref?.split('/').pop()
  if (name && SPECIAL[name]) return SPECIAL[name]
  if (schema?.enum) return 'text'
  switch (schema?.type) {
    case 'string':
      if (schema.format === 'uuid') return 'reference'
      if (schema.format === 'date' || schema.format === 'date-time') return 'date'
      return 'text'
    case 'integer':
    case 'number':
      return 'number'
    case 'boolean':
      return 'boolean'
  }
  return undefined
}
