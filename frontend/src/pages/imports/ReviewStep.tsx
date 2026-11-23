import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Play } from 'lucide-react'
import { useState } from 'react'
import { api } from '../../api/client'
import { unwrap } from '../../api/problem'
import { errorMessage, fieldLabel, type ImportJob, type ImportRowStatus, type ReviewRow } from './importApi'
import { TemplateSaver } from './MappingStep'
import { StatusChip, DiscardedLines } from './parts'

const FILTERS: { value: ImportRowStatus | null; label: string; count: (c: { total: number; ready: number; skipped: number; problems: number }) => number }[] = [
  { value: null, label: 'All rows', count: (c) => c.total },
  { value: 'ready', label: 'Ready', count: (c) => c.ready },
  { value: 'skipped', label: 'Already present', count: (c) => c.skipped },
  { value: 'problem', label: 'Problems', count: (c) => c.problems },
]

const PAGE = 50

/** Every row as it will be imported, before anything is saved; importing starts the background import. */
export function ReviewStep({ job }: { job: ImportJob }) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<ImportRowStatus | null>(null)
  const [pageSize, setPageSize] = useState(PAGE)
  const [error, setError] = useState<string | null>(null)
  const [committing, setCommitting] = useState(false)
  const review = useQuery({
    queryKey: ['imports', job.id, 'review', status, pageSize],
    queryFn: async () =>
      unwrap(
        await api.GET('/api/imports/{id}/review', {
          params: { path: { id: job.id }, query: { status: status ?? undefined, pageSize } },
        }),
      ),
    placeholderData: (previous) => previous,
  })

  const commit = async () => {
    setCommitting(true)
    setError(null)
    try {
      const updated = unwrap(await api.POST('/api/imports/{id}/commit', { params: { path: { id: job.id } } }))
      queryClient.setQueryData(['imports', job.id], updated)
      await queryClient.invalidateQueries({ queryKey: ['imports'] })
    } catch (e) {
      setError(errorMessage(e))
      setCommitting(false)
    }
  }

  if (review.isPending)
    return (
      <div className="data-state" aria-busy="true">
        Preparing the review…
      </div>
    )
  if (review.isError)
    return (
      <div className="data-state data-state--error" role="alert">
        Could not prepare the review: {review.error.message}
      </div>
    )

  const { counts, rows, total } = review.data

  return (
    <div className="imports__step-body">
      <section className="plate imports__section" aria-labelledby="review-heading">
        <div className="imports__review-head">
          <h2 id="review-heading" className="imports__section-title">
            Review
          </h2>
          <p className="imports__summary">
            <strong>{counts.ready.toLocaleString()}</strong> ready to import
            {counts.skipped > 0 && (
              <>
                {' '}
                · <strong>{counts.skipped.toLocaleString()}</strong> already in the catalogue
              </>
            )}
            {counts.problems > 0 && (
              <>
                {' '}
                · <strong>{counts.problems.toLocaleString()}</strong> with problems
              </>
            )}
          </p>
        </div>
        <div className="seg imports__row-filter" role="group" aria-label="Show rows">
          {FILTERS.map((f) => (
            <button
              key={f.label}
              type="button"
              className="seg__btn"
              aria-pressed={status === f.value}
              onClick={() => {
                setStatus(f.value)
                setPageSize(PAGE)
              }}
            >
              {f.label} <span className="imports__count">{f.count(counts).toLocaleString()}</span>
            </button>
          ))}
        </div>
        {rows.length === 0 ? (
          <p className="muted">No rows.</p>
        ) : (
          <div className="table-wrap">
            <table className="table imports__review">
              <caption className="visually-hidden">Rows as they will be imported</caption>
              <thead>
                <tr>
                  <th scope="col" className="imports__num">
                    Row
                  </th>
                  <th scope="col">Pictures</th>
                  <th scope="col">Record</th>
                  <th scope="col">Status</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <ReviewTableRow key={row.sheetRow} row={row} />
                ))}
              </tbody>
            </table>
          </div>
        )}
        {rows.length < total && (
          <button type="button" className="btn imports__more" onClick={() => setPageSize((s) => s + PAGE)}>
            Show more rows ({(total - rows.length).toLocaleString()} left)
          </button>
        )}
      </section>
      <section className="plate imports__section" aria-label="Template">
        <p className="muted imports__section-sub">
          Save the mapping with every place, list and date decision as a template, so the next workbook starts from them.
        </p>
        <TemplateSaver job={job} />
      </section>
      <div className="imports__actions">
        <p className="muted">Each row is saved on its own: problem rows are listed in the report and never hold up the others.</p>
        <div className="imports__actions-main">
          {error && (
            <p className="field__error" role="alert">
              {error}
            </p>
          )}
          <button type="button" className="btn btn--primary" disabled={committing || counts.ready === 0} onClick={() => void commit()}>
            <Play aria-hidden="true" /> Import {counts.ready.toLocaleString()} {counts.ready === 1 ? 'row' : 'rows'}
          </button>
        </div>
      </div>
    </div>
  )
}

function ReviewTableRow({ row }: { row: ReviewRow }) {
  return (
    <tr className={row.status === 'problem' ? 'imports__row--problem' : undefined}>
      <td className="imports__num" data-label="Row">
        {row.sheetRow}
      </td>
      <td data-label="Pictures">
        <div className="imports__thumbs">
          {row.pictures.length === 0 && <span className="muted">None</span>}
          {row.pictures.map((p) =>
            p.thumbUrl ? (
              <span key={p.index} className="thumb">
                <img src={p.thumbUrl} alt={p.name} loading="lazy" />
              </span>
            ) : (
              <span key={p.index} className="thumb imports__thumb--unsupported" title={`${p.name}: format not accepted`}>
                <span className="visually-hidden">{p.name}: format not accepted</span>
              </span>
            ),
          )}
        </div>
      </td>
      <td>
        <div className="imports__record-head">
          <span className="tag">{row.inventoryNumber ?? 'No number'}</span>
          <span className="cell-title">{row.title ?? <span className="muted">No title</span>}</span>
        </div>
        {row.fields.length > 0 && (
          <dl className="imports__fields">
            {row.fields
              .filter((f) => f.field !== 'title')
              .map((f) => (
                <div key={f.field}>
                  <dt>{fieldLabel(f.field)}</dt>
                  <dd>{f.value}</dd>
                </div>
              ))}
          </dl>
        )}
        <DiscardedLines lines={row.discarded} />
        {row.problems.length > 0 && (
          <ul className="imports__problems">
            {row.problems.map((p, i) => (
              <li key={i}>
                <strong>{fieldLabel(p.field)}:</strong> {p.reason}
                {p.text && <span className="muted"> (“{p.text}”)</span>}
              </li>
            ))}
          </ul>
        )}
        {row.warnings.length > 0 && (
          <ul className="imports__row-warnings">
            {row.warnings.map((w, i) => (
              <li key={i}>{w}</li>
            ))}
          </ul>
        )}
      </td>
      <td data-label="Status">
        <StatusChip status={row.status} />
      </td>
    </tr>
  )
}
