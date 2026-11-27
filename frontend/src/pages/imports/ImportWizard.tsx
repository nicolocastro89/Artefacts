import { useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Trash2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router'
import { api } from '../../api/client'
import { unwrap } from '../../api/problem'
import { errorMessage, formatBytes, isEditable, useImportJob, type ImportJob } from './importApi'
import { ListsStep } from './ListsStep'
import { MappingStep } from './MappingStep'
import { StateChip } from './parts'
import { PlacesStep } from './PlacesStep'
import { ProgressView } from './ProgressView'
import { ResolutionStep } from './ResolutionStep'
import { ReviewStep } from './ReviewStep'
import './imports.css'

export type WizardStep = 'mapping' | 'places' | 'lists' | 'dates' | 'dimensions' | 'review'

const STEPS: { id: WizardStep; label: string }[] = [
  { id: 'mapping', label: 'Columns' },
  { id: 'places', label: 'Places' },
  { id: 'lists', label: 'Lists' },
  { id: 'dates', label: 'Dates' },
  { id: 'dimensions', label: 'Dimensions' },
  { id: 'review', label: 'Review' },
]

/** The step a job opens on when none is chosen: the first unfinished one. */
function defaultStep(job: ImportJob): WizardStep {
  return job.state === 'readyForReview' ? 'places' : 'mapping'
}

/**
 * One import, as a workflow over its state: the user maps columns, settles places, list values, dates and dimensions and reviews every row,
 * moving freely between those steps until they import; then the page follows the background import to its report.
 */
export function ImportWizard() {
  const { id = '' } = useParams()
  const { data: job, isPending, isError, error } = useImportJob(id)

  useEffect(() => {
    document.title = `${job?.fileName ?? 'Import'} · Artefacts`
  }, [job?.fileName])

  if (isPending)
    return (
      <div className="data-state" aria-busy="true">
        Loading…
      </div>
    )
  if (isError)
    return (
      <div className="data-state data-state--error" role="alert">
        Could not load the import: {error.message}
      </div>
    )

  return (
    <div className="imports imports--wizard">
      <Link className="btn btn--quiet btn--sm imports__back" to="/imports">
        <ArrowLeft aria-hidden="true" /> All imports
      </Link>
      <div className="page-head">
        <div className="page-head__titles">
          <h1>{job.fileName}</h1>
          <p className="page-head__sub">
            <StateChip state={job.state} /> {job.objectType.name}
            {job.sheetName && <> · sheet “{job.sheetName}”</>}
            {job.state !== 'reading' && job.totalRows > 0 && <> · {job.totalRows.toLocaleString()} rows</>}
            {job.groupHeadings > 0 && <> in {job.groupHeadings} groups</>} · {formatBytes(job.fileSize)}
            {job.template && <> · template “{job.template.name}”</>}
          </p>
        </div>
        {isEditable(job.state) && <DiscardButton job={job} />}
      </div>
      {job.warnings.length > 0 && isEditable(job.state) && (
        <ul className="notice notice--warning imports__warnings">
          {job.warnings.map((w) => (
            <li key={w}>{w}</li>
          ))}
        </ul>
      )}
      <Body job={job} />
    </div>
  )
}

function Body({ job }: { job: ImportJob }) {
  const [params, setParams] = useSearchParams()

  if (job.state === 'reading')
    return (
      <div className="plate imports__status" aria-live="polite" aria-busy="true">
        <strong>Reading the workbook…</strong>
        <p className="muted">Rows and pictures are being copied out of the file. You can leave this page; the import keeps going.</p>
      </div>
    )
  if (job.state === 'failed')
    return (
      <div className="notice notice--error" role="alert">
        <strong>The workbook could not be read.</strong> {job.error}
      </div>
    )
  if (job.state === 'discarded')
    return (
      <div className="plate empty">
        <strong>This import was discarded.</strong>
        <p>Nothing was added to the catalogue, and the uploaded workbook and its pictures were removed.</p>
      </div>
    )
  if (job.state === 'importing' || job.state === 'finished') return <ProgressView job={job} />

  const step = (STEPS.find((s) => s.id === params.get('step'))?.id ?? defaultStep(job)) as WizardStep
  const go = (next: WizardStep) => setParams({ step: next })

  return (
    <>
      <nav className="imports__steps" aria-label="Import steps">
        <ol>
          {STEPS.map((s, i) => (
            <li key={s.id}>
              <button
                type="button"
                className="imports__step"
                aria-current={s.id === step ? 'step' : undefined}
                // The other steps need a saved mapping.
                disabled={s.id !== 'mapping' && job.state === 'awaitingMapping'}
                onClick={() => go(s.id)}
              >
                <span className="imports__step-number">{i + 1}</span> {s.label}
              </button>
            </li>
          ))}
        </ol>
      </nav>
      {step === 'mapping' && <MappingStep job={job} onDone={() => go('places')} />}
      {step === 'places' && <PlacesStep job={job} onDone={() => go('lists')} />}
      {step === 'lists' && <ListsStep job={job} onDone={() => go('dates')} />}
      {step === 'dates' && <ResolutionStep job={job} kind="dates" onDone={() => go('dimensions')} />}
      {step === 'dimensions' && <ResolutionStep job={job} kind="dimensions" onDone={() => go('review')} />}
      {step === 'review' && <ReviewStep job={job} />}
    </>
  )
}

function DiscardButton({ job }: { job: ImportJob }) {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)

  const discard = async () => {
    if (!window.confirm(`Discard the import of “${job.fileName}”? The uploaded workbook and its pictures are removed; nothing is added to the catalogue.`))
      return
    try {
      unwrap(await api.POST('/api/imports/{id}/discard', { params: { path: { id: job.id } } }))
      await queryClient.invalidateQueries({ queryKey: ['imports'] })
      void navigate('/imports')
    } catch (e) {
      setError(errorMessage(e))
    }
  }

  return (
    <div className="imports__discard">
      <button type="button" className="btn btn--danger" onClick={() => void discard()}>
        <Trash2 aria-hidden="true" /> Discard import
      </button>
      {error && (
        <p className="field__error" role="alert">
          {error}
        </p>
      )}
    </div>
  )
}
