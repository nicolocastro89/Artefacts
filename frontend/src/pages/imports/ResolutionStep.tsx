import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight } from 'lucide-react'
import { useId, useState } from 'react'
import { api } from '../../api/client'
import { unwrap } from '../../api/problem'
import { useUnits } from '../../api/units'
import { errorMessage, type ImportJob, type ImportResolution, type ImportResolutionItem } from './importApi'
import { RESOLUTION_EDITORS, type ResolutionContext } from './resolutionKinds'

/**
 * Texts of one kind the import cannot settle by rule (dates, dimensions), once per distinct text: a decision applies
 * to every row using the text. The server lists the texts and stores the decisions; the kind's editor draws one text.
 * Rows whose text has no decision are held back from the import.
 */
export function ResolutionStep({ job, kind, onDone }: { job: ImportJob; kind: keyof typeof RESOLUTION_EDITORS; onDone: () => void }) {
  const editor = RESOLUTION_EDITORS[kind]
  const headingId = useId()
  const queryClient = useQueryClient()
  const { data: units = [] } = useUnits()
  const items = useQuery({
    queryKey: ['imports', job.id, 'resolutions', kind],
    queryFn: async () => unwrap(await api.GET('/api/imports/{id}/resolutions/{kind}', { params: { path: { id: job.id, kind } } })),
  })
  // Drafts on top of the saved decisions.
  const [edits, setEdits] = useState<Record<string, unknown>>({})
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const ctx: ResolutionContext = {
    units,
    defaultUnit: job.mapping?.dimensions?.defaultUnitId ?? units.find((u) => u.abbreviation === 'cm')?.id ?? '',
  }

  const draftFor = (item: ImportResolutionItem) => (item.text in edits ? edits[item.text] : editor.draftOf(item, ctx))

  const save = async () => {
    setSaving(true)
    setError(null)
    try {
      const resolutions = (items.data ?? []).map((i) => editor.toResolution(i.text, draftFor(i))).filter((r): r is ImportResolution => r !== null)
      unwrap(await api.PUT('/api/imports/{id}/resolutions/{kind}', { params: { path: { id: job.id, kind } }, body: { resolutions } }))
      await queryClient.invalidateQueries({ queryKey: ['imports', job.id] })
      onDone()
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      setSaving(false)
    }
  }

  if (items.isPending)
    return (
      <div className="data-state" aria-busy="true">
        {editor.loadingText}
      </div>
    )
  if (items.isError)
    return (
      <div className="data-state data-state--error" role="alert">
        {editor.errorText}: {items.error.message}
      </div>
    )

  const undecided = items.data.filter((i) => !editor.isDecided(draftFor(i)))
  const settleRest = () => {
    const rest = editor.settleRest
    if (rest) setEdits((all) => ({ ...all, ...Object.fromEntries(undecided.map((i) => [i.text, rest.apply(draftFor(i))])) }))
  }

  return (
    <div className="imports__step-body">
      <section className="plate imports__section" aria-labelledby={headingId}>
        <h2 id={headingId} className="imports__section-title">
          {editor.heading}
        </h2>
        {items.data.length === 0 ? (
          <p className="muted">{editor.emptyText}</p>
        ) : (
          <>
            <p className="muted imports__section-sub">{editor.intro}</p>
            <ul className="imports__dates">
              {items.data.map((item) => (
                <editor.Row key={item.text} item={item} draft={draftFor(item)} ctx={ctx} onChange={(draft) => setEdits((all) => ({ ...all, [item.text]: draft }))} />
              ))}
            </ul>
          </>
        )}
      </section>
      <div className="imports__actions">
        <div className="imports__actions-side">
          <p className="muted" aria-live="polite">
            {editor.summary(undecided.length, items.data.length)}
          </p>
          {editor.settleRest && undecided.length > 0 && (
            <button type="button" className="btn btn--sm" onClick={settleRest}>
              {editor.settleRest.label(undecided.length)}
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
            {editor.continueLabel} <ArrowRight aria-hidden="true" />
          </button>
        </div>
      </div>
    </div>
  )
}
