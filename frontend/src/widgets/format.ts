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

interface MeasurementLike {
  value: number
  inMetres?: number
  unit?: { abbreviation?: string } | null
}

/** The four measurements, in the order they are shown, with their short labels. */
export const MEASUREMENT_LABELS = { height: 'H', width: 'W', length: 'L', diameter: 'Ø' } as const
export type MeasurementName = keyof typeof MEASUREMENT_LABELS

export function isMeasurement(v: unknown): v is MeasurementLike {
  return typeof v === 'object' && v !== null && typeof (v as MeasurementLike).value === 'number' && 'unit' in v
}

/** "17.6 cm" — the value in the unit it was entered in. */
export function formatMeasurement(m: MeasurementLike): string {
  const value = new Intl.NumberFormat(undefined, { maximumFractionDigits: 4 }).format(m.value)
  return m.unit?.abbreviation ? `${value} ${m.unit.abbreviation}` : value
}

/** "H 7 cm · Ø 17.6 cm" — the measurements that exist, each with its short label; '' when there are none. */
export function formatMeasurements(measurements: Partial<Record<MeasurementName, unknown>>): string {
  return (Object.keys(MEASUREMENT_LABELS) as MeasurementName[])
    .filter((name) => isMeasurement(measurements[name]))
    .map((name) => `${MEASUREMENT_LABELS[name]} ${formatMeasurement(measurements[name] as MeasurementLike)}`)
    .join(' · ')
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
  PURCHASE: 'Purchase',
  GIFT: 'Gift',
  BEQUEST: 'Bequest',
  TRANSFER: 'Transfer',
  OTHER: 'Other',
  PERSON: 'Person',
  ORGANISATION: 'Organisation',
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
  if (type === 'measurement' || isMeasurement(value)) return isMeasurement(value) ? formatMeasurement(value) : String(value)
  if (type === 'date' && typeof value === 'string') return formatDate(value)
  if (type === 'boolean' || typeof value === 'boolean') return value ? 'Yes' : 'No'
  if (type === 'number' && typeof value === 'number') return new Intl.NumberFormat().format(value)
  if (typeof value === 'string') return ENUM_LABELS[value] ?? value
  return String(value)
}
