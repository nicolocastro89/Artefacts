import { useQuery } from '@tanstack/react-query'
import { Download } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { api } from '../../api/client'
import { unwrap } from '../../api/problem'
import { useFieldLabel, type ImportJob, type ImportRowStatus } from './importApi'
import { StatusChip } from './parts'

/**
 * A committed import: progress while the worker runs (the job is polled every 2 seconds; the page can be left and
 * reopened), then the report of imported, skipped and problem rows.
 */
export function ProgressView({ job }: { job: ImportJob }) {
  const fieldLabel = useFieldLabel()
  const finished = job.state === 'finished'
  const fraction = job.totalRows > 0 ? job.processedRows / job.totalRows : 0
  const [filter, setFilter] = useState<ImportRowStatus | null>(null)
  const report = useQuery({
    queryKey: ['imports', job.id, 'report', job.processedRows, job.state],
    queryFn: async () => unwrap(await api.GET('/api/imports/{id}/report', { params: { path: { id: job.id } } })),
    enabled: finished,
  })

  const rows = (report.data?.rows ?? []).filter((r) => !filter || r.status === filter)

  return (
    <div className="imports__step-body">
      <section className="plate imports__section" aria-labelledby="progress-heading" aria-live="polite">
        <h2 id="progress-heading" className="imports__section-title">
          {finished ? 'Import finished' : 'Importing…'}
        </h2>
        {!finished && (
          <>
            <div
              className="imports__progress imports__progress--large"
              role="progressbar"
              aria-label="Rows imported"
              aria-valuemin={0}
              aria-valuemax={job.totalRows}
              aria-valuenow={job.processedRows}
            >
              <span style={{ width: `${Math.round(fraction * 100)}%` }} />
            </div>
            <p className="muted">
              {job.processedRows.toLocaleString()} of {job.totalRows.toLocaleString()} rows. You can leave this page: the import keeps running and its report waits here.
            </p>
          </>
        )}
        <dl className="imports__totals">
          <div>
            <dt>Imported</dt>
            <dd>{job.importedRows.toLocaleString()}</dd>
          </div>
          <div>
            <dt>Already present</dt>
            <dd>{job.skippedRows.toLocaleString()}</dd>
          </div>
          <div>
            <dt>Problems</dt>
            <dd>{job.problemRows.toLocaleString()}</dd>
          </div>
        </dl>
        {finished && (
          <a className="btn" href={`/api/imports/${job.id}/report.xlsx`} download>
            <Download aria-hidden="true" /> Download report (.xlsx)
          </a>
        )}
      </section>

      {finished && (
        <section className="plate imports__section" aria-labelledby="report-heading">
          <h2 id="report-heading" className="imports__section-title">
            Report
          </h2>
          <div className="seg" role="group" aria-label="Show rows">
            {([null, 'imported', 'skipped', 'problem'] as const).map((s) => (
              <button key={s ?? 'all'} type="button" className="seg__btn" aria-pressed={filter === s} onClick={() => setFilter(s)}>
                {s === null ? 'All rows' : s === 'imported' ? 'Imported' : s === 'skipped' ? 'Already present' : 'Problems'}
              </button>
            ))}
          </div>
          {report.isPending ? (
            <div className="data-state" aria-busy="true">
              Loading the report…
            </div>
          ) : report.isError ? (
            <div className="data-state data-state--error" role="alert">
              Could not load the report: {report.error.message}
            </div>
          ) : (
            <div className="table-wrap">
              <table className="table imports__review">
                <caption className="visually-hidden">Import report</caption>
                <thead>
                  <tr>
                    <th scope="col" className="imports__num">
                      Row
                    </th>
                    <th scope="col">Inventory number</th>
                    <th scope="col">Status</th>
                    <th scope="col">Details</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr key={r.sheetRow} className={r.status === 'problem' ? 'imports__row--problem' : undefined}>
                      <td className="imports__num" data-label="Row">
                        {r.sheetRow}
                      </td>
                      <td data-label="Inventory number">
                        {r.artefactId ? (
                          <Link className="tag" to={`/artefacts/${r.artefactId}`}>
                            {r.inventoryNumber}
                          </Link>
                        ) : (
                          (r.inventoryNumber ?? <span className="muted">None</span>)
                        )}
                      </td>
                      <td data-label="Status">
                        <StatusChip status={r.status} />
                      </td>
                      <td data-label="Details">
                        {r.problems.length > 0 && (
                          <ul className="imports__problems">
                            {r.problems.map((p, i) => (
                              <li key={i}>
                                <strong>{fieldLabel(p.field)}:</strong> {p.reason}
                                {p.text && <span className="muted"> (“{p.text}”)</span>}
                              </li>
                            ))}
                          </ul>
                        )}
                        {r.warnings.length > 0 && (
                          <ul className="imports__row-warnings">
                            {r.warnings.map((w, i) => (
                              <li key={i}>{w}</li>
                            ))}
                          </ul>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}
    </div>
  )
}
