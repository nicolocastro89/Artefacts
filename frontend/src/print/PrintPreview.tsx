import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, Printer } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router'
import { artefactPrintPage } from '../pages/definitions/artefactPrint'
import { runStoredQuery } from '../widgets/core/dataSources'
import { PageRenderer } from '../widgets/core/PageRenderer'
import { PrintContext } from './PrintContext'
import { buildPrintCss, contentBox, imageMaxHeight, type Orientation, type Paper } from './printCss'
import { destroyPreviewer, ensureHandlers, scaleOversizedUnits, waitForImages } from './paginate'
import './print.css'

type Status = { state: 'waiting' } | { state: 'paginating' } | { state: 'ready'; pages: number } | { state: 'error'; message: string }

/**
 * Paginated print preview of an artefact record. The record is rendered in print mode into a hidden source,
 * then laid out by Paged.js onto real pages. Printing prints those pages, so the breaks match the preview.
 */
export function PrintPreview() {
  const { id = '' } = useParams()
  const [paper, setPaper] = useState<Paper>('A4')
  const [orientation, setOrientation] = useState<Orientation>('portrait')
  const [status, setStatus] = useState<Status>({ state: 'waiting' })
  const sourceRef = useRef<HTMLDivElement>(null)
  const pagesRef = useRef<HTMLDivElement>(null)

  // Same query as the page's container, so this resolves when the source has its data.
  const record = useQuery({
    queryKey: ['query-run', 'artefact-detail', { id }],
    queryFn: ({ signal }) => runStoredQuery('artefact-detail', { id }, signal),
  })
  const artefact = (record.data as { artefact: { title?: string } | null } | undefined)?.artefact
  const title = artefact?.title

  useEffect(() => {
    document.title = `${title ? `Print: ${title}` : 'Print'} · Artefacts`
  }, [title])

  useEffect(() => {
    if (!record.isSuccess || !artefact) return
    let cancelled = false
    const run = async () => {
      setStatus({ state: 'paginating' })
      await new Promise((r) => requestAnimationFrame(() => r(null)))
      const source = sourceRef.current
      const target = pagesRef.current
      if (!source || !target) return
      await waitForImages(source)
      const box = contentBox(paper, orientation)
      const clone = source.cloneNode(true) as HTMLElement
      clone.removeAttribute('aria-hidden')
      // Only the source is hidden. Paged.js re-creates the clone's root on every continuation page,
      // so keeping the source's hiding class would make every page after the first blank.
      clone.classList.remove('print-source')
      // Paged.js waits for every image it lays out; a lazy one on an off-screen page would never load.
      for (const img of clone.querySelectorAll('img')) img.loading = 'eager'
      scaleOversizedUnits(source, clone, box.heightPx)
      const { Previewer } = await ensureHandlers()
      if (cancelled) return
      target.replaceChildren()
      previewer = new Previewer()
      const flow = await previewer.preview(clone, [{ 'artefact-print.css': buildPrintCss(paper, orientation) }], target)
      if (!cancelled) setStatus({ state: 'ready', pages: flow.total })
    }
    let previewer: InstanceType<Awaited<ReturnType<typeof ensureHandlers>>['Previewer']> | null = null
    run().catch((e: unknown) => !cancelled && setStatus({ state: 'error', message: e instanceof Error ? e.message : String(e) }))
    return () => {
      cancelled = true
      // Paper/orientation changed or the preview closed: stop the old layout before the next one starts.
      if (previewer) destroyPreviewer(previewer)
    }
  }, [record.isSuccess, record.dataUpdatedAt, paper, orientation])

  const box = contentBox(paper, orientation)
  const imageMax = { '--print-image-max': imageMaxHeight(paper, orientation) } as React.CSSProperties

  return (
    <div className="print-screen">
      <header className="print-bar">
        <Link className="btn" to={`/artefacts/${encodeURIComponent(id)}`}>
          <ArrowLeft aria-hidden="true" /> Back to record
        </Link>
        <div className="print-bar__title">
          <strong>Print preview</strong>
          {title && <span className="muted">{title}</span>}
        </div>
        <label className="print-bar__field">
          <span className="label">Paper</span>
          <select className="select" value={paper} onChange={(e) => setPaper(e.target.value as Paper)}>
            <option value="A4">A4</option>
            <option value="Letter">Letter</option>
          </select>
        </label>
        <label className="print-bar__field">
          <span className="label">Orientation</span>
          <select className="select" value={orientation} onChange={(e) => setOrientation(e.target.value as Orientation)}>
            <option value="portrait">Portrait</option>
            <option value="landscape">Landscape</option>
          </select>
        </label>
        <span className="print-bar__count" aria-live="polite">
          {status.state === 'ready' ? `${status.pages} ${status.pages === 1 ? 'page' : 'pages'}` : status.state === 'error' ? '' : 'Laying out pages…'}
        </span>
        <button type="button" className="btn btn--primary" disabled={status.state !== 'ready'} onClick={() => window.print()}>
          <Printer aria-hidden="true" /> Print
        </button>
      </header>

      {record.isError && (
        <div className="data-state data-state--error" role="alert">
          Could not load this artefact: {record.error.message}
        </div>
      )}
      {record.isSuccess && !artefact && (
        <div className="data-state data-state--error" role="alert">
          This artefact does not exist (it may have been deleted).
        </div>
      )}
      {status.state === 'error' && (
        <div className="notice notice--error print-error" role="alert">
          The pages could not be laid out: {status.message}
        </div>
      )}

      {/* Source: the record in print mode at the page's content width, measured but never shown. */}
      <div ref={sourceRef} className="print-source print-doc" aria-hidden="true" style={{ ...imageMax, width: `${box.widthPx}px` }}>
        <PrintContext.Provider value>
          <PageRenderer page={artefactPrintPage} route={{ id }} />
        </PrintContext.Provider>
      </div>

      <div ref={pagesRef} className="print-pages" aria-label="Pages" style={imageMax} />
    </div>
  )
}
