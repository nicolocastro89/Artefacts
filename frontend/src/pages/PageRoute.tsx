import { useEffect } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import { toDefinition, usePage } from '../api/pages'
import { ApiError } from '../api/problem'
import type { PageDefinition } from '../widgets/core/page'
import { PageRenderer } from '../widgets/core/PageRenderer'

type PageRouteProps = { title?: string } & ({ page: PageDefinition; slug?: never } | { slug: string; page?: never })

/** Renders a page definition, given in code or loaded from the stored page with this slug, with the current route and search params. */
export function PageRoute({ page, slug, title }: PageRouteProps) {
  return slug === undefined ? <PageView page={page} title={title} /> : <StoredPageRoute slug={slug} title={title} />
}

/** The route of the custom pages: `/p/:slug`. */
export function CustomPageRoute() {
  const { slug = '' } = useParams()
  return <StoredPageRoute key={slug} slug={slug} />
}

function PageView({ page, title }: { page: PageDefinition; title?: string }) {
  const route = useParams()
  const [search] = useSearchParams()
  useEffect(() => {
    document.title = `${title ?? page.title} · Artefacts`
  }, [page.title, title])
  return <PageRenderer page={page} route={route} search={search} />
}

function StoredPageRoute({ slug, title }: { slug: string; title?: string }) {
  const stored = usePage(slug)

  if (stored.isPending) return <div className="data-state data-state--loading" aria-busy="true">Loading…</div>
  if (stored.isError) {
    if (stored.error instanceof ApiError && stored.error.status === 404) return <PageNotFound />
    return (
      <div className="data-state data-state--error" role="alert">
        Could not load this page: {stored.error.message}{' '}
        <button type="button" className="btn" onClick={() => void stored.refetch()}>
          Retry
        </button>
      </div>
    )
  }
  return <PageView page={toDefinition(stored.data)} title={title} />
}

export function PageNotFound() {
  return (
    <div className="plate empty">
      <strong>This page does not exist.</strong>
      <Link className="btn" to="/artefacts">
        Go to artefacts
      </Link>
    </div>
  )
}
