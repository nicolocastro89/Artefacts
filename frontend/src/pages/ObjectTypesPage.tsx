import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Pencil, Plus, Search, Trash2, X } from 'lucide-react'
import { useEffect, useId, useMemo, useState, type FormEvent } from 'react'
import { api, type ObjectType } from '../api/client'
import { ApiError, unwrap } from '../api/problem'
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
    return q ? types.filter((t) => t.name.toLowerCase().includes(q)) : types
  }, [types, search])

  return (
    <div className="regions">
      <ReferenceTabs />
      <div className="page-head">
        <div className="page-head__titles">
          <h1>Object types</h1>
          <p className="page-head__sub">The kinds of object in the collection. Renaming a type updates every artefact of that type.</p>
        </div>
      </div>

      <AddObjectType onAdded={refresh} />

      <div className="toolbar__row regions__filter">
        <label className="search" htmlFor={searchId}>
          <span className="visually-hidden">Filter object types</span>
          <Search aria-hidden="true" />
          <input id={searchId} className="input input--search" type="search" placeholder="Filter object types" value={search} onChange={(e) => setSearch(e.target.value)} />
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
                <th scope="col">Name</th>
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
                <ObjectTypeRow key={type.id} type={type} onChanged={refresh} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function AddObjectType({ onAdded }: { onAdded: () => Promise<unknown> }) {
  const nameId = useId()
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    setNotice(null)
    try {
      const result = await api.POST('/api/object-types', { body: { name } })
      const type = unwrap(result)
      setNotice(result.response.status === 200 ? `“${type.name}” already exists.` : `Added “${type.name}”.`)
      setName('')
      await onAdded()
    } catch (err) {
      setError(message(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="plate regions__add regions__add--single" onSubmit={submit}>
      <div className={`field${error ? ' field--invalid' : ''}`}>
        <label className="field__label" htmlFor={nameId}>
          New object type
        </label>
        <input id={nameId} className="input" value={name} required onChange={(e) => setName(e.target.value)} placeholder="e.g. Ceramics" aria-invalid={!!error} />
      </div>
      <button type="submit" className="btn btn--primary" disabled={saving || !name.trim()}>
        <Plus aria-hidden="true" /> Add object type
      </button>
      <p className="regions__add-feedback" aria-live="polite">
        {error && <span className="field__error">{error}</span>}
        {notice && <span className="muted">{notice}</span>}
      </p>
    </form>
  )
}

function ObjectTypeRow({ type, onChanged }: { type: ObjectType; onChanged: () => Promise<unknown> }) {
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(type.name)
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
  const rename = async (e: FormEvent) => {
    e.preventDefault()
    if (await run(async () => unwrap(await api.PUT('/api/object-types/{id}', { ...path, body: { name } })))) setEditing(false)
  }

  return (
    <tr aria-busy={busy}>
      <td>
        {editing ? (
          <form className="regions__rename" onSubmit={rename}>
            <input className="input" aria-label={`New name for ${type.name}`} value={name} autoFocus onChange={(e) => setName(e.target.value)} />
            <button type="submit" className="btn btn--icon btn--sm" aria-label="Save name" disabled={busy}>
              <Check aria-hidden="true" />
            </button>
            <button
              type="button"
              className="btn btn--icon btn--sm"
              aria-label="Cancel renaming"
              onClick={() => {
                setEditing(false)
                setName(type.name)
                setError(null)
              }}
            >
              <X aria-hidden="true" />
            </button>
          </form>
        ) : (
          <span className="cell-title">{type.name}</span>
        )}
        {error && (
          <p className="field__error" role="alert">
            {error}
          </p>
        )}
      </td>
      <td className="regions__count">{type.artefactCount.toLocaleString()}</td>
      <td className="regions__actions">
        {!editing && (
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
