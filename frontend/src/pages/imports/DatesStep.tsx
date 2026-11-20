import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight } from 'lucide-react'
import { useId, useState } from 'react'
import { api } from '../../api/client'
import { unwrap } from '../../api/problem'
import { errorMessage, type ImportDate, type ImportJob } from './importApi'

type Range = { earliest: string; latest: string }

/**
 * Date texts the historical date rules do not understand, once per distinct text: a year range applies to every row
 * using it. Dates left without a range keep their rows out of the import, to be corrected in the workbook.
 */
export function DatesStep({ job, onDone }: { job: ImportJob; onDone: () => void }) {
  const queryClient = useQueryClient()
  const dates = useQuery({
    queryKey: ['imports', job.id, 'dates'],
    queryFn: async () => unwrap(await api.GET('/api/imports/{id}/dates', { params: { path: { id: job.id } } })),
  })
  // Edits on top of the saved resolutions.
  const [edits, setEdits] = useState<Record<string, Range>>({})
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const rangeOf = (d: ImportDate): Range =>
    edits[d.text] ?? { earliest: d.resolution?.earliestYear?.toString() ?? '', latest: d.resolution?.latestYear?.toString() ?? '' }

  const save = async () => {
    setSaving(true)
    setError(null)
    try {
      const resolutions = (dates.data ?? [])
        .map((d) => ({ text: d.text, range: rangeOf(d) }))
        .filter(({ range }) => range.earliest.trim() && range.latest.trim())
        .map(({ text, range }) => ({ text, earliestYear: Number(range.earliest), latestYear: Number(range.latest) }))
      unwrap(await api.PUT('/api/imports/{id}/dates', { params: { path: { id: job.id } }, body: { dates: resolutions } }))
      await queryClient.invalidateQueries({ queryKey: ['imports', job.id] })
      onDone()
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      setSaving(false)
    }
  }

  if (dates.isPending)
    return (
      <div className="data-state" aria-busy="true">
        Reading dates…
      </div>
    )
  if (dates.isError)
    return (
      <div className="data-state data-state--error" role="alert">
        Could not load the dates: {dates.error.message}
      </div>
    )

  const open = dates.data.filter((d) => !(rangeOf(d).earliest && rangeOf(d).latest)).length

  return (
    <div className="imports__step-body">
      <section className="plate imports__section" aria-labelledby="dates-heading">
        <h2 id="dates-heading" className="imports__section-title">
          Dates not understood
        </h2>
        {dates.data.length === 0 ? (
          <p className="muted">Every date in the workbook is understood.</p>
        ) : (
          <>
            <p className="muted imports__section-sub">
              Give a year range to import these rows with the date text as written. Rows whose date has no range are listed as problems and not imported.
            </p>
            <ul className="imports__dates">
              {dates.data.map((d) => (
                <DateRow
                  key={d.text}
                  date={d}
                  range={rangeOf(d)}
                  onChange={(r) => setEdits((all) => ({ ...all, [d.text]: r }))}
                />
              ))}
            </ul>
          </>
        )}
      </section>
      <div className="imports__actions">
        <p className="muted" aria-live="polite">
          {open > 0 ? `${open} without a range` : dates.data.length > 0 ? 'Every date has a range.' : ''}
        </p>
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

function DateRow({ date, range, onChange }: { date: ImportDate; range: Range; onChange: (r: Range) => void }) {
  const fromId = useId()
  const toId = useId()
  const rows = date.sheetRows
  return (
    <li className="imports__date">
      <div className="imports__place-text">
        <strong>{date.text}</strong>
        <span className="muted">
          {rows.length === 1 ? `Row ${rows[0]}` : `Rows ${rows.slice(0, 6).join(', ')}${rows.length > 6 ? ` and ${rows.length - 6} more` : ''}`}
        </span>
      </div>
      <div className="imports__years">
        <div className="field">
          <label className="field__label" htmlFor={fromId}>
            Earliest year for “{date.text}”
          </label>
          <input id={fromId} className="input" inputMode="numeric" value={range.earliest} onChange={(e) => onChange({ ...range, earliest: e.target.value.replace(/\D/g, '') })} />
        </div>
        <div className="field">
          <label className="field__label" htmlFor={toId}>
            Latest year for “{date.text}”
          </label>
          <input id={toId} className="input" inputMode="numeric" value={range.latest} onChange={(e) => onChange({ ...range, latest: e.target.value.replace(/\D/g, '') })} />
        </div>
      </div>
    </li>
  )
}
