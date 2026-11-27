import { useId, type ComponentType } from 'react'
import type { UnitOfMeasure } from '../../api/client'
import { MEASURE_KINDS, type ImportResolution, type ImportResolutionItem, type MeasureKind } from './importApi'

/** What an editor needs to know besides the text it edits. */
export interface ResolutionContext {
  units: UnitOfMeasure[]
  defaultUnit: string
}

/**
 * A kind of text an import asks the user to settle, as the generic resolution step needs it: how to start a draft
 * from the saved decision, when a draft counts as decided, how to send it, and the editor for one text. The server
 * lists the texts and stores the decisions; only the editing is specific to the kind.
 */
export interface ResolutionEditor<D = unknown> {
  /** The kind's identifier in the API: <c>/api/imports/{id}/resolutions/{kind}</c>. */
  kind: string
  heading: string
  loadingText: string
  errorText: string
  emptyText: string
  intro: string
  continueLabel: string
  draftOf(item: ImportResolutionItem, ctx: ResolutionContext): D
  isDecided(draft: D): boolean
  /** The decision to send for a draft, or null while it is undecided. */
  toResolution(text: string, draft: D): ImportResolution | null
  summary(undecided: number, total: number): string
  /** A way to settle every undecided text at once. */
  settleRest?: { label(count: number): string; apply(draft: D): D }
  Row: ComponentType<{ item: ImportResolutionItem; draft: D; ctx: ResolutionContext; onChange: (draft: D) => void }>
}

function defineEditor<D>(editor: ResolutionEditor<D>): ResolutionEditor {
  return editor as unknown as ResolutionEditor
}

function RowsOf({ rows }: { rows: number[] }) {
  return (
    <span className="muted">{rows.length === 1 ? `Row ${rows[0]}` : `Rows ${rows.slice(0, 6).join(', ')}${rows.length > 6 ? ` and ${rows.length - 6} more` : ''}`}</span>
  )
}

// ---------- dates ----------

type Range = { earliest: string; latest: string }
type DateRangePayload = { earliestYear?: number | null; latestYear?: number | null }

const dateEditor = defineEditor<Range>({
  kind: 'dates',
  heading: 'Dates not understood',
  loadingText: 'Reading dates…',
  errorText: 'Could not load the dates',
  emptyText: 'Every date in the workbook is understood.',
  intro: 'Give a year range to import these rows with the date text as written. Rows whose date has no range are listed as problems and not imported.',
  continueLabel: 'Save and continue to dimensions',
  draftOf: (item) => {
    const p = (item.resolution?.payload ?? {}) as DateRangePayload
    return { earliest: p.earliestYear?.toString() ?? '', latest: p.latestYear?.toString() ?? '' }
  },
  isDecided: (r) => r.earliest.trim() !== '' && r.latest.trim() !== '',
  toResolution: (text, r) =>
    r.earliest.trim() && r.latest.trim() ? { text, action: 'range', payload: { earliestYear: Number(r.earliest), latestYear: Number(r.latest) } } : null,
  summary: (undecided, total) => (undecided > 0 ? `${undecided} without a range` : total > 0 ? 'Every date has a range.' : ''),
  Row: function DateRow({ item, draft, onChange }) {
    const fromId = useId()
    const toId = useId()
    return (
      <li className="imports__date">
        <div className="imports__place-text">
          <strong>{item.text}</strong>
          <RowsOf rows={item.sheetRows} />
        </div>
        <div className="imports__years">
          <div className="field">
            <label className="field__label" htmlFor={fromId}>
              Earliest year for “{item.text}”
            </label>
            <input id={fromId} className="input" inputMode="numeric" value={draft.earliest} onChange={(e) => onChange({ ...draft, earliest: e.target.value.replace(/\D/g, '') })} />
          </div>
          <div className="field">
            <label className="field__label" htmlFor={toId}>
              Latest year for “{item.text}”
            </label>
            <input id={toId} className="input" inputMode="numeric" value={draft.latest} onChange={(e) => onChange({ ...draft, latest: e.target.value.replace(/\D/g, '') })} />
          </div>
        </div>
      </li>
    )
  },
})

// ---------- dimensions ----------

type Entry = { value: string; unitId: string }

/** A text's decision as edited: measurements by kind, or kept as text only. */
interface DimensionsDraft {
  keepTextOnly: boolean
  measurements: Record<MeasureKind, Entry>
}

type MeasurementPayload = { value?: number | null; unitId?: string | null } | null
type DimensionsPayload = Partial<Record<MeasureKind, MeasurementPayload>>

const amountOf = (text: string) => Number(text.trim().replace(',', '.'))

const dimensionsEditor = defineEditor<DimensionsDraft>({
  kind: 'dimensions',
  heading: 'Dimensions not read',
  loadingText: 'Reading dimensions…',
  errorText: 'Could not load the dimensions',
  emptyText: 'The measurements of every dimensions text could be read.',
  intro:
    'Enter the measurements these texts stand for, or keep a text without measurements. The text is imported as written either way; rows whose text has no decision are listed as problems and not imported.',
  continueLabel: 'Save and continue to review',
  draftOf: (item, { defaultUnit }) => {
    const resolution = item.resolution
    const payload = (resolution?.action === 'measure' ? (resolution.payload ?? {}) : {}) as DimensionsPayload
    const entry = (kind: MeasureKind): Entry => {
      const m = payload[kind]
      return m?.value != null ? { value: String(m.value), unitId: m.unitId ?? defaultUnit } : { value: '', unitId: defaultUnit }
    }
    return {
      keepTextOnly: resolution?.action === 'keepText',
      measurements: { height: entry('height'), width: entry('width'), length: entry('length'), diameter: entry('diameter') },
    }
  },
  isDecided: (d) => d.keepTextOnly || Object.values(d.measurements).some((m) => m.value.trim() !== ''),
  toResolution(text, d) {
    if (d.keepTextOnly) return { text, action: 'keepText' }
    if (!this.isDecided(d)) return null
    const payload: Record<string, { value: number; unitId: string | null } | null> = {}
    for (const { value: kind } of MEASURE_KINDS) {
      const m = d.measurements[kind]
      payload[kind] = m.value.trim() === '' ? null : { value: amountOf(m.value), unitId: m.unitId || null }
    }
    return { text, action: 'measure', payload }
  },
  summary: (undecided, total) => (undecided > 0 ? `${undecided} without a decision` : total > 0 ? 'Every text has a decision.' : ''),
  settleRest: {
    label: (count) => `Keep the remaining ${count === 1 ? 'text' : `${count} texts`} without measurements`,
    apply: (d) => ({ ...d, keepTextOnly: true }),
  },
  Row: function DimensionsRow({ item, draft, ctx, onChange }) {
    const keepId = useId()
    const setEntry = (kind: MeasureKind, patch: Partial<Entry>) =>
      onChange({ ...draft, measurements: { ...draft.measurements, [kind]: { ...draft.measurements[kind], ...patch } } })
    return (
      <li className="imports__date imports__dimension">
        <div className="imports__place-text">
          <strong>{item.text}</strong>
          <RowsOf rows={item.sheetRows} />
          <label className="imports__check" htmlFor={keepId}>
            <input id={keepId} type="checkbox" checked={draft.keepTextOnly} onChange={(e) => onChange({ ...draft, keepTextOnly: e.target.checked })} />
            Keep the text only, without measurements<span className="visually-hidden"> (“{item.text}”)</span>
          </label>
        </div>
        <fieldset className="imports__measures" disabled={draft.keepTextOnly}>
          <legend className="visually-hidden">Measurements for “{item.text}”</legend>
          {MEASURE_KINDS.map(({ value: kind, label, short }) => (
            <MeasureInput
              key={kind}
              label={`${label} for “${item.text}”`}
              caption={`${label} (${short})`}
              entry={draft.measurements[kind]}
              units={ctx.units}
              onChange={(patch) => setEntry(kind, patch)}
            />
          ))}
        </fieldset>
      </li>
    )
  },
})

function MeasureInput({ label, caption, entry, units, onChange }: { label: string; caption: string; entry: Entry; units: UnitOfMeasure[]; onChange: (patch: Partial<Entry>) => void }) {
  const id = useId()
  return (
    <div className="field">
      <label className="field__label" htmlFor={id}>
        <span aria-hidden="true">{caption}</span>
        <span className="visually-hidden">{label}</span>
      </label>
      <div className="imports__measure-inputs">
        <input id={id} className="input" inputMode="decimal" value={entry.value} onChange={(e) => onChange({ value: e.target.value })} />
        <select className="select" aria-label={`Unit of ${label.charAt(0).toLowerCase()}${label.slice(1)}`} value={entry.unitId} onChange={(e) => onChange({ unitId: e.target.value })}>
          <option value="">Unit</option>
          {units.map((u) => (
            <option key={u.id} value={u.id}>
              {u.abbreviation}
            </option>
          ))}
        </select>
      </div>
    </div>
  )
}

/** The editors of the kinds of resolution, by the kind's identifier in the API. */
export const RESOLUTION_EDITORS: Record<'dates' | 'dimensions', ResolutionEditor> = {
  dates: dateEditor,
  dimensions: dimensionsEditor,
}
