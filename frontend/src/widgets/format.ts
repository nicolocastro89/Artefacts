import type { FieldType } from './core/types'

interface HistoricalDateLike {
  display: string
  earliestYear?: number | null
  latestYear?: number | null
}

interface MoneyLike {
  amount: number
  currency: string
}

export function isHistoricalDate(v: unknown): v is HistoricalDateLike {
  return typeof v === 'object' && v !== null && typeof (v as HistoricalDateLike).display === 'string'
}

export function isMoney(v: unknown): v is MoneyLike {
  return typeof v === 'object' && v !== null && typeof (v as MoneyLike).currency === 'string'
}

/** "1667–1700", "–1719", "1801–" — the interpreted range of a historical date. */
export function formatYearRange(earliest?: number | null, latest?: number | null): string {
  if (earliest == null && latest == null) return ''
  if (earliest === latest) return String(earliest)
  return `${earliest ?? ''}–${latest ?? ''}`
}

export function formatMoney(m: MoneyLike): string {
  try {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency: m.currency }).format(m.amount)
  } catch {
    return `${m.amount} ${m.currency}`
  }
}

export function formatDate(value: string): string {
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return value
  const dateOnly = /^\d{4}-\d{2}-\d{2}$/.test(value)
  return new Intl.DateTimeFormat(undefined, dateOnly ? { dateStyle: 'medium', timeZone: 'UTC' } : { dateStyle: 'medium', timeStyle: 'short' }).format(d)
}

const ENUM_LABELS: Record<string, string> = {
  purchase: 'Purchase',
  gift: 'Gift',
  bequest: 'Bequest',
  transfer: 'Transfer',
  other: 'Other',
}

/** Formats any scalar value for display according to its field type. Returns '' for empty values. */
export function formatValue(value: unknown, type?: FieldType): string {
  if (value === null || value === undefined || value === '') return ''
  if (type === 'historical-date' || isHistoricalDate(value)) return isHistoricalDate(value) ? value.display : String(value)
  if (type === 'money' || isMoney(value)) return isMoney(value) ? formatMoney(value) : String(value)
  if (type === 'date' && typeof value === 'string') return formatDate(value)
  if (type === 'boolean' || typeof value === 'boolean') return value ? 'Yes' : 'No'
  if (type === 'number' && typeof value === 'number') return new Intl.NumberFormat().format(value)
  if (typeof value === 'string') return ENUM_LABELS[value] ?? value
  return String(value)
}
