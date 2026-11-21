import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Pencil, Plus, Search, Trash2, X } from 'lucide-react'
import { useEffect, useId, useMemo, useState, type FormEvent, type KeyboardEvent } from 'react'
import { api, type Party, type PartyKind } from '../api/client'
import { unwrap } from '../api/problem'
import { message, partyKindLabels } from './lists'
import { ReferenceTabs } from './ReferenceTabs'
import './RegionsPage.css'

function KindSelect({ value, onChange, label, id }: { value: PartyKind; onChange: (kind: PartyKind) => void; label?: string; id?: string }) {
  return (
    <select id={id} className="input" aria-label={label} value={value} onChange={(e) => onChange(e.target.value as PartyKind)}>
      <option value="organisation">{partyKindLabels.organisation}</option>
      <option value="person">{partyKindLabels.person}</option>
    </select>
  )
}

/**
 * Manages the people and organisations the collection deals with: the sources artefacts were acquired from and the
 * holders that keep them.
 */
export function PartiesPage() {
  const queryClient = useQueryClient()
  const searchId = useId()
  const kindId = useId()
  const [search, setSearch] = useState('')
  const [kind, setKind] = useState<PartyKind | ''>('')
  const { data: parties = [], isPending, isError, error } = useQuery({
    queryKey: ['parties', 'all'],
    queryFn: async () => unwrap(await api.GET('/api/parties')),
  })

  useEffect(() => {
    document.title = 'Parties · Artefacts'
  }, [])

  const refresh = () =>
    Promise.all([queryClient.invalidateQueries({ queryKey: ['parties'] }), queryClient.invalidateQueries({ queryKey: ['operation'] })])

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase()
    return parties.filter((p) => (kind === '' || p.kind === kind) && (!q || p.name.toLowerCase().includes(q)))
  }, [parties, search, kind])

  return (
    <div className="regions">
      <ReferenceTabs />
      <div className="page-head">
        <div className="page-head__titles">
          <h1>Parties</h1>
          <p className="page-head__sub">
            People and organisations: the auction houses, dealers and sellers artefacts were acquired from, and the holders that keep them.
          </p>
        </div>
      </div>

      <AddParty onAdded={refresh} />

      <div className="toolbar__row regions__filter">
        <label className="search" htmlFor={searchId}>
          <span className="visually-hidden">Filter parties</span>
          <Search aria-hidden="true" />
          <input id={searchId} className="input input--search" type="search" placeholder="Filter parties" value={search} onChange={(e) => setSearch(e.target.value)} />
        </label>
        <label className="visually-hidden" htmlFor={kindId}>
          Filter by kind
        </label>
        <select id={kindId} className="input parties__kind-filter" value={kind} onChange={(e) => setKind(e.target.value as PartyKind | '')}>
          <option value="">All kinds</option>
          <option value="organisation">Organisations</option>
          <option value="person">People</option>
        </select>
      </div>

      {isPending ? (
        <div className="data-state" aria-busy="true">
          Loading…
        </div>
      ) : isError ? (
        <div className="data-state data-state--error" role="alert">
          Could not load parties: {error.message}
        </div>
      ) : visible.length === 0 ? (
        <div className="plate empty">
          <strong>{parties.length === 0 ? 'No parties yet.' : 'No parties match.'}</strong>
          <p>{parties.length === 0 ? 'Add the first party above, or add one while editing an artefact.' : 'Try a different filter.'}</p>
        </div>
      ) : (
        <div className="table-wrap">
          <table className="table regions__table">
            <caption className="visually-hidden">Parties</caption>
            <thead>
              <tr>
                <th scope="col">Name</th>
                <th scope="col">Kind</th>
                <th scope="col" className="regions__count">
                  Source of
                </th>
                <th scope="col" className="regions__count">
                  Holder of
                </th>
                <th scope="col">
                  <span className="visually-hidden">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {visible.map((party) => (
                <PartyRow key={party.id} party={party} onChanged={refresh} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function AddParty({ onAdded }: { onAdded: () => Promise<unknown> }) {
  const nameId = useId()
  const kindId = useId()
  const [name, setName] = useState('')
  const [kind, setKind] = useState<PartyKind>('organisation')
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    setNotice(null)
    try {
      const result = await api.POST('/api/parties', { body: { name: name.trim(), kind } })
      const party = unwrap(result)
      setNotice(result.response.status === 200 ? `“${party.name}” already exists.` : `Added “${party.name}”.`)
      setName('')
      await onAdded()
    } catch (err) {
      setError(message(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="plate regions__add regions__add--single" onSubmit={submit} style={{ '--name-columns': 2 } as React.CSSProperties}>
      <div className={`field${error ? ' field--invalid' : ''}`}>
        <label className="field__label" htmlFor={nameId}>
          New party
        </label>
        <input id={nameId} className="input" value={name} placeholder="e.g. Sotheby's" aria-invalid={!!error} onChange={(e) => setName(e.target.value)} />
      </div>
      <div className="field">
        <label className="field__label" htmlFor={kindId}>
          Kind
        </label>
        <KindSelect id={kindId} value={kind} onChange={setKind} />
      </div>
      <button type="submit" className="btn btn--primary" disabled={saving || name.trim() === ''}>
        <Plus aria-hidden="true" /> Add party
      </button>
      <p className="regions__add-feedback" aria-live="polite">
        {error && <span className="field__error">{error}</span>}
        {notice && <span className="muted">{notice}</span>}
      </p>
    </form>
  )
}

function PartyRow({ party, onChanged }: { party: Party; onChanged: () => Promise<unknown> }) {
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(party.name)
  const [kind, setKind] = useState<PartyKind>(party.kind)
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

  const path = { params: { path: { id: party.id } } }
  const save = async () => {
    if (await run(async () => unwrap(await api.PUT('/api/parties/{id}', { ...path, body: { name: name.trim(), kind } })))) setEditing(false)
  }
  const cancel = () => {
    setEditing(false)
    setName(party.name)
    setKind(party.kind)
    setError(null)
  }
  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault()
      void save()
    } else if (e.key === 'Escape') cancel()
  }

  return (
    <tr aria-busy={busy}>
      <td className="names__cell" data-label="Name">
        {editing ? (
          <input className="input" aria-label={`Name for ${party.name}`} value={name} autoFocus onChange={(e) => setName(e.target.value)} onKeyDown={onKeyDown} />
        ) : (
          <span className="cell-title">{party.name}</span>
        )}
        {error && (
          <p className="field__error" role="alert">
            {error}
          </p>
        )}
      </td>
      <td data-label="Kind">{editing ? <KindSelect label={`Kind of ${party.name}`} value={kind} onChange={setKind} /> : partyKindLabels[party.kind]}</td>
      <td className="regions__count" data-label="Source of">
        {party.sourceCount.toLocaleString()}
      </td>
      <td className="regions__count" data-label="Holder of">
        {party.holderCount.toLocaleString()}
      </td>
      <td className="regions__actions">
        {editing ? (
          <>
            <button type="button" className="btn btn--icon btn--sm" aria-label="Save party" disabled={busy} onClick={() => void save()}>
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
            if (window.confirm(`Delete the party “${party.name}”?`)) void run(async () => unwrap(await api.DELETE('/api/parties/{id}', path)))
          }}
        >
          <Trash2 aria-hidden="true" /> Delete
        </button>
      </td>
    </tr>
  )
}
