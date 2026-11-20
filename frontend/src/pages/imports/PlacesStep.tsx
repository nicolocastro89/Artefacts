import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight } from 'lucide-react'
import { useId, useState } from 'react'
import { api, type Language, type Region } from '../../api/client'
import { unwrap } from '../../api/problem'
import { useContentLanguages } from '../../app/languages'
import { errorMessage, type ImportJob, type ImportPlace, type PlaceDecision } from './importApi'

type Choice = { kind: 'undecided' } | { kind: 'link'; regionId: string } | { kind: 'create'; names: Record<string, string>; parentId: string } | { kind: 'keep' }

function choiceOf(place: ImportPlace, language: string): Choice {
  const d = place.decision
  if (!d) return { kind: 'undecided' }
  if (d.action === 'link' && d.regionId) return { kind: 'link', regionId: d.regionId }
  if (d.action === 'create') return { kind: 'create', names: { ...(d.names ?? { [language]: place.text }) }, parentId: d.parentId ?? '' }
  return { kind: 'keep' }
}

function decisionOf(text: string, choice: Choice): PlaceDecision | null {
  switch (choice.kind) {
    case 'link':
      return { text, action: 'link', regionId: choice.regionId }
    case 'create':
      return { text, action: 'create', names: choice.names, parentId: choice.parentId || null }
    case 'keep':
      return { text, action: 'keep' }
    default:
      return null
  }
}

/**
 * Each distinct place text of the workbook: link it to a region (exact matches in any language are linked already),
 * create a region for it, or keep it as text only. The text itself is always imported as written.
 */
export function PlacesStep({ job, onDone }: { job: ImportJob; onDone: () => void }) {
  const queryClient = useQueryClient()
  const { data: languages = [] } = useContentLanguages()
  const sourceLanguage = job.mapping?.language ?? 'it'
  const places = useQuery({
    queryKey: ['imports', job.id, 'places'],
    queryFn: async () => unwrap(await api.GET('/api/imports/{id}/places', { params: { path: { id: job.id } } })),
  })
  const regions = useQuery({ queryKey: ['regions', 'all'], queryFn: async () => unwrap(await api.GET('/api/regions')) })
  // Choices made on this page, on top of the saved decisions.
  const [choices, setChoices] = useState<Record<string, Choice>>({})
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const current = (p: ImportPlace): Choice => choices[p.text] ?? choiceOf(p, sourceLanguage)

  const save = async () => {
    setSaving(true)
    setError(null)
    try {
      const decisions = (places.data ?? []).map((p) => decisionOf(p.text, current(p))).filter((d) => d !== null)
      unwrap(await api.PUT('/api/imports/{id}/places', { params: { path: { id: job.id } }, body: { places: decisions } }))
      await queryClient.invalidateQueries({ queryKey: ['imports', job.id] })
      onDone()
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      setSaving(false)
    }
  }

  if (places.isPending)
    return (
      <div className="data-state" aria-busy="true">
        Matching places…
      </div>
    )
  if (places.isError)
    return (
      <div className="data-state data-state--error" role="alert">
        Could not load the places: {places.error.message}
      </div>
    )

  const undecided = places.data.filter((p) => current(p).kind === 'undecided').length

  return (
    <div className="imports__step-body">
      <section className="plate imports__section" aria-labelledby="places-heading">
        <h2 id="places-heading" className="imports__section-title">
          Places of production
        </h2>
        <p className="muted imports__section-sub">
          {places.data.length === 0
            ? 'No row has a place of production.'
            : `${places.data.length} distinct ${places.data.length === 1 ? 'place' : 'places'}. Places left undecided are imported as text without a region.`}
        </p>
        {places.data.length > 0 && (
          <ul className="imports__places">
            {places.data.map((p) => (
              <PlaceRow
                key={p.text}
                place={p}
                choice={current(p)}
                regions={regions.data ?? []}
                languages={languages}
                sourceLanguage={sourceLanguage}
                onChange={(c) => setChoices((all) => ({ ...all, [p.text]: c }))}
              />
            ))}
          </ul>
        )}
      </section>
      <div className="imports__actions">
        <p className="muted" aria-live="polite">
          {undecided > 0 ? `${undecided} undecided` : 'Every place has a decision.'}
        </p>
        <div className="imports__actions-main">
          {error && (
            <p className="field__error" role="alert">
              {error}
            </p>
          )}
          <button type="button" className="btn btn--primary" disabled={saving} onClick={() => void save()}>
            Save and continue to dates <ArrowRight aria-hidden="true" />
          </button>
        </div>
      </div>
    </div>
  )
}

function PlaceRow({
  place,
  choice,
  regions,
  languages,
  sourceLanguage,
  onChange,
}: {
  place: ImportPlace
  choice: Choice
  regions: Region[]
  languages: Language[]
  sourceLanguage: string
  onChange: (c: Choice) => void
}) {
  const id = useId()
  const otherId = useId()
  const parentId = useId()
  const suggested = new Set(place.suggestions.map((s) => s.regionId))
  const value =
    choice.kind === 'link' ? (suggested.has(choice.regionId) ? `s:${choice.regionId}` : 'other') : choice.kind === 'undecided' ? '' : choice.kind

  const select = (v: string) => {
    if (v.startsWith('s:')) onChange({ kind: 'link', regionId: v.slice(2) })
    else if (v === 'other') onChange({ kind: 'link', regionId: choice.kind === 'link' ? choice.regionId : '' })
    else if (v === 'create') onChange({ kind: 'create', names: { [sourceLanguage]: place.text.replace(/\?+$/, '').trim() }, parentId: '' })
    else if (v === 'keep') onChange({ kind: 'keep' })
    else onChange({ kind: 'undecided' })
  }

  return (
    <li className="imports__place">
      <div className="imports__place-text">
        <strong lang={sourceLanguage}>{place.text}</strong>
        <span className="muted">
          {place.rows} {place.rows === 1 ? 'row' : 'rows'}
        </span>
      </div>
      <div className="imports__place-decision">
        <div className="field">
          <label className="field__label" htmlFor={id}>
            Region for “{place.text}”
          </label>
          <select id={id} className="select" value={value} onChange={(e) => select(e.target.value)}>
            <option value="">Undecided: keep as text</option>
            {place.suggestions.map((s) => (
              <option key={s.regionId} value={`s:${s.regionId}`}>
                {s.regionName}
                {s.exact ? ' — same name' : ` — ${Math.round(s.score * 100)}% similar`}
                {s.alias ? ' (linked before)' : ''}
              </option>
            ))}
            <option value="other">Another region…</option>
            <option value="create">Create a new region…</option>
            <option value="keep">Keep as text, without a region</option>
          </select>
          {choice.kind === 'undecided' && place.suggestions.length > 0 && (
            <p className="field__hint">
              Similar: {place.suggestions.map((s) => `${s.regionName} (${Math.round(s.score * 100)}%)`).join(', ')}
            </p>
          )}
        </div>
        {value === 'other' && choice.kind === 'link' && (
          <div className="field">
            <label className="field__label" htmlFor={otherId}>
              Region
            </label>
            <select id={otherId} className="select" value={choice.regionId} onChange={(e) => onChange({ kind: 'link', regionId: e.target.value })}>
              <option value="">Choose…</option>
              {regions.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name}
                  {r.parentName ? ` (${r.parentName})` : ''}
                </option>
              ))}
            </select>
          </div>
        )}
        {choice.kind === 'create' && (
          <div className="imports__create">
            {languages.map((l) => (
              <NameInput
                key={l.code}
                language={l}
                place={place.text}
                value={choice.names[l.code] ?? ''}
                onChange={(v) => onChange({ ...choice, names: { ...choice.names, [l.code]: v } })}
              />
            ))}
            <div className="field">
              <label className="field__label" htmlFor={parentId}>
                Inside region
              </label>
              <select id={parentId} className="select" value={choice.parentId} onChange={(e) => onChange({ ...choice, parentId: e.target.value })}>
                <option value="">None (top level)</option>
                {regions.map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.name}
                  </option>
                ))}
              </select>
            </div>
          </div>
        )}
      </div>
    </li>
  )
}

function NameInput({ language, place, value, onChange }: { language: Language; place: string; value: string; onChange: (v: string) => void }) {
  const id = useId()
  return (
    <div className="field">
      <label className="field__label" htmlFor={id}>
        New region for “{place}” ({language.name})
      </label>
      <input id={id} className="input" lang={language.code} value={value} onChange={(e) => onChange(e.target.value)} />
    </div>
  )
}
