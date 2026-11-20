type PagedModule = typeof import('pagedjs')

let loaded: Promise<PagedModule> | null = null

/**
 * Loads Paged.js once (lazily, it is large) and registers our handler that repeats a split table's
 * header row on every page the table continues onto (Paged.js does not do this itself).
 */
export function ensureHandlers(): Promise<PagedModule> {
  loaded ??= import('pagedjs').then((paged) => {
    class RepeatTableHeaders extends paged.Handler {
      afterPageLayout(pageElement: HTMLElement) {
        for (const table of pageElement.querySelectorAll<HTMLTableElement>('table[data-split-from]')) {
          if (table.tHead) continue
          const ref = table.dataset.splitFrom
          const original = ref ? (this.chunker.source?.querySelector?.(`[data-ref="${ref}"]`) as HTMLTableElement | null) : null
          const head = original?.tHead
          if (head) table.insertBefore(head.cloneNode(true), table.firstChild)
        }
      }
    }
    paged.registerHandlers(RepeatTableHeaders)
    return paged
  })
  return loaded
}

/**
 * Tears down a previous layout completely. Paged.js has no single teardown: each page holds a ResizeObserver
 * that keeps reflowing (and throws once its nodes are gone), and the injected stylesheets, including the old
 * `@page` size, stay in <head>. Without this, changing paper or orientation corrupts the next layout.
 */
export function destroyPreviewer(previewer: InstanceType<PagedModule['Previewer']>): void {
  try {
    for (const page of previewer.chunker.pages ?? []) page.destroy()
    previewer.chunker.destroy()
  } catch {
    // A layout that was still in progress may have half-built pages; removing what exists is enough.
  }
  previewer.polisher.destroy()
}

/** Resolves when every image in `root` has loaded (or failed), so layout measures real image sizes. */
export async function waitForImages(root: HTMLElement, timeoutMs = 15_000): Promise<void> {
  const images = [...root.querySelectorAll('img')]
  const settle = (img: HTMLImageElement) =>
    img.complete ? Promise.resolve() : new Promise<void>((resolve) => {
      img.addEventListener('load', () => resolve(), { once: true })
      img.addEventListener('error', () => resolve(), { once: true })
    })
  await Promise.race([Promise.all(images.map(settle)), new Promise((r) => setTimeout(r, timeoutMs))])
}

/**
 * A keep-together unit taller than a page cannot move to the next page, so it is scaled down to fit one page.
 * Measures in the (laid-out) source and applies `zoom` to the matching node of the clone that gets paginated.
 */
export function scaleOversizedUnits(source: HTMLElement, clone: HTMLElement, pageHeightPx: number): void {
  const measured = [...source.querySelectorAll<HTMLElement>('.keep-together')]
  const targets = [...clone.querySelectorAll<HTMLElement>('.keep-together')]
  measured.forEach((el, i) => {
    const height = el.getBoundingClientRect().height
    const target = targets[i]
    if (target && height > pageHeightPx) {
      const factor = Math.max(0.2, (pageHeightPx - 4) / height)
      target.style.zoom = String(factor)
      target.dataset.scaled = factor.toFixed(2)
    }
  })
}
