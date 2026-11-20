import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Pencil, Plus, Search, Trash2, X } from 'lucide-react'
import { useEffect, useId, useMemo, useState, type FormEvent } from 'react'
import { api, type Region } from '../api/client'
import { ApiError, unwrap } from '../api/problem'
import './RegionsPage.css'

function message(e: unknown): string {
  if (e instanceof ApiError) return Object.values(e.fieldErrors).flat()[0] ?? e.message
  return e instanceof Error ? e.message : 'Something went wrong.'
}

/** Manages the region reference list used by provenance. */
export function RegionsPage() {
  const queryClient = useQueryClient()
  const searchId = useId()
  const [search, setSearch] = useState('')
  const { data: regions = [], isPending, isError, error } = useQuery({
    queryKey: ['regions', 'all'],
    queryFn: async () => unwrap(await api.GET('/api/regions')),
  })

  useEffect(() => {
    document.title = 'Regions · Artefacts'
  }, [])

  const refresh = () => Promise.all([queryClient.invalidateQueries({ queryKey: ['regions'] }), queryClient.invalidateQueries({ queryKey: ['count', 'regions'] })])

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase()
    return q ? regions.filter((r) => r.name.toLowerCase().includes(q) || r.parentName?.toLowerCase().includes(q)) : regions
  }, [regions, search])

  return (
    <div className="regions">
      <div className="page-head">
        <div className="page-head__titles">
          <h1>Regions</h1>
          <p className="page-head__sub">The places provenance refers to. Renaming a region updates every artefact that uses it.</p>
        </div>
      </div>

      <AddRegion regions={regions} onAdded={refresh} />

      <div className="toolbar__row regions__filter">
        <label className="search" htmlFor={searchId}>
          <span className="visually-hidden">Filter regions</span>
          <Search aria-hidden="true" />
          <input id={searchId} className="input input--search" type="search" placeholder="Filter regions" value={search} onChange={(e) => setSearch(e.target.value)} />
        </label>
      </div>

      {isPending ? (
        <div className="data-state" aria-busy="true">
          Loading…
        </div>
      ) : isError ? (
        <div className="data-state data-state--error" role="alert">
          Could not load regions: {error.message}
        </div>
      ) : visible.length === 0 ? (
        <div className="plate empty">
          <strong>{regions.length === 0 ? 'No regions yet.' : 'No regions match.'}</strong>
          <p>{regions.length === 0 ? 'Add the first region above, or add regions while editing provenance.' : 'Try a different filter.'}</p>
        </div>
      ) : (
        <div className="table-wrap">
          <table className="table regions__table">
            <caption className="visually-hidden">Regions</caption>
            <thead>
              <tr>
                <th scope="col">Name</th>
                <th scope="col">Part of</th>
                <th scope="col">
                  <span className="visually-hidden">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {visible.map((region) => (
                <RegionRow key={region.id} region={region} regions={regions} onChanged={refresh} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function ParentSelect({ id, value, regions, exclude, onChange, disabled }: { id?: string; value: string; regions: Region[]; exclude?: string; onChange: (v: string) => void; disabled?: boolean }) {
  return (
    <select id={id} className="select" value={value} disabled={disabled} onChange={(e) => onChange(e.target.value)}>
      <option value="">— None —</option>
      {regions
        .filter((r) => r.id !== exclude)
        .map((r) => (
          <option key={r.id} value={r.id}>
            {r.name}
          </option>
        ))}
    </select>
  )
}

function AddRegion({ regions, onAdded }: { regions: Region[]; onAdded: () => Promise<unknown> }) {
  const nameId = useId()
  const parentId = useId()
  const [name, setName] = useState('')
  const [parent, setParent] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    setNotice(null)
    try {
      const result = await api.POST('/api/regions', { body: { name, parentId: parent || null } })
      const region = unwrap(result)
      setNotice(result.response.status === 200 ? `“${region.name}” already exists.` : `Added “${region.name}”.`)
      setName('')
      setParent('')
      await onAdded()
    } catch (err) {
      setError(message(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="plate regions__add" onSubmit={submit}>
      <div className={`field${error ? ' field--invalid' : ''}`}>
        <label className="field__label" htmlFor={nameId}>
          New region
        </label>
        <input id={nameId} className="input" value={name} required onChange={(e) => setName(e.target.value)} placeholder="e.g. Flanders" aria-invalid={!!error} />
      </div>
      <div className="field">
        <label className="field__label" htmlFor={parentId}>
          Part of
        </label>
        <ParentSelect id={parentId} value={parent} regions={regions} onChange={setParent} />
      </div>
      <button type="submit" className="btn btn--primary" disabled={saving || !name.trim()}>
        <Plus aria-hidden="true" /> Add region
      </button>
      <p className="regions__add-feedback" aria-live="polite">
        {error && <span className="field__error">{error}</span>}
        {notice && <span className="muted">{notice}</span>}
      </p>
    </form>
  )
}

function RegionRow({ region, regions, onChanged }: { region: Region; regions: Region[]; onChanged: () => Promise<unknown> }) {
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(region.name)
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

  const path = { params: { path: { id: region.id } } }
  const rename = async (e: FormEvent) => {
    e.preventDefault()
    if (await run(async () => unwrap(await api.PUT('/api/regions/{id}', { ...path, body: { name } })))) setEditing(false)
  }

  return (
    <tr aria-busy={busy}>
      <td>
        {editing ? (
          <form className="regions__rename" onSubmit={rename}>
            <input className="input" aria-label={`New name for ${region.name}`} value={name} autoFocus onChange={(e) => setName(e.target.value)} />
            <button type="submit" className="btn btn--icon btn--sm" aria-label="Save name" disabled={busy}>
              <Check aria-hidden="true" />
            </button>
            <button
              type="button"
              className="btn btn--icon btn--sm"
              aria-label="Cancel renaming"
              onClick={() => {
                setEditing(false)
                setName(region.name)
                setError(null)
              }}
            >
              <X aria-hidden="true" />
            </button>
          </form>
        ) : (
          <span className="cell-title">{region.name}</span>
        )}
        {error && (
          <p className="field__error" role="alert">
            {error}
          </p>
        )}
      </td>
      <td className="regions__parent">
        <ParentSelect
          value={region.parentId ?? ''}
          regions={regions}
          exclude={region.id}
          disabled={busy}
          onChange={(v) => void run(async () => unwrap(await api.PUT('/api/regions/{id}/parent', { ...path, body: { parentId: v || null } })))}
        />
      </td>
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
            if (window.confirm(`Delete the region “${region.name}”?`)) void run(async () => unwrap(await api.DELETE('/api/regions/{id}', path)))
          }}
        >
          <Trash2 aria-hidden="true" /> Delete
        </button>
      </td>
    </tr>
  )
}
