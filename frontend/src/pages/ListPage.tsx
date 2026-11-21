import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Pencil, Plus, Search, Trash2, X } from 'lucide-react'
import { useEffect, useId, useMemo, useState, type FormEvent, type KeyboardEvent } from 'react'
import { api, type Language, type ListSlug, type ReferenceEntry } from '../api/client'
import { unwrap } from '../api/problem'
import { useContentLanguages } from '../app/languages'
import { listConfigs, message, type ListConfig } from './lists'
import { namesOf, type NameDraft } from './names'
import { NameCells, NameInputs } from './referenceNames'
import { ReferenceTabs } from './ReferenceTabs'
import './RegionsPage.css'

/** Manages one of the lists artefacts pick values from: list with usage counts, filter, add, rename and delete. */
export function ListPage({ list }: { list: ListSlug }) {
  const config = listConfigs[list]
  const queryClient = useQueryClient()
  const searchId = useId()
  const [search, setSearch] = useState('')
  const { data: languages = [] } = useContentLanguages()
  const { data: entries = [], isPending, isError, error } = useQuery({
    queryKey: ['lists', list, 'all'],
    queryFn: async () => unwrap(await api.GET('/api/lists/{list}', { params: { path: { list } } })),
  })

  useEffect(() => {
    document.title = `${config.title} · Artefacts`
  }, [config.title])

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['lists', list] }),
      queryClient.invalidateQueries({ queryKey: ['operation'] }),
    ])

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase()
    if (!q) return entries
    return entries.filter((e) => [e.name, ...Object.values(e.names ?? {})].some((n) => n.toLowerCase().includes(q)))
  }, [entries, search])

  const plural = config.title.toLowerCase()
  return (
    <div className="regions">
      <ReferenceTabs />
      <div className="page-head">
        <div className="page-head__titles">
          <h1>{config.title}</h1>
          <p className="page-head__sub">{config.description}</p>
        </div>
      </div>

      <AddEntry config={config} languages={languages} onAdded={refresh} />

      <div className="toolbar__row regions__filter">
        <label className="search" htmlFor={searchId}>
          <span className="visually-hidden">Filter {plural}</span>
          <Search aria-hidden="true" />
          <input
            id={searchId}
            className="input input--search"
            type="search"
            placeholder={config.translated ? `Filter ${plural} in any language` : `Filter ${plural}`}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
      </div>

      {isPending ? (
        <div className="data-state" aria-busy="true">
          Loading…
        </div>
      ) : isError ? (
        <div className="data-state data-state--error" role="alert">
          Could not load {plural}: {error.message}
        </div>
      ) : visible.length === 0 ? (
        <div className="plate empty">
          <strong>{entries.length === 0 ? `No ${plural} yet.` : `No ${plural} match.`}</strong>
          <p>{entries.length === 0 ? `Add the first ${config.noun} above, or add one while editing an artefact.` : 'Try a different filter.'}</p>
        </div>
      ) : (
        <div className="table-wrap">
          <table className="table regions__table">
            <caption className="visually-hidden">{config.title}</caption>
            <thead>
              <tr>
                {config.translated ? (
                  languages.map((l) => (
                    <th key={l.code} scope="col">
                      {l.name}
                    </th>
                  ))
                ) : (
                  <th scope="col">Name</th>
                )}
                <th scope="col" className="regions__count">
                  Artefacts
                </th>
                <th scope="col">
                  <span className="visually-hidden">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {visible.map((entry) => (
                <EntryRow key={entry.id} config={config} entry={entry} languages={languages} onChanged={refresh} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function AddEntry({ config, languages, onAdded }: { config: ListConfig; languages: Language[]; onAdded: () => Promise<unknown> }) {
  const nameId = useId()
  const [draft, setDraft] = useState<NameDraft>({})
  const [name, setName] = useState('')
  const names = namesOf(draft)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const empty = config.translated ? Object.keys(names).length === 0 : name.trim() === ''

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    setNotice(null)
    try {
      const body = config.translated ? { names } : { name: name.trim() }
      const result = await api.POST('/api/lists/{list}', { params: { path: { list: config.slug } }, body })
      const entry = unwrap(result)
      setNotice(result.response.status === 200 ? `“${entry.name}” already exists.` : `Added “${entry.name}”.`)
      setDraft({})
      setName('')
      await onAdded()
    } catch (err) {
      setError(message(err))
    } finally {
      setSaving(false)
    }
  }

  const columns = config.translated ? languages.length : 1
  return (
    <form className="plate regions__add regions__add--single" onSubmit={submit} style={{ '--name-columns': columns } as React.CSSProperties}>
      {config.translated ? (
        <NameInputs languages={languages} value={draft} onChange={setDraft} noun={config.noun} placeholder={config.placeholder} invalid={!!error} />
      ) : (
        <div className={`field${error ? ' field--invalid' : ''}`}>
          <label className="field__label" htmlFor={nameId}>
            New {config.noun}
          </label>
          <input id={nameId} className="input" value={name} placeholder={config.placeholder['']} aria-invalid={!!error} onChange={(e) => setName(e.target.value)} />
        </div>
      )}
      <button type="submit" className="btn btn--primary" disabled={saving || empty}>
        <Plus aria-hidden="true" /> Add {config.noun}
      </button>
      <p className="regions__add-feedback" aria-live="polite">
        {error && <span className="field__error">{error}</span>}
        {notice && <span className="muted">{notice}</span>}
      </p>
    </form>
  )
}

function EntryRow({
  config,
  entry,
  languages,
  onChanged,
}: {
  config: ListConfig
  entry: ReferenceEntry
  languages: Language[]
  onChanged: () => Promise<unknown>
}) {
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState<NameDraft>(entry.names ?? {})
  const [name, setName] = useState(entry.name)
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

  const path = { params: { path: { list: config.slug, id: entry.id } } }
  const save = async () => {
    const body = config.translated ? { names: namesOf(draft) } : { name: name.trim() }
    if (await run(async () => unwrap(await api.PUT('/api/lists/{list}/{id}', { ...path, body })))) setEditing(false)
  }
  const cancel = () => {
    setEditing(false)
    setDraft(entry.names ?? {})
    setName(entry.name)
    setError(null)
  }
  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault()
      void save()
    } else if (e.key === 'Escape') cancel()
  }
  const note = error && (
    <p className="field__error" role="alert">
      {error}
    </p>
  )

  return (
    <tr aria-busy={busy}>
      {config.translated ? (
        <NameCells
          languages={languages}
          names={entry.names ?? {}}
          editing={editing}
          draft={draft}
          onDraft={setDraft}
          onSave={() => void save()}
          onCancel={cancel}
          label={entry.name}
          note={note}
        />
      ) : (
        <td className="names__cell" data-label="Name">
          {editing ? (
            <input className="input" aria-label={`Name for ${entry.name}`} value={name} autoFocus onChange={(e) => setName(e.target.value)} onKeyDown={onKeyDown} />
          ) : (
            <span className="cell-title">{entry.name}</span>
          )}
          {note}
        </td>
      )}
      <td className="regions__count" data-label="Artefacts">
        {entry.artefactCount.toLocaleString()}
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
            if (window.confirm(`Delete the ${config.noun} “${entry.name}”?`)) void run(async () => unwrap(await api.DELETE('/api/lists/{list}/{id}', path)))
          }}
        >
          <Trash2 aria-hidden="true" /> Delete
        </button>
      </td>
    </tr>
  )
}
