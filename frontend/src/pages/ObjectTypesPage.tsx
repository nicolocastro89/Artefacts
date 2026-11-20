import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Pencil, Plus, Search, Trash2, X } from 'lucide-react'
import { useEffect, useId, useMemo, useState, type FormEvent } from 'react'
import { api, type Language, type ObjectType } from '../api/client'
import { ApiError, unwrap } from '../api/problem'
import { useContentLanguages } from '../app/languages'
import { namesOf, type NameDraft } from './names'
import { NameCells, NameInputs } from './referenceNames'
import { ReferenceTabs } from './ReferenceTabs'
import './RegionsPage.css'

function message(e: unknown): string {
  if (e instanceof ApiError) return Object.values(e.fieldErrors).flat()[0] ?? e.message
  return e instanceof Error ? e.message : 'Something went wrong.'
}

/** Manages the object type reference list (Ceramics, Carpets, …) used to classify artefacts. */
export function ObjectTypesPage() {
  const queryClient = useQueryClient()
  const searchId = useId()
  const [search, setSearch] = useState('')
  const { data: languages = [] } = useContentLanguages()
  const { data: types = [], isPending, isError, error } = useQuery({
    queryKey: ['object-types', 'all'],
    queryFn: async () => unwrap(await api.GET('/api/object-types')),
  })

  useEffect(() => {
    document.title = 'Object types · Artefacts'
  }, [])

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['object-types'] }),
      queryClient.invalidateQueries({ queryKey: ['count', 'object-types'] }),
      queryClient.invalidateQueries({ queryKey: ['operation'] }),
    ])

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase()
    return q ? types.filter((t) => Object.values(t.names).some((n) => n.toLowerCase().includes(q))) : types
  }, [types, search])

  return (
    <div className="regions">
      <ReferenceTabs />
      <div className="page-head">
        <div className="page-head__titles">
          <h1>Object types</h1>
          <p className="page-head__sub">The kinds of object in the collection, named in each language. Renaming a type updates every artefact of that type.</p>
        </div>
      </div>

      <AddObjectType languages={languages} onAdded={refresh} />

      <div className="toolbar__row regions__filter">
        <label className="search" htmlFor={searchId}>
          <span className="visually-hidden">Filter object types</span>
          <Search aria-hidden="true" />
          <input id={searchId} className="input input--search" type="search" placeholder="Filter object types in any language" value={search} onChange={(e) => setSearch(e.target.value)} />
        </label>
      </div>

      {isPending ? (
        <div className="data-state" aria-busy="true">
          Loading…
        </div>
      ) : isError ? (
        <div className="data-state data-state--error" role="alert">
          Could not load object types: {error.message}
        </div>
      ) : visible.length === 0 ? (
        <div className="plate empty">
          <strong>{types.length === 0 ? 'No object types yet.' : 'No object types match.'}</strong>
          <p>{types.length === 0 ? 'Add the first object type above, or add one while editing an artefact.' : 'Try a different filter.'}</p>
        </div>
      ) : (
        <div className="table-wrap">
          <table className="table regions__table">
            <caption className="visually-hidden">Object types</caption>
            <thead>
              <tr>
                {languages.map((l) => (
                  <th key={l.code} scope="col">
                    {l.name}
                  </th>
                ))}
                <th scope="col" className="regions__count">
                  Artefacts
                </th>
                <th scope="col">
                  <span className="visually-hidden">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {visible.map((type) => (
                <ObjectTypeRow key={type.id} type={type} languages={languages} onChanged={refresh} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function AddObjectType({ languages, onAdded }: { languages: Language[]; onAdded: () => Promise<unknown> }) {
  const [draft, setDraft] = useState<NameDraft>({})
  const names = namesOf(draft)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    setNotice(null)
    try {
      const result = await api.POST('/api/object-types', { body: { names } })
      const type = unwrap(result)
      setNotice(result.response.status === 200 ? `“${type.name}” already exists.` : `Added “${type.name}”.`)
      setDraft({})
      await onAdded()
    } catch (err) {
      setError(message(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="plate regions__add regions__add--single" onSubmit={submit} style={{ '--name-columns': languages.length } as React.CSSProperties}>
      <NameInputs languages={languages} value={draft} onChange={setDraft} noun="object type" placeholder={{ en: 'e.g. Ceramics', it: 'es. Ceramiche' }} invalid={!!error} />
      <button type="submit" className="btn btn--primary" disabled={saving || Object.keys(names).length === 0}>
        <Plus aria-hidden="true" /> Add object type
      </button>
      <p className="regions__add-feedback" aria-live="polite">
        {error && <span className="field__error">{error}</span>}
        {notice && <span className="muted">{notice}</span>}
      </p>
    </form>
  )
}

function ObjectTypeRow({ type, languages, onChanged }: { type: ObjectType; languages: Language[]; onChanged: () => Promise<unknown> }) {
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState<NameDraft>(type.names)
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

  const path = { params: { path: { id: type.id } } }
  const save = async () => {
    if (await run(async () => unwrap(await api.PUT('/api/object-types/{id}', { ...path, body: { names: namesOf(draft) } })))) setEditing(false)
  }
  const cancel = () => {
    setEditing(false)
    setDraft(type.names)
    setError(null)
  }

  return (
    <tr aria-busy={busy}>
      <NameCells
        languages={languages}
        names={type.names}
        editing={editing}
        draft={draft}
        onDraft={setDraft}
        onSave={() => void save()}
        onCancel={cancel}
        label={type.name}
        note={
          error && (
            <p className="field__error" role="alert">
              {error}
            </p>
          )
        }
      />
      <td className="regions__count" data-label="Artefacts">
        {type.artefactCount.toLocaleString()}
      </td>
      <td className="regions__actions">
        {editing ? (
          <>
            <button type="button" className="btn btn--icon btn--sm" aria-label="Save names" disabled={busy} onClick={() => void save()}>
              <Check aria-hidden="true" />
            </button>
            <button type="button" className="btn btn--icon btn--sm" aria-label="Cancel renaming" onClick={cancel}>
              <X aria-hidden="true" />
            </button>
          </>
        ) : (
          <button type="button" className="btn btn--sm" onClick={() => setEditing(true)}>
            <Pencil aria-hidden="true" /> Rename
          </button>
        )}
        <button
          type="button"
          className="btn btn--sm btn--danger"
          disabled={busy}
          onClick={() => {
            if (window.confirm(`Delete the object type “${type.name}”?`)) void run(async () => unwrap(await api.DELETE('/api/object-types/{id}', path)))
          }}
        >
          <Trash2 aria-hidden="true" /> Delete
        </button>
      </td>
    </tr>
  )
}
