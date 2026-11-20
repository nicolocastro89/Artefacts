import { ImageOff } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import type { ImageUrls } from '../../api/client'
import { usePrintMode } from '../../print/PrintContext'
import { resolvePath, typeAt } from '../core/paths'
import { defineWidget } from '../core/registry'
import { isCompatible } from '../core/validation'
import type { FieldType } from '../core/types'
import { formatValue, formatYearRange, isHistoricalDate } from '../format'
import './display.css'

function isImageUrls(v: unknown): v is ImageUrls {
  return typeof v === 'object' && v !== null && typeof (v as ImageUrls).thumb === 'string'
}

/** An image on its fixed-scale neutral plate; a quiet placeholder when there is none. */
export function ImagePlate({ urls, size, alt, aspect }: { urls?: ImageUrls | null; size: keyof ImageUrls; alt: string; aspect?: string }) {
  const print = usePrintMode()
  const src = urls ? (print ? urls.print : urls[size]) : undefined
  return (
    <div className="thumb" style={aspect ? ({ '--thumb-aspect': aspect } as React.CSSProperties) : undefined}>
      {/* Print images load eagerly: pagination waits for them, and a lazy image on an off-screen page never loads. */}
      {src ? <img src={src} alt={alt} loading={print ? 'eager' : 'lazy'} decoding={print ? 'sync' : 'async'} /> : <ImageOff aria-label="No image" role="img" />}
    </div>
  )
}

// ---------- heading ----------
defineWidget({
  id: 'heading',
  label: 'Heading',
  slots: { text: { type: 'text', required: true }, subtitle: { type: 'any' } },
  config: { level: 1 as 1 | 2 | 3 },
  print: { keepTogether: true },
  component: ({ values, config }) => {
    const Tag = `h${config.level}` as 'h1' | 'h2' | 'h3'
    const subtitle = formatValue(values.subtitle?.value, values.subtitle?.type)
    return (
      <div className={`w-heading w-heading--${config.level}`}>
        <Tag>{formatValue(values.text?.value)}</Tag>
        {subtitle && <p className="w-heading__sub">{subtitle}</p>}
      </div>
    )
  },
})

// ---------- inventory tag ----------
defineWidget({
  id: 'inventory-tag',
  label: 'Inventory tag',
  slots: { text: { type: 'text', required: true } },
  config: { selected: false },
  print: { keepTogether: true },
  component: ({ values, config }) => (
    <span className={`tag${config.selected ? ' tag--selected' : ''}`}>{formatValue(values.text?.value)}</span>
  ),
})

// ---------- image ----------
defineWidget({
  id: 'image',
  label: 'Image',
  slots: { src: { type: 'image', required: true }, alt: { type: 'text' }, caption: { type: 'text' } },
  config: { size: 'web' as keyof ImageUrls, aspect: '4 / 3' },
  print: { keepTogether: true },
  component: ({ values, config }) => {
    const urls = isImageUrls(values.src?.value) ? values.src.value : null
    const caption = formatValue(values.caption?.value)
    return (
      <figure className="w-image">
        <ImagePlate urls={urls} size={config.size} alt={formatValue(values.alt?.value) || caption || ''} aspect={config.aspect} />
        {caption && <figcaption className="w-image__caption">{caption}</figcaption>}
      </figure>
    )
  },
})

// ---------- image gallery ----------
interface GalleryItem {
  urls?: ImageUrls
  caption?: string | null
  isPrimary?: boolean
  originalFileName?: string
  mediaAssetId?: string
}

defineWidget({
  id: 'image-gallery',
  label: 'Image gallery',
  slots: { images: { type: 'list', required: true } },
  config: { emptyText: 'No images yet.' },
  print: {},
  component: function Gallery({ values, config }) {
    const print = usePrintMode()
    const images = (Array.isArray(values.images?.value) ? values.images.value : []) as GalleryItem[]
    // Remembered by index into the current list; until the reader picks one, the viewer shows the primary.
    const [picked, setPicked] = useState<number | null>(null)
    if (images.length === 0) return <p className="muted">{config.emptyText}</p>
    const selected = picked !== null && picked < images.length ? picked : Math.max(0, images.findIndex((img) => img.isPrimary))
    const current = images[selected]!

    if (print) {
      return (
        <div className="w-gallery w-gallery--print">
          {images.map((img, i) => (
            <figure key={img.mediaAssetId ?? i} className="w-image keep-together">
              <ImagePlate urls={img.urls} size="print" alt={img.caption ?? img.originalFileName ?? ''} aspect="auto" />
              {img.caption && <figcaption className="w-image__caption">{img.caption}</figcaption>}
            </figure>
          ))}
        </div>
      )
    }

    return (
      <div className="w-gallery">
        <figure className="w-gallery__viewer">
          <ImagePlate urls={current.urls} size="web" alt={current.caption ?? current.originalFileName ?? ''} aspect="4 / 3" />
          <figcaption className="w-image__caption">
            {current.caption || <span className="muted">{current.originalFileName}</span>}
          </figcaption>
        </figure>
        {images.length > 1 && (
          <ul className="w-gallery__strip" aria-label="Images">
            {images.map((img, i) => (
              <li key={img.mediaAssetId ?? i}>
                <button
                  type="button"
                  className="w-gallery__pick"
                  aria-pressed={i === selected}
                  aria-label={`Show image ${i + 1}${img.caption ? `: ${img.caption}` : ''}`}
                  onClick={() => setPicked(i)}
                >
                  <ImagePlate urls={img.urls} size="thumb" alt="" />
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    )
  },
})

// ---------- field list ----------
defineWidget({
  id: 'field-list',
  label: 'Field list',
  slots: {},
  dynamicSlots: { type: 'any' },
  config: { labels: {} as Record<string, string>, hideEmpty: true, showRanges: true },
  print: { keepTogether: true },
  component: ({ values, config }) => {
    const rows = Object.entries(values)
      .map(([name, bound]) => ({ name, bound, text: formatValue(bound.value, bound.type) }))
      .filter((r) => !config.hideEmpty || r.text !== '')
    if (rows.length === 0) return null
    return (
      <dl className="w-fields">
        {rows.map(({ name, bound, text }) => (
          <div key={name} className="w-fields__row">
            <dt className="label">{config.labels[name] ?? name}</dt>
            <dd>
              {text}
              {config.showRanges && isHistoricalDate(bound.value) && (
                <span className="w-fields__range"> {formatYearRange(bound.value.earliestYear, bound.value.latestYear)}</span>
              )}
            </dd>
          </div>
        ))}
      </dl>
    )
  },
})

// ---------- table ----------
export interface TableColumn {
  path: string
  label: string
  /** Render as a link to this route template, e.g. "/artefacts/{id}" (placeholders resolve against the row). */
  link?: string
}

defineWidget({
  id: 'table',
  label: 'Table',
  slots: { rows: { type: 'list', required: true } },
  config: { columns: [] as TableColumn[], emptyText: 'Nothing to show.' },
  print: {},
  component: ({ values, config, fieldTypes }) => {
    const rows = (Array.isArray(values.rows?.value) ? values.rows.value : []) as unknown[]
    const rowsKey = values.rows?.key ?? ''
    const columnTypes = config.columns.map((c) => typeAt(fieldTypes, rowsKey, c.path))
    // Columns bind item-relative paths; each must name a scalar (or image) field of the row.
    const invalid = fieldTypes
      ? config.columns.filter((_, i) => !columnTypes[i] || !isCompatible('any', columnTypes[i] as FieldType))
      : []
    if (invalid.length > 0) {
      return (
        <div className="widget-config-error" role="alert">
          Unknown columns: {invalid.map((c) => `"${c.path}"`).join(', ')}
        </div>
      )
    }
    if (rows.length === 0) return <p className="muted">{config.emptyText}</p>
    return (
      <div className="table-wrap">
        <table className="table">
          <thead>
            <tr>
              {config.columns.map((c) => (
                <th key={c.path} scope="col">
                  {c.label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, ri) => (
              <tr key={ri}>
                {config.columns.map((c, ci) => {
                  const value = resolvePath(row, c.path)
                  const type = columnTypes[ci]
                  const content =
                    type === 'image' ? (
                      <ImagePlate urls={isImageUrls(value) ? value : null} size="thumb" alt="" />
                    ) : (
                      formatValue(value, type)
                    )
                  return (
                    <td key={c.path} className={type === 'image' ? 'table__thumb' : undefined}>
                      {c.link ? <Link to={fillTemplate(c.link, row)}>{content}</Link> : content}
                    </td>
                  )
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    )
  },
})

/** Replaces `{path}` placeholders with values resolved from `data`. */
export function fillTemplate(template: string, data: unknown): string {
  return template.replace(/\{([^}]+)\}/g, (_, p: string) => encodeURIComponent(String(resolvePath(data, p) ?? '')))
}

