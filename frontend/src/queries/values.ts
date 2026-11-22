import type { Scalar } from './model'

const NUMBER_TYPES = new Set(['Int', 'Long', 'Short', 'Byte', 'Float', 'Decimal'])
export const isNumberType = (type: string) => NUMBER_TYPES.has(type)

/** What a new condition or argument starts with: a value of the right type, so the document stays valid while it is edited. */
export function defaultScalar(type: string): Scalar {
  if (isNumberType(type)) return 0
  if (type === 'Boolean') return false
  return ''
}

/** Reads the text of an input as a value of the GraphQL type; null when it does not read as one. */
export function parseScalar(text: string, type: string): Scalar | null {
  if (isNumberType(type)) {
    const number = Number(text)
    return text.trim() !== '' && Number.isFinite(number) ? number : null
  }
  if (type === 'Boolean') return text === 'true' ? true : text === 'false' ? false : null
  return text
}
