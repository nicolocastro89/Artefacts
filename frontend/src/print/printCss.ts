export type Paper = 'A4' | 'Letter'
export type Orientation = 'portrait' | 'landscape'

const SIZES_MM: Record<Paper, [number, number]> = { A4: [210, 297], Letter: [215.9, 279.4] }
/** Page margins in mm: top, right, bottom, left. The top and bottom hold the running header and footer. */
export const MARGINS_MM = [18, 16, 20, 16] as const

const MM_TO_PX = 96 / 25.4

export function contentBox(paper: Paper, orientation: Orientation): { widthPx: number; heightPx: number } {
  const [w, h] = SIZES_MM[paper]
  const [pw, ph] = orientation === 'portrait' ? [w, h] : [h, w]
  return {
    widthPx: Math.floor((pw - MARGINS_MM[1] - MARGINS_MM[3]) * MM_TO_PX),
    heightPx: Math.floor((ph - MARGINS_MM[0] - MARGINS_MM[2]) * MM_TO_PX),
  }
}

/**
 * Tallest a printed image may be: at most 150 mm, and always leaving room on the page for the record heading
 * and a caption. Otherwise, in landscape, the heading and the primary image cannot share the first page, and
 * the image (a keep-together unit) moves to page 2, leaving page 1 with only the title.
 */
export function imageMaxHeight(paper: Paper, orientation: Orientation): string {
  const [w, h] = SIZES_MM[paper]
  const pageHeight = orientation === 'portrait' ? h : w
  const contentHeight = pageHeight - MARGINS_MM[0] - MARGINS_MM[2]
  return `${Math.min(150, Math.floor(contentHeight - 45))}mm`
}

/**
 * CSS Paged Media rules for the printed record: paper size, running header (inventory number and title),
 * "page N of M" footer, keep-together units, and tables that break only between rows.
 */
export function buildPrintCss(paper: Paper, orientation: Orientation): string {
  const [t, r, b, l] = MARGINS_MM
  const margin = `font-family: 'Archivo Variable', Archivo, sans-serif; font-size: 8.5pt; color: #545f65;`
  return `
@page {
  size: ${paper} ${orientation};
  margin: ${t}mm ${r}mm ${b}mm ${l}mm;
  @top-left { content: string(inv); ${margin} font-stretch: 75%; font-weight: 650; letter-spacing: 0.04em; }
  @top-right { content: string(title); ${margin} }
  @bottom-right { content: "page " counter(page) " of " counter(pages); ${margin} }
  @bottom-left { content: "Artefacts"; ${margin} font-stretch: 75%; text-transform: uppercase; letter-spacing: 0.08em; }
}

/* No ancestor selector: Paged.js does not keep the source root's classes on the first page. */
[data-node-id="tag"] .tag { string-set: inv content(text); }
[data-node-id="title"] h1 { string-set: title content(text); }

.keep-together, .w-image, .w-fields__row, figure { break-inside: avoid; }
.layout--section > .layout__title { break-after: avoid; }
table { break-inside: auto; }
tr { break-inside: avoid; break-after: auto; }
thead { display: table-header-group; }
`
}
