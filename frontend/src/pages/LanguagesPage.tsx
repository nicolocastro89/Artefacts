import { useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, Plus, Trash2 } from 'lucide-react'
import { useEffect, useId, useState, type FormEvent } from 'react'
import { api, type Language } from '../api/client'
import { ApiError, unwrap } from '../api/problem'
import { useContentLanguages } from '../app/languages'
import { ReferenceTabs } from './ReferenceTabs'
import './RegionsPage.css'

function message(e: unknown): string {
  if (e instanceof ApiError) return Object.values(e.fieldErrors).flat()[0] ?? e.message
  return e instanceof Error ? e.message : 'Something went wrong.'
}

/**
 * The content languages names and descriptions are recorded in, in fallback order: a value missing in the reader's
 * language is shown in the first language below that has it.
 */
export function LanguagesPage() {
  const queryClient = useQueryClient()
  const { data: languages = [], isPending, isError, error } = useContentLanguages()
  const [rowError, setRowError] = useState<{ code: string; text: string } | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    document.title = 'Languages · Artefacts'
  }, [])

  // Everything translated reads differently after a change: refetch it all.
  const refresh = () => queryClient.invalidateQueries()

  const run = async (code: string, action: () => Promise<unknown>) => {
    setBusy(true)
    setRowError(null)
    try {
      await action()
      await refresh()
    } catch (e) {
      setRowError({ code, text: message(e) })
    } finally {
      setBusy(false)
    }
  }

  const move = (index: number, by: number) => {
    const codes = languages.map((l) => l.code)
    const [code] = codes.splice(index, 1)
    codes.splice(index + by, 0, code!)
    void run(code!, async () => unwrap(await api.PUT('/api/languages/order', { body: { codes } })))
  }

  return (
    <div className="regions">
      <ReferenceTabs />
      <div className="page-head">
        <div className="page-head__titles">
          <h1>Languages</h1>
          <p className="page-head__sub">The languages names and descriptions are recorded in. When a value is missing in the reader’s language, the first language in this order that has it is shown.</p>
        </div>
      </div>

      <AddLanguage onAdded={refresh} />

      {isPending ? (
        <div className="data-state" aria-busy="true">
          Loading…
        </div>
      ) : isError ? (
        <div className="data-state data-state--error" role="alert">
          Could not load languages: {error.message}
        </div>
      ) : (
        <div className="table-wrap">
          <table className="table regions__table">
            <caption className="visually-hidden">Languages in fallback order</caption>
            <thead>
              <tr>
                <th scope="col" className="regions__count">
                  Order
                </th>
                <th scope="col">Language</th>
                <th scope="col">Code</th>
                <th scope="col">
                  <span className="visually-hidden">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {languages.map((language: Language, i) => (
                <tr key={language.code} aria-busy={busy}>
                  <td className="regions__count" data-label="Order">
                    {i + 1}
                  </td>
                  <td>
                    <span className="cell-title">{language.name}</span>
                    {rowError?.code === language.code && (
                      <p className="field__error" role="alert">
                        {rowError.text}
                      </p>
                    )}
                  </td>
                  <td data-label="Code">
                    <span className="tag">{language.code}</span>
                  </td>
                  <td className="regions__actions">
                    <button type="button" className="btn btn--icon btn--sm" aria-label={`Move ${language.name} up`} disabled={busy || i === 0} onClick={() => move(i, -1)}>
                      <ArrowUp aria-hidden="true" />
                    </button>
                    <button
                      type="button"
                      className="btn btn--icon btn--sm"
                      aria-label={`Move ${language.name} down`}
                      disabled={busy || i === languages.length - 1}
                      onClick={() => move(i, 1)}
                    >
                      <ArrowDown aria-hidden="true" />
                    </button>
                    <button
                      type="button"
                      className="btn btn--sm btn--danger"
                      disabled={busy}
                      onClick={() => {
                        if (window.confirm(`Remove the language “${language.name}”?`))
                          void run(language.code, async () => unwrap(await api.DELETE('/api/languages/{code}', { params: { path: { code: language.code } } })))
                      }}
                    >
                      <Trash2 aria-hidden="true" /> Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function AddLanguage({ onAdded }: { onAdded: () => Promise<unknown> }) {
  const codeId = useId()
  const nameId = useId()
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      unwrap(await api.POST('/api/languages', { body: { code, name } }))
      setCode('')
      setName('')
      await onAdded()
    } catch (err) {
      setError(message(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="plate regions__add regions__add--language" onSubmit={submit}>
      <div className={`field${error ? ' field--invalid' : ''}`}>
        <label className="field__label" htmlFor={nameId}>
          New language
        </label>
        <input id={nameId} className="input" value={name} required onChange={(e) => setName(e.target.value)} placeholder="e.g. French" />
      </div>
      <div className={`field${error ? ' field--invalid' : ''}`}>
        <label className="field__label" htmlFor={codeId}>
          Code
        </label>
        <input id={codeId} className="input" value={code} required onChange={(e) => setCode(e.target.value)} placeholder="fr" aria-describedby={`${codeId}-hint`} />
        <p id={`${codeId}-hint`} className="field__hint">
          Two-letter ISO code, optionally with a region (pt-br)
        </p>
      </div>
      <button type="submit" className="btn btn--primary" disabled={saving || !code.trim() || !name.trim()}>
        <Plus aria-hidden="true" /> Add language
      </button>
      <p className="regions__add-feedback" aria-live="polite">
        {error && <span className="field__error">{error}</span>}
      </p>
    </form>
  )
}
