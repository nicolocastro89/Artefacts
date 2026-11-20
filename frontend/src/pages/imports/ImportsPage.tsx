import { useQuery, useQueryClient } from '@tanstack/react-query'
import { FileSpreadsheet, Upload } from 'lucide-react'
import { useEffect, useId, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { api } from '../../api/client'
import { unwrap } from '../../api/problem'
import {
  checkWorkbookFile,
  errorMessage,
  formatBytes,
  uploadWorkbook,
  useImportJobs,
  useImportTemplates,
  type ImportJob,
} from './importApi'
import { StateChip } from './parts'
import './imports.css'

/** The Import section: upload a workbook, and every import so far with its state and counts. */
export function ImportsPage() {
  const { data: jobs = [], isPending, isError, error } = useImportJobs()

  useEffect(() => {
    document.title = 'Import · Artefacts'
  }, [])

  return (
    <div className="imports">
      <div className="page-head">
        <div className="page-head__titles">
          <h1>Import</h1>
          <p className="page-head__sub">Bring records and their pictures in from an Excel workbook. Nothing is added to the catalogue until you have reviewed every row.</p>
        </div>
      </div>

      <UploadForm />

      <section aria-labelledby="imports-heading" className="imports__list">
        <h2 id="imports-heading" className="imports__heading">
          Imports
        </h2>
        {isPending ? (
          <div className="data-state" aria-busy="true">
            Loading…
          </div>
        ) : isError ? (
          <div className="data-state data-state--error" role="alert">
            Could not load imports: {error.message}
          </div>
        ) : jobs.length === 0 ? (
          <div className="plate empty">
            <strong>No imports yet.</strong>
            <p>Upload a workbook above to start the first one.</p>
          </div>
        ) : (
          <div className="table-wrap">
            <table className="table imports__table">
              <caption className="visually-hidden">Imports, newest first</caption>
              <thead>
                <tr>
                  <th scope="col">Workbook</th>
                  <th scope="col">Object type</th>
                  <th scope="col">State</th>
                  <th scope="col" className="imports__num">
                    Rows
                  </th>
                  <th scope="col" className="imports__num">
                    Imported
                  </th>
                  <th scope="col" className="imports__num">
                    Problems
                  </th>
                  <th scope="col">Started</th>
                </tr>
              </thead>
              <tbody>
                {jobs.map((job) => (
                  <JobRow key={job.id} job={job} />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}

function JobRow({ job }: { job: ImportJob }) {
  const running = job.state === 'importing'
  return (
    <tr>
      <td>
        <Link className="cell-title imports__file" to={`/imports/${job.id}`}>
          <FileSpreadsheet aria-hidden="true" />
          {job.fileName}
        </Link>
        <span className="muted imports__meta">
          {job.sheetName ? `${job.sheetName} · ` : ''}
          {formatBytes(job.fileSize)}
        </span>
      </td>
      <td data-label="Object type">{job.objectType.name}</td>
      <td data-label="State">
        <StateChip state={job.state} />
        {running && job.totalRows > 0 && (
          <span className="imports__meta">
            {job.processedRows} of {job.totalRows} rows
          </span>
        )}
      </td>
      <td className="imports__num" data-label="Rows">
        {job.state === 'reading' ? '…' : job.totalRows.toLocaleString()}
      </td>
      <td className="imports__num" data-label="Imported">
        {job.state === 'finished' || running ? job.importedRows.toLocaleString() : '–'}
      </td>
      <td className="imports__num" data-label="Problems">
        {job.state === 'finished' || running ? job.problemRows.toLocaleString() : '–'}
      </td>
      <td data-label="Started">{new Date(job.createdAt).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })}</td>
    </tr>
  )
}

function UploadForm() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const fileId = useId()
  const typeId = useId()
  const templateId = useId()
  const input = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [objectType, setObjectType] = useState('')
  const [template, setTemplate] = useState('')
  const [progress, setProgress] = useState<number | null>(null)
  const [error, setError] = useState<{ field: 'file' | 'objectTypeId' | 'form'; text: string } | null>(null)
  const { data: types = [] } = useQuery({
    queryKey: ['object-types', 'all'],
    queryFn: async () => unwrap(await api.GET('/api/object-types')),
  })
  const { data: templates = [] } = useImportTemplates()

  const choose = (f: File | null) => {
    setFile(f)
    setError(f ? (checkWorkbookFile(f) ? { field: 'file', text: checkWorkbookFile(f)! } : null) : null)
  }

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!file) return setError({ field: 'file', text: 'Choose an .xlsx workbook to import.' })
    const fileProblem = checkWorkbookFile(file)
    if (fileProblem) return setError({ field: 'file', text: fileProblem })
    if (!objectType) return setError({ field: 'objectTypeId', text: 'Choose the object type of the rows in this workbook.' })
    setError(null)
    setProgress(0)
    try {
      const job = await uploadWorkbook({ file, objectTypeId: objectType, templateId: template || null }, setProgress)
      await queryClient.invalidateQueries({ queryKey: ['imports'] })
      void navigate(`/imports/${job.id}`)
    } catch (err) {
      setError({ field: 'form', text: errorMessage(err) })
      setProgress(null)
    }
  }

  const uploading = progress !== null

  return (
    <form className="plate imports__upload" onSubmit={submit} noValidate aria-describedby={error?.field === 'form' ? 'upload-error' : undefined}>
      <div className={`field imports__file-field${error?.field === 'file' ? ' field--invalid' : ''}`}>
        <label className="field__label" htmlFor={fileId}>
          Workbook
        </label>
        <input
          ref={input}
          id={fileId}
          className="input imports__file-input"
          type="file"
          accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
          disabled={uploading}
          aria-invalid={error?.field === 'file' || undefined}
          aria-describedby={`${fileId}-hint`}
          onChange={(e) => choose(e.target.files?.[0] ?? null)}
        />
        <p id={`${fileId}-hint`} className={error?.field === 'file' ? 'field__error' : 'field__hint'} role={error?.field === 'file' ? 'alert' : undefined}>
          {error?.field === 'file' ? error.text : file ? `${file.name} · ${formatBytes(file.size)}` : 'An .xlsx file with pictures in it, up to 2 GB'}
        </p>
      </div>
      <div className={`field${error?.field === 'objectTypeId' ? ' field--invalid' : ''}`}>
        <label className="field__label" htmlFor={typeId}>
          Object type
        </label>
        <select
          id={typeId}
          className="select"
          value={objectType}
          disabled={uploading}
          aria-invalid={error?.field === 'objectTypeId' || undefined}
          onChange={(e) => setObjectType(e.target.value)}
        >
          <option value="">Choose…</option>
          {types.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>
        {error?.field === 'objectTypeId' ? (
          <p className="field__error" role="alert">
            {error.text}
          </p>
        ) : (
          <p className="field__hint">Every row of the workbook gets this type</p>
        )}
      </div>
      <div className="field">
        <label className="field__label" htmlFor={templateId}>
          Template
        </label>
        <select id={templateId} className="select" value={template} disabled={uploading} onChange={(e) => setTemplate(e.target.value)}>
          <option value="">None: suggest a mapping</option>
          {templates.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>
        <p className="field__hint">A saved mapping with its place and date decisions</p>
      </div>
      <button type="submit" className="btn btn--primary" disabled={uploading}>
        <Upload aria-hidden="true" /> {uploading ? 'Uploading…' : 'Upload and read'}
      </button>
      {uploading && (
        <div className="imports__progress" role="progressbar" aria-label="Upload" aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(progress * 100)}>
          <span style={{ width: `${Math.round(progress * 100)}%` }} />
        </div>
      )}
      {error?.field === 'form' && (
        <p id="upload-error" className="notice notice--error imports__upload-error" role="alert">
          {error.text}
        </p>
      )}
    </form>
  )
}

