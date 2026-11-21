import { useQueries, useQueryClient } from '@tanstack/react-query'
import { Copy, Save } from 'lucide-react'
import { Component, useEffect, useId, useMemo, useState, type FormEvent, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { createPage, duplicatePage, pageKeys, updatePage, usePageById, type StoredPage } from '../api/pages'
import { ApiError, mapFieldErrors } from '../api/problem'
import { fetchQueryDescription } from '../widgets/core/dataSources'
import { lintPage, queryIdsOf, type PageProblem, type QueryDescriptions } from '../widgets/core/lintPage'
import type { PageNode } from '../widgets/core/page'
import { PageRenderer } from '../widgets/core/PageRenderer'
import { message } from './lists'
import { PageNotFound } from './PageRoute'
import './PagesPage.css'

const starter = JSON.stringify(
  {
    root: {
      type: 'container',
      id: 'main',
      dataSource: { kind: 'static', data: { title: 'A new page' } },
      children: [{ type: 'widget', id: 'title', widgetId: 'heading', bindings: { text: 'title' } }],
    },
  },
  null,
  2,
)

/** The editor of one page, or of a new one at `/pages/new`. */
export function PageEditor() {
  const { id } = useParams()
  return id ? <ExistingPage id={id} /> : <Editor page={null} />
}

function ExistingPage({ id }: { id: string }) {
  const page = usePageById(id)
  if (page.isPending) return <div className="data-state data-state--loading" aria-busy="true">Loading…</div>
  if (page.isError) {
    if (page.error instanceof ApiError && page.error.status === 404) return <PageNotFound />
    return (
      <div className="data-state data-state--error" role="alert">
        Could not load this page: {page.error.message}
      </div>
    )
  }
  return <Editor key={`${page.data.id}:${page.data.updatedAt}`} page={page.data} />
}

type Parsed = { ok: true; value: unknown } | { ok: false; error: string }

function parse(text: string): Parsed {
  try {
    return { ok: true, value: JSON.parse(text) }
  } catch (e) {
    return { ok: false, error: e instanceof Error ? e.message : 'Not valid JSON.' }
  }
}

function rootOf(value: unknown): PageNode | null {
  const root = (value as { root?: unknown } | null)?.root
  return root && typeof root === 'object' && typeof (root as { type?: unknown }).type === 'string' ? (root as PageNode) : null
}

function Editor({ page }: { page: StoredPage | null }) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const ids = { name: useId(), slug: useId(), nav: useId(), label: useId(), definition: useId(), params: useId() }
  const readOnly = page?.kind === 'system'

  const [name, setName] = useState(page?.name ?? '')
  const [slug, setSlug] = useState(page?.slug ?? '')
  const [showInNavigation, setShowInNavigation] = useState(page?.showInNavigation ?? false)
  const [navigationLabel, setNavigationLabel] = useState(page?.navigationLabel ?? '')
  const [text, setText] = useState(page ? JSON.stringify(page.definition, null, 2) : starter)
  const [previewParams, setPreviewParams] = useState('')
  const [errors, setErrors] = useState<{ byField: Record<string, string[]>; unmatched: string[] }>({ byField: {}, unmatched: [] })
  const [saving, setSaving] = useState(false)
  const [copying, setCopying] = useState(false)

  useEffect(() => {
    document.title = `${page?.name ?? 'New page'} · Pages · Artefacts`
  }, [page?.name])

  const parsed = useMemo(() => parse(text), [text])
  const root = parsed.ok ? rootOf(parsed.value) : null

  const queryIds = useMemo(() => (root ? safely(() => queryIdsOf(root), []) : []), [root])
  const descriptionQueries = useQueries({
    queries: queryIds.map((queryId) => ({
      queryKey: ['query-description', queryId],
      queryFn: ({ signal }: { signal: AbortSignal }) => fetchQueryDescription(queryId, signal),
      staleTime: 10 * 60 * 1000,
      retry: false,
    })),
  })
  const descriptions: QueryDescriptions = Object.fromEntries(
    queryIds.map((queryId, i) => {
      const result = descriptionQueries[i]
      return [queryId, result?.data ?? (result?.error instanceof ApiError && result.error.status === 404 ? null : undefined)]
    }),
  )
  const problems: PageProblem[] = root ? safely(() => lintPage(root, descriptions), [{ path: 'root', message: 'This definition cannot be checked until its structure is valid.' }]) : []

  const save = async (e: FormEvent) => {
    e.preventDefault()
    if (readOnly || !parsed.ok) return
    setSaving(true)
    setErrors({ byField: {}, unmatched: [] })
    try {
      const body = { name, slug, definition: parsed.value, showInNavigation, navigationLabel: navigationLabel.trim() === '' ? null : navigationLabel }
      const saved = page ? await updatePage(page.id, body) : await createPage(body)
      await queryClient.invalidateQueries({ queryKey: pageKeys.all })
      navigate(`/pages/${saved.id}`, { replace: true })
    } catch (err) {
      if (err instanceof ApiError) setErrors(mapFieldErrors(err.fieldErrors, ['name', 'slug', 'navigationLabel', 'definition']))
      else setErrors({ byField: {}, unmatched: [message(err)] })
    } finally {
      setSaving(false)
    }
  }

  const field = (key: string) => errors.byField[key] ?? []

  return (
    <div className="pages">
      <div className="page-head">
        <div className="page-head__titles">
          <h1>{page ? page.name : 'New page'}</h1>
          <p className="page-head__sub">
            {readOnly ? 'A system page: it is read-only. Duplicate it to change a copy.' : 'The definition is JSON; the preview shows problems with widgets and bindings as you type.'}
          </p>
        </div>
        <Link className="btn" to="/pages">
          All pages
        </Link>
        {page && (
          <button type="button" className="btn" aria-expanded={copying} onClick={() => setCopying((open) => !open)}>
            <Copy aria-hidden="true" /> Duplicate
          </button>
        )}
      </div>

      {page && copying && <DuplicateForm page={page} onDone={(copy) => navigate(`/pages/${copy.id}`)} />}

      <form className="pages__editor" onSubmit={save}>
        <div className="plate pages__fields">
          <div className={`field${field('name').length ? ' field--invalid' : ''}`}>
            <label className="field__label" htmlFor={ids.name}>
              Name
            </label>
            <input id={ids.name} className="input" value={name} readOnly={readOnly} aria-invalid={field('name').length > 0} onChange={(e) => setName(e.target.value)} />
            <Errors messages={field('name')} />
          </div>
          <div className={`field${field('slug').length ? ' field--invalid' : ''}`}>
            <label className="field__label" htmlFor={ids.slug}>
              Slug
            </label>
            <input id={ids.slug} className="input" value={slug} readOnly={readOnly} aria-invalid={field('slug').length > 0} onChange={(e) => setSlug(e.target.value)} />
            <p className="field__hint">{readOnly ? 'The built-in routes load this page.' : `Shown at /p/${slug.trim().toLowerCase() || 'slug'}`}</p>
            <Errors messages={field('slug')} />
          </div>
          {!readOnly && (
            <>
              <div className="field">
                <label className="pages__check" htmlFor={ids.nav}>
                  <input id={ids.nav} type="checkbox" checked={showInNavigation} onChange={(e) => setShowInNavigation(e.target.checked)} /> Show in navigation
                </label>
              </div>
              <div className={`field${field('navigationLabel').length ? ' field--invalid' : ''}`}>
                <label className="field__label" htmlFor={ids.label}>
                  Navigation label
                </label>
                <input id={ids.label} className="input" value={navigationLabel} disabled={!showInNavigation} aria-invalid={field('navigationLabel').length > 0} onChange={(e) => setNavigationLabel(e.target.value)} />
                <Errors messages={field('navigationLabel')} />
              </div>
            </>
          )}
        </div>

        <div className={`field${field('definition').length || !parsed.ok ? ' field--invalid' : ''}`}>
          <label className="field__label" htmlFor={ids.definition}>
            Definition
          </label>
          <textarea id={ids.definition} className="textarea pages__json" spellCheck={false} rows={22} value={text} readOnly={readOnly} aria-invalid={!parsed.ok || field('definition').length > 0} onChange={(e) => setText(e.target.value)} />
          {!parsed.ok && (
            <p className="field__error" role="alert">
              Not valid JSON: {parsed.error}
            </p>
          )}
          <Errors messages={field('definition')} />
        </div>

        {errors.unmatched.length > 0 && <Errors messages={errors.unmatched} />}

        {!readOnly && (
          <div className="pages__save">
            <button type="submit" className="btn btn--primary" disabled={saving || !parsed.ok}>
              <Save aria-hidden="true" /> {page ? 'Save changes' : 'Create page'}
            </button>
          </div>
        )}
      </form>

      <section className="pages__preview" aria-labelledby={`${ids.params}-title`}>
        <h2 id={`${ids.params}-title`}>Preview</h2>
        <div className="field">
          <label className="field__label" htmlFor={ids.params}>
            URL parameters
          </label>
          <input id={ids.params} className="input" placeholder="id=…&page=2" value={previewParams} onChange={(e) => setPreviewParams(e.target.value)} />
        </div>
        <ul className="pages__problems" aria-label="Problems found in the definition">
          {problems.length === 0 && root && <li className="muted">No problems found.</li>}
          {problems.map((problem) => (
            <li key={`${problem.path}:${problem.message}`}>
              <code>{problem.path}</code> {problem.message}
            </li>
          ))}
        </ul>
        <div className="plate pages__canvas">
          {root ? (
            <PreviewBoundary resetKey={text}>
              <PageRenderer page={{ id: 'preview', title: name, root }} search={new URLSearchParams(previewParams)} />
            </PreviewBoundary>
          ) : (
            <p className="muted">The preview appears when the definition has a root node.</p>
          )}
        </div>
      </section>
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

function safely<T>(run: () => T, fallback: T): T {
  try {
    return run()
  } catch {
    return fallback
  }
}

function DuplicateForm({ page, onDone }: { page: StoredPage; onDone: (copy: StoredPage) => void }) {
  const queryClient = useQueryClient()
  const nameId = useId()
  const slugId = useId()
  const [name, setName] = useState(`${page.name} copy`)
  const [slug, setSlug] = useState(`${page.slug}-copy`)
  const [errors, setErrors] = useState<{ byField: Record<string, string[]>; unmatched: string[] }>({ byField: {}, unmatched: [] })
  const [busy, setBusy] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setErrors({ byField: {}, unmatched: [] })
    try {
      const copy = await duplicatePage(page.id, { name, slug })
      await queryClient.invalidateQueries({ queryKey: pageKeys.all })
      onDone(copy)
    } catch (err) {
      if (err instanceof ApiError) setErrors(mapFieldErrors(err.fieldErrors, ['name', 'slug']))
      else setErrors({ byField: {}, unmatched: [message(err)] })
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
        <Errors messages={errors.byField.name ?? []} />
      </div>
      <div className="field">
        <label className="field__label" htmlFor={slugId}>
          Slug of the copy
        </label>
        <input id={slugId} className="input" value={slug} onChange={(e) => setSlug(e.target.value)} />
        <Errors messages={errors.byField.slug ?? []} />
      </div>
      <button type="submit" className="btn btn--primary" disabled={busy}>
        Create copy
      </button>
      <Errors messages={errors.unmatched} />
    </form>
  )
}

/** A page definition the renderer cannot draw must not take the editor down with it. */
class PreviewBoundary extends Component<{ resetKey: string; children: ReactNode }, { failed: string | null; key: string }> {
  state = { failed: null as string | null, key: this.props.resetKey }

  static getDerivedStateFromError(error: unknown) {
    return { failed: error instanceof Error ? error.message : 'The page could not be drawn.' }
  }

  static getDerivedStateFromProps(props: { resetKey: string }, state: { failed: string | null; key: string }) {
    return props.resetKey === state.key ? null : { failed: null, key: props.resetKey }
  }

  render() {
    return this.state.failed === null ? (
      this.props.children
    ) : (
      <p className="field__error" role="alert">
        The preview could not draw this definition: {this.state.failed}
      </p>
    )
  }
}
