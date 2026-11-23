import { useQueryClient } from '@tanstack/react-query'
import { Check, Pencil, Plus, Trash2, X } from 'lucide-react'
import { useEffect, useId, useState, type FormEvent } from 'react'
import { api, type Language, type UnitOfMeasure } from '../api/client'
import { ApiError, unwrap } from '../api/problem'
import { UNITS_KEY, useUnits } from '../api/units'
import { useContentLanguages } from '../app/languages'
import { namesOf, type NameDraft } from './names'
import { NameCells, NameInputs } from './referenceNames'
import { ReferenceTabs } from './ReferenceTabs'
import './RegionsPage.css'

function message(e: unknown): string {
  if (e instanceof ApiError) return Object.values(e.fieldErrors).flat()[0] ?? e.message
  return e instanceof Error ? e.message : 'Something went wrong.'
}

/** A factor as typed: decimal comma or point; null when it is not a number. */
function parseFactor(text: string): number | null {
  const value = Number(text.trim().replace(',', '.'))
  return text.trim() === '' || Number.isNaN(value) ? null : value
}

/** Manages the units of measure (mm, cm, in, …) that artefact measurements are recorded in. */
export function UnitsPage() {
  const queryClient = useQueryClient()
  const { data: languages = [] } = useContentLanguages()
  const { data: units = [], isPending, isError, error } = useUnits()

  useEffect(() => {
    document.title = 'Units of measure · Artefacts'
  }, [])

  const refresh = () =>
    Promise.all([queryClient.invalidateQueries({ queryKey: UNITS_KEY }), queryClient.invalidateQueries({ queryKey: ['operation'] })])

  return (
    <div className="regions">
      <ReferenceTabs />
      <div className="page-head">
        <div className="page-head__titles">
          <h1>Units of measure</h1>
          <p className="page-head__sub">
            The units measurements are recorded in. The factor is the number of metres in one of the unit (a millimetre is 0.001); measurements keep the
            number as entered, so a corrected factor changes their value in metres.
          </p>
        </div>
      </div>

      <AddUnit languages={languages} onAdded={refresh} />

      {isPending ? (
        <div className="data-state" aria-busy="true">
          Loading…
        </div>
      ) : isError ? (
        <div className="data-state data-state--error" role="alert">
          Could not load units: {error.message}
        </div>
      ) : units.length === 0 ? (
        <div className="plate empty">
          <strong>No units yet.</strong>
          <p>Add the first unit above.</p>
        </div>
      ) : (
        <div className="table-wrap">
          <table className="table regions__table">
            <caption className="visually-hidden">Units of measure</caption>
            <thead>
              <tr>
                {languages.map((l) => (
                  <th key={l.code} scope="col">
                    {l.name}
                  </th>
                ))}
                <th scope="col">Abbreviation</th>
                <th scope="col" className="regions__count">
                  Metres per unit
                </th>
                <th scope="col" className="regions__count">
                  Artefacts
                </th>
                <th scope="col">
                  <span className="visually-hidden">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {units.map((unit) => (
                <UnitRow key={unit.id} unit={unit} languages={languages} onChanged={refresh} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function AddUnit({ languages, onAdded }: { languages: Language[]; onAdded: () => Promise<unknown> }) {
  const id = useId()
  const [draft, setDraft] = useState<NameDraft>({})
  const [abbreviation, setAbbreviation] = useState('')
  const [factor, setFactor] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const names = namesOf(draft)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    setNotice(null)
    try {
      const unit = unwrap(await api.POST('/api/units', { body: { names, abbreviation, factor: parseFactor(factor) } }))
      setNotice(`Added “${unit.name}” (${unit.abbreviation}).`)
      setDraft({})
      setAbbreviation('')
      setFactor('')
      await onAdded()
    } catch (err) {
      setError(message(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="plate regions__add units__add" onSubmit={submit} style={{ '--name-columns': languages.length } as React.CSSProperties}>
      <NameInputs languages={languages} value={draft} onChange={setDraft} noun="unit" placeholder={{ en: 'e.g. Foot', it: 'es. Piede' }} invalid={!!error} />
      <div className="field">
        <label className="field__label" htmlFor={`${id}-abbr`}>
          Abbreviation
        </label>
        <input id={`${id}-abbr`} className="input" value={abbreviation} placeholder="ft" onChange={(e) => setAbbreviation(e.target.value)} />
      </div>
      <div className="field">
        <label className="field__label" htmlFor={`${id}-factor`}>
          Metres per unit
        </label>
        <input id={`${id}-factor`} className="input" inputMode="decimal" value={factor} placeholder="0.3048" onChange={(e) => setFactor(e.target.value)} />
      </div>
      <button type="submit" className="btn btn--primary" disabled={saving || Object.keys(names).length === 0 || !abbreviation.trim() || !factor.trim()}>
        <Plus aria-hidden="true" /> Add unit
      </button>
      <p className="regions__add-feedback" aria-live="polite">
        {error && <span className="field__error">{error}</span>}
        {notice && <span className="muted">{notice}</span>}
      </p>
    </form>
  )
}

function UnitRow({ unit, languages, onChanged }: { unit: UnitOfMeasure; languages: Language[]; onChanged: () => Promise<unknown> }) {
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState<NameDraft>(unit.names)
  const [abbreviation, setAbbreviation] = useState(unit.abbreviation)
  const [factor, setFactor] = useState(String(unit.factor))
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const run = async (action: () => Promise<unknown>) => {
    setBusy(true)
    setError(null)
    try {
      await action()
      await onChanged()
      return true
    } catch (e) {
      setError(message(e))
      return false
    } finally {
      setBusy(false)
    }
  }

  const path = { params: { path: { id: unit.id } } }
  const save = async () => {
    const body = { names: namesOf(draft), abbreviation, factor: parseFactor(factor) }
    if (await run(async () => unwrap(await api.PUT('/api/units/{id}', { ...path, body })))) setEditing(false)
  }
  const cancel = () => {
    setEditing(false)
    setDraft(unit.names)
    setAbbreviation(unit.abbreviation)
    setFactor(String(unit.factor))
    setError(null)
  }
  const factorChanged = parseFactor(factor) !== unit.factor

  return (
    <tr aria-busy={busy}>
      <NameCells
        languages={languages}
        names={unit.names}
        editing={editing}
        draft={draft}
        onDraft={setDraft}
        onSave={() => void save()}
        onCancel={cancel}
        label={unit.name}
        note={
          error && (
            <p className="field__error" role="alert">
              {error}
            </p>
          )
        }
      />
      <td data-label="Abbreviation">
        {editing ? (
          <input className="input units__input" aria-label={`Abbreviation of ${unit.name}`} value={abbreviation} onChange={(e) => setAbbreviation(e.target.value)} />
        ) : (
          unit.abbreviation
        )}
      </td>
      <td className="regions__count" data-label="Metres per unit">
        {editing ? (
          <>
            <input className="input units__input" inputMode="decimal" aria-label={`Metres per ${unit.name}`} value={factor} onChange={(e) => setFactor(e.target.value)} />
            {factorChanged && unit.artefactCount > 0 && (
              <p className="field__hint">
                Changes the value in metres of {unit.artefactCount.toLocaleString()} artefact{unit.artefactCount === 1 ? '' : 's'}.
              </p>
            )}
          </>
        ) : (
          unit.factor
        )}
      </td>
      <td className="regions__count" data-label="Artefacts">
        {unit.artefactCount.toLocaleString()}
      </td>
      <td className="regions__actions">
        {editing ? (
          <>
            <button type="button" className="btn btn--icon btn--sm" aria-label="Save unit" disabled={busy} onClick={() => void save()}>
              <Check aria-hidden="true" />
            </button>
            <button type="button" className="btn btn--icon btn--sm" aria-label="Cancel editing" onClick={cancel}>
              <X aria-hidden="true" />
            </button>
          </>
        ) : (
          <button type="button" className="btn btn--sm" onClick={() => setEditing(true)}>
            <Pencil aria-hidden="true" /> Edit
          </button>
        )}
        <button
          type="button"
          className="btn btn--sm btn--danger"
          disabled={busy}
          onClick={() => {
            if (window.confirm(`Delete the unit “${unit.name}”?`)) void run(async () => unwrap(await api.DELETE('/api/units/{id}', path)))
          }}
        >
          <Trash2 aria-hidden="true" /> Delete
        </button>
      </td>
    </tr>
  )
}
