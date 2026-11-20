import { useEffect } from 'react'
import { useParams, useSearchParams } from 'react-router'
import type { PageDefinition } from '../widgets/core/page'
import { PageRenderer } from '../widgets/core/PageRenderer'

/** Renders a page definition with the current route and search params. */
export function PageRoute({ page, title }: { page: PageDefinition; title?: string }) {
  const route = useParams()
  const [search] = useSearchParams()
  useEffect(() => {
    document.title = `${title ?? page.title} · Artefacts`
  }, [page.title, title])
  return <PageRenderer page={page} route={route} search={search} />
}
