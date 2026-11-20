import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Pencil, Plus, Search, Trash2, X } from 'lucide-react'
import { useEffect, useId, useMemo, useState, type FormEvent } from 'react'
import { api, type Language, type Region } from '../api/client'
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

/** Manages the region reference list used by provenance and places of production, with a name per language. */
export function RegionsPage() {
  const queryClient = useQueryClient()
  const searchId = useId()
  const [search, setSearch] = useState('')
  const { data: languages = [] } = useContentLanguages()
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
    return q
      ? regions.filter((r) => Object.values(r.names).some((n) => n.toLowerCase().includes(q)) || r.parentName?.toLowerCase().includes(q))
      : regions
  }, [regions, search])

  return (
    <div className="regions">
      <ReferenceTabs />
      <div className="page-head">
        <div className="page-head__titles">
          <h1>Regions</h1>
          <p className="page-head__sub">The places provenance and places of production refer to, named in each language. Renaming a region updates every artefact that uses it.</p>
        </div>
      </div>

      <AddRegion regions={regions} languages={languages} onAdded={refresh} />

      <div className="toolbar__row regions__filter">
        <label className="search" htmlFor={searchId}>
          <span className="visually-hidden">Filter regions</span>
          <Search aria-hidden="true" />
          <input id={searchId} className="input input--search" type="search" placeholder="Filter regions in any language" value={search} onChange={(e) => setSearch(e.target.value)} />
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
                {languages.map((l) => (
                  <th key={l.code} scope="col">
                    {l.name}
                  </th>
                ))}
                <th scope="col">Part of</th>
                <th scope="col">
                  <span className="visually-hidden">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {visible.map((region) => (
                <RegionRow key={region.id} region={region} regions={regions} languages={languages} onChanged={refresh} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function ParentSelect({ id, value, regions, exclude, onChange, disabled, label }: { id?: string; value: string; regions: Region[]; exclude?: string; onChange: (v: string) => void; disabled?: boolean; label?: string }) {
  return (
    <select id={id} className="select" value={value} disabled={disabled} aria-label={label} onChange={(e) => onChange(e.target.value)}>
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

function AddRegion({ regions, languages, onAdded }: { regions: Region[]; languages: Language[]; onAdded: () => Promise<unknown> }) {
  const parentId = useId()
  const [draft, setDraft] = useState<NameDraft>({})
  const [parent, setParent] = useState('')
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
      const result = await api.POST('/api/regions', { body: { names, parentId: parent || null } })
      const region = unwrap(result)
      setNotice(result.response.status === 200 ? `“${region.name}” already exists.` : `Added “${region.name}”.`)
      setDraft({})
      setParent('')
      await onAdded()
    } catch (err) {
      setError(message(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="plate regions__add" onSubmit={submit} style={{ '--name-columns': languages.length } as React.CSSProperties}>
      <NameInputs languages={languages} value={draft} onChange={setDraft} noun="region" placeholder={{ en: 'e.g. Flanders', it: 'es. Fiandre' }} invalid={!!error} />
      <div className="field">
        <label className="field__label" htmlFor={parentId}>
          Part of
        </label>
        <ParentSelect id={parentId} value={parent} regions={regions} onChange={setParent} />
      </div>
      <button type="submit" className="btn btn--primary" disabled={saving || Object.keys(names).length === 0}>
        <Plus aria-hidden="true" /> Add region
      </button>
      <p className="regions__add-feedback" aria-live="polite">
        {error && <span className="field__error">{error}</span>}
        {notice && <span className="muted">{notice}</span>}
      </p>
    </form>
  )
}

function RegionRow({ region, regions, languages, onChanged }: { region: Region; regions: Region[]; languages: Language[]; onChanged: () => Promise<unknown> }) {
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState<NameDraft>(region.names)
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
  const save = async () => {
    if (await run(async () => unwrap(await api.PUT('/api/regions/{id}', { ...path, body: { names: namesOf(draft) } })))) setEditing(false)
  }
  const cancel = () => {
    setEditing(false)
    setDraft(region.names)
    setError(null)
  }

  return (
      <tr aria-busy={busy}>
        <NameCells
          languages={languages}
          names={region.names}
          editing={editing}
          draft={draft}
          onDraft={setDraft}
          onSave={() => void save()}
          onCancel={cancel}
          label={region.name}
          note={
            error && (
              <p className="field__error" role="alert">
                {error}
              </p>
            )
          }
        />
        <td className="regions__parent" data-label="Part of">
          <ParentSelect
            value={region.parentId ?? ''}
            regions={regions}
            exclude={region.id}
            disabled={busy}
            label={`${region.name} is part of`}
            onChange={(v) => void run(async () => unwrap(await api.PUT('/api/regions/{id}/parent', { ...path, body: { parentId: v || null } })))}
          />
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
              if (window.confirm(`Delete the region “${region.name}”?`)) void run(async () => unwrap(await api.DELETE('/api/regions/{id}', path)))
            }}
          >
            <Trash2 aria-hidden="true" /> Delete
          </button>
        </td>
      </tr>
  )
}
