import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { Copy, Save } from 'lucide-react'
import { useEffect, useId, useMemo, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { createQuery, describeDocument, duplicateQuery, queryKeys, updateQuery, useStoredQuery, type StoredQuery } from '../api/queries'
import { ApiError, mapFieldErrors } from '../api/problem'
import { message } from '../pages/lists'
import { PageNotFound } from '../pages/PageRoute'
import '../pages/PagesPage.css'
import { BlocksView } from './blocks/BlocksView'
import { addRoot, settle } from './edit'
import { toModel } from './fromDocument'
import { emptyModel, type QueryModel } from './model'
import { useBuilderRules, type BuilderRules } from './rules'
import { RunPanel } from './RunPanel'
import { toDocument } from './toDocument'

/** The editor of one stored query, or of a new one at `/queries/new`. */
export function QueryEditor() {
  const { id } = useParams()
  const rules = useBuilderRules()
  if (rules.isPending) return <div className="data-state data-state--loading" aria-busy="true">Loading…</div>
  if (rules.isError) {
    return (
      <div className="data-state data-state--error" role="alert">
        Could not load what the builder offers: {rules.error.message}
      </div>
    )
  }
  return id ? <ExistingQuery id={id} rules={rules.data} /> : <Editor query={null} rules={rules.data} />
}

function ExistingQuery({ id, rules }: { id: string; rules: BuilderRules }) {
  const query = useStoredQuery(id)
  if (query.isPending) return <div className="data-state data-state--loading" aria-busy="true">Loading…</div>
  if (query.isError) {
    if (query.error instanceof ApiError && query.error.status === 404) return <PageNotFound />
    return (
      <div className="data-state data-state--error" role="alert">
        Could not load this query: {query.error.message}
      </div>
    )
  }
  return <Editor key={`${query.data.id}:${query.data.updatedAt}`} query={query.data} rules={rules} />
}

function useDebounced<T>(value: T, ms: number): T {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), ms)
    return () => clearTimeout(timer)
  }, [value, ms])
  return debounced
}

/** A starting point for a new query: the artefact list with its total and a few fields. */
function starterDocument(rules: BuilderRules): string {
  const settled = settle(addRoot(emptyModel(), 'artefacts'), rules)
  return settled.ok ? toDocument(settled.model) : '{ language }'
}

function Editor({ query, rules }: { query: StoredQuery | null; rules: BuilderRules }) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const ids = { name: useId(), text: useId() }
  const readOnly = query?.kind === 'system'

  const [name, setName] = useState(query?.name ?? '')
  const [document, setDocument] = useState(() => query?.document ?? starterDocument(rules))
  const [errors, setErrors] = useState<{ byField: Record<string, string[]>; unmatched: string[] }>({ byField: {}, unmatched: [] })
  const [saving, setSaving] = useState(false)
  const [copying, setCopying] = useState(false)

  const conversion = useMemo(() => toModel(document, rules), [document, rules])
  const [view, setView] = useState<'blocks' | 'text'>(conversion.ok ? 'blocks' : 'text')
  const shown = conversion.ok ? view : 'text'

  useEffect(() => {
    window.document.title = `${query?.name ?? 'New query'} · Queries · Artefacts`
  }, [query?.name])

  // The document is described as it is typed, a moment after the last change.
  const settledDocument = useDebounced(document, 400)
  const described = useQuery({
    queryKey: ['describe-document', settledDocument],
    queryFn: ({ signal }) => describeDocument(settledDocument, signal),
    placeholderData: keepPreviousData,
    staleTime: 60_000,
  })

  const changeFromBlocks = (next: QueryModel) => {
    // Printing drops comments, so say so before the first rewrite of a document that has them.
    if (conversion.ok && conversion.model.comments.length > 0 && !window.confirm('This query has comments. Changing a block rewrites it and removes them. Continue?')) return
    setDocument(toDocument(next))
  }

  const save = async (e: FormEvent) => {
    e.preventDefault()
    if (readOnly) return
    setSaving(true)
    setErrors({ byField: {}, unmatched: [] })
    try {
      const saved = query ? await updateQuery(query.id, { name, document }) : await createQuery({ name, document })
      await queryClient.invalidateQueries({ queryKey: queryKeys.all })
      navigate(`/queries/${saved.id}`, { replace: true })
    } catch (err) {
      if (err instanceof ApiError) setErrors(mapFieldErrors(err.fieldErrors, ['name', 'document']))
      else setErrors({ byField: {}, unmatched: [message(err)] })
    } finally {
      setSaving(false)
    }
  }

  const nameErrors = errors.byField.name ?? []
  const documentErrors = errors.byField.document ?? []

  return (
    <div className="pages">
      <div className="page-head">
        <div className="page-head__titles">
          <h1>{query ? query.name : 'New query'}</h1>
          <p className="page-head__sub">
            {readOnly ? 'A system query: it is read-only. Duplicate it to change a copy.' : 'Build the query from blocks, or write it as text. Both edit the same document.'}
          </p>
        </div>
        <Link className="btn" to="/queries">
          All queries
        </Link>
        {query && (
          <button type="button" className="btn" aria-expanded={copying} onClick={() => setCopying((open) => !open)}>
            <Copy aria-hidden="true" /> Duplicate
          </button>
        )}
      </div>

      {query && copying && <DuplicateForm query={query} onDone={(copy) => navigate(`/queries/${copy.id}`)} />}

      <form className="pages__editor" onSubmit={save}>
        <div className={`field${nameErrors.length ? ' field--invalid' : ''}`}>
          <label className="field__label" htmlFor={ids.name}>
            Name
          </label>
          <input id={ids.name} className="input" value={name} readOnly={readOnly} aria-invalid={nameErrors.length > 0} onChange={(e) => setName(e.target.value)} />
          <Errors messages={nameErrors} />
        </div>

        <div className="editor__views" role="tablist" aria-label="View of the query">
          <button type="button" role="tab" className={`btn btn--sm${shown === 'blocks' ? ' btn--primary' : ''}`} aria-selected={shown === 'blocks'} disabled={!conversion.ok} onClick={() => setView('blocks')}>
            Blocks
          </button>
          <button type="button" role="tab" className={`btn btn--sm${shown === 'text' ? ' btn--primary' : ''}`} aria-selected={shown === 'text'} onClick={() => setView('text')}>
            Text
          </button>
        </div>

        {!conversion.ok && (
          <div className="plate" role="status">
            <strong>Blocks cannot show this query:</strong>
            <ul>
              {conversion.reasons.map((reason) => (
                <li key={reason}>{reason}</li>
              ))}
            </ul>
            <p className="muted">It is edited as text; the blocks never rewrite it.</p>
          </div>
        )}

        {shown === 'blocks' && conversion.ok ? (
          <BlocksView model={conversion.model} rules={rules} readOnly={readOnly} onChange={changeFromBlocks} />
        ) : (
          <div className={`field${documentErrors.length ? ' field--invalid' : ''}`}>
            <label className="field__label" htmlFor={ids.text}>
              Query text
            </label>
            <textarea
              id={ids.text}
              className="textarea pages__json"
              spellCheck={false}
              rows={20}
              value={document}
              readOnly={readOnly}
              aria-invalid={documentErrors.length > 0}
              onChange={(e) => setDocument(e.target.value)}
            />
          </div>
        )}
        <Errors messages={documentErrors} />
        {errors.unmatched.length > 0 && <Errors messages={errors.unmatched} />}

        {!readOnly && (
          <div className="pages__save">
            <button type="submit" className="btn btn--primary" disabled={saving || name.trim() === ''}>
              <Save aria-hidden="true" /> {query ? 'Save changes' : 'Create query'}
            </button>
          </div>
        )}
      </form>

      <RunPanel document={document} description={described.data?.description ?? null} problems={described.data?.problems ?? []} />
    </div>
  )
}

function Errors({ messages }: { messages: string[] }) {
  return messages.map((m) => (
    <p key={m} className="field__error" role="alert">
      {m}
    </p>
  ))
}

function DuplicateForm({ query, onDone }: { query: StoredQuery; onDone: (copy: StoredQuery) => void }) {
  const queryClient = useQueryClient()
  const nameId = useId()
  const [name, setName] = useState(`${query.name} copy`)
  const [errors, setErrors] = useState<string[]>([])
  const [busy, setBusy] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setErrors([])
    try {
      const copy = await duplicateQuery(query.id, name)
      await queryClient.invalidateQueries({ queryKey: queryKeys.all })
      onDone(copy)
    } catch (err) {
      setErrors(err instanceof ApiError ? Object.values(err.fieldErrors).flat() : [message(err)])
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="plate pages__duplicate" onSubmit={submit}>
      <div className="field">
        <label className="field__label" htmlFor={nameId}>
          Name of the copy
        </label>
        <input id={nameId} className="input" value={name} onChange={(e) => setName(e.target.value)} />
        <Errors messages={errors} />
      </div>
      <button type="submit" className="btn btn--primary" disabled={busy}>
        Create copy
      </button>
    </form>
  )
}

