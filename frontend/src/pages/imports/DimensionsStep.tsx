import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight } from 'lucide-react'
import { useId, useState } from 'react'
import { api, type UnitOfMeasure } from '../../api/client'
import { unwrap } from '../../api/problem'
import { useUnits } from '../../api/units'
import { errorMessage, MEASURE_KINDS, type DimensionResolution, type ImportDimensions, type ImportJob, type MeasureKind } from './importApi'

type Entry = { value: string; unitId: string }

/** A text's decision as edited: measurements by kind, or kept as text only. */
interface Draft {
  keepTextOnly: boolean
  measurements: Record<MeasureKind, Entry>
}

const amountOf = (text: string) => Number(text.trim().replace(',', '.'))

function draftOf(resolution: DimensionResolution | null | undefined, unitId: string): Draft {
  const entry = (kind: MeasureKind): Entry => {
    const m = resolution?.[kind]
    return m?.value != null ? { value: String(m.value), unitId: m.unitId ?? unitId } : { value: '', unitId }
  }
  return {
    keepTextOnly: resolution?.keepTextOnly ?? false,
    measurements: { height: entry('height'), width: entry('width'), length: entry('length'), diameter: entry('diameter') },
  }
}

const isDecided = (d: Draft) => d.keepTextOnly || Object.values(d.measurements).some((m) => m.value.trim() !== '')

function resolutionOf(text: string, d: Draft): DimensionResolution | null {
  if (d.keepTextOnly) return { text, keepTextOnly: true }
  if (!isDecided(d)) return null
  const measurement = (kind: MeasureKind) => {
    const m = d.measurements[kind]
    return m.value.trim() === '' ? null : { value: amountOf(m.value), unitId: m.unitId || null }
  }
  return { text, keepTextOnly: false, height: measurement('height'), width: measurement('width'), length: measurement('length'), diameter: measurement('diameter') }
}

/**
 * Dimensions texts whose measurements could not be read clearly, once per distinct text: the measurements entered
 * apply to every row using it, or the text is kept without measurements. The text itself is always imported as
 * written; rows whose text has no decision are held back.
 */
export function DimensionsStep({ job, onDone }: { job: ImportJob; onDone: () => void }) {
  const queryClient = useQueryClient()
  const { data: units = [] } = useUnits()
  const dimensions = useQuery({
    queryKey: ['imports', job.id, 'dimensions'],
    queryFn: async () => unwrap(await api.GET('/api/imports/{id}/dimensions', { params: { path: { id: job.id } } })),
  })
  const [edits, setEdits] = useState<Record<string, Draft>>({})
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const defaultUnit = job.mapping?.dimensions?.defaultUnitId ?? units.find((u) => u.abbreviation === 'cm')?.id ?? ''

  const draftFor = (d: ImportDimensions): Draft => edits[d.text] ?? draftOf(d.resolution, defaultUnit)
  const setDraft = (text: string, draft: Draft) => setEdits((all) => ({ ...all, [text]: draft }))

  const save = async () => {
    setSaving(true)
    setError(null)
    try {
      const resolutions = (dimensions.data ?? []).map((d) => resolutionOf(d.text, draftFor(d))).filter((r): r is DimensionResolution => r !== null)
      unwrap(await api.PUT('/api/imports/{id}/dimensions', { params: { path: { id: job.id } }, body: { dimensions: resolutions } }))
      await queryClient.invalidateQueries({ queryKey: ['imports', job.id] })
      onDone()
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      setSaving(false)
    }
  }

  if (dimensions.isPending)
    return (
      <div className="data-state" aria-busy="true">
        Reading dimensions…
      </div>
    )
  if (dimensions.isError)
    return (
      <div className="data-state data-state--error" role="alert">
        Could not load the dimensions: {dimensions.error.message}
      </div>
    )

  const undecided = dimensions.data.filter((d) => !isDecided(draftFor(d)))
  const keepRemaining = () =>
    setEdits((all) => ({ ...all, ...Object.fromEntries(undecided.map((d) => [d.text, { ...draftFor(d), keepTextOnly: true }])) }))

  return (
    <div className="imports__step-body">
      <section className="plate imports__section" aria-labelledby="dimensions-heading">
        <h2 id="dimensions-heading" className="imports__section-title">
          Dimensions not read
        </h2>
        {dimensions.data.length === 0 ? (
          <p className="muted">The measurements of every dimensions text could be read.</p>
        ) : (
          <>
            <p className="muted imports__section-sub">
              Enter the measurements these texts stand for, or keep a text without measurements. The text is imported as written either way; rows
              whose text has no decision are listed as problems and not imported.
            </p>
            <ul className="imports__dates">
              {dimensions.data.map((d) => (
                <DimensionsRow key={d.text} item={d} draft={draftFor(d)} units={units} onChange={(draft) => setDraft(d.text, draft)} />
              ))}
            </ul>
          </>
        )}
      </section>
      <div className="imports__actions">
        <div className="imports__actions-side">
          <p className="muted" aria-live="polite">
            {undecided.length > 0 ? `${undecided.length} without a decision` : dimensions.data.length > 0 ? 'Every text has a decision.' : ''}
          </p>
          {undecided.length > 0 && (
            <button type="button" className="btn btn--sm" onClick={keepRemaining}>
              Keep the remaining {undecided.length === 1 ? 'text' : `${undecided.length} texts`} without measurements
            </button>
          )}
        </div>
        <div className="imports__actions-main">
          {error && (
            <p className="field__error" role="alert">
              {error}
            </p>
          )}
          <button type="button" className="btn btn--primary" disabled={saving} onClick={() => void save()}>
            Save and continue to review <ArrowRight aria-hidden="true" />
          </button>
        </div>
      </div>
    </div>
  )
}

function DimensionsRow({ item, draft, units, onChange }: { item: ImportDimensions; draft: Draft; units: UnitOfMeasure[]; onChange: (d: Draft) => void }) {
  const keepId = useId()
  const rows = item.sheetRows
  const setEntry = (kind: MeasureKind, patch: Partial<Entry>) =>
    onChange({ ...draft, measurements: { ...draft.measurements, [kind]: { ...draft.measurements[kind], ...patch } } })
  return (
    <li className="imports__date imports__dimension">
      <div className="imports__place-text">
        <strong>{item.text}</strong>
        <span className="muted">
          {rows.length === 1 ? `Row ${rows[0]}` : `Rows ${rows.slice(0, 6).join(', ')}${rows.length > 6 ? ` and ${rows.length - 6} more` : ''}`}
        </span>
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
            units={units}
            onChange={(patch) => setEntry(kind, patch)}
          />
        ))}
      </fieldset>
    </li>
  )
}

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
