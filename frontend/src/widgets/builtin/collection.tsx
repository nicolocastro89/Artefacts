import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, ChevronsUpDown, LayoutGrid, Plus, Rows3, Search, X } from 'lucide-react'
import { useEffect, useLayoutEffect, useRef, useState, type KeyboardEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import type { ImageUrls } from '../../api/client'
import { resolvePath } from '../core/paths'
import { defineWidget } from '../core/registry'
import { formatValue, formatYearRange, isHistoricalDate } from '../format'
import { fillTemplate, ImagePlate } from './display'
import './collection.css'

/*
 * Collection widgets share list state through the URL (search, sort, direction, page, view, selected),
 * so a list view can be bookmarked and shared. They never fetch: the container feeds them its rows.
 */

type ViewMode = 'table' | 'grid'

function useListParams() {
  const [params, setParams] = useSearchParams()
  const update = (changes: Record<string, string | null>, options?: { resetPage?: boolean }) => {
    setParams(
      (prev) => {
        const next = new URLSearchParams(prev)
        for (const [k, v] of Object.entries(changes)) {
          if (v === null || v === '') next.delete(k)
          else next.set(k, v)
        }
        if (options?.resetPage) next.delete('page')
        return next
      },
      { replace: true },
    )
  }
  return {
    view: (params.get('view') === 'grid' ? 'grid' : 'table') as ViewMode,
    sort: params.get('sort') ?? 'inventoryNumber',
    direction: params.get('direction') === 'desc' ? 'desc' : 'asc',
    selected: params.get('selected'),
    search: params.get('search') ?? '',
    update,
  }
}

function rowId(row: unknown, idPath: string): string {
  return String(resolvePath(row, idPath) ?? '')
}

// ---------- toolbar ----------
defineWidget({
  id: 'list-toolbar',
  label: 'List toolbar',
  slots: { total: { type: 'number' } },
  config: { title: 'Artefacts', noun: 'record', searchLabel: 'Search', searchPlaceholder: 'Search', newHref: '', newLabel: 'New' },
  print: { hideInPrint: true },
  component: function ListToolbar({ values, config }) {
    const { view, search, update } = useListParams()
    const [text, setText] = useState(search)
    const total = typeof values.total?.value === 'number' ? values.total.value : null

    // Debounce typing into the URL; keep the input in sync when the URL changes elsewhere (back/forward).
    useEffect(() => setText(search), [search])
    useEffect(() => {
      if (text === search) return
      const t = setTimeout(() => update({ search: text.trim() || null, selected: null }, { resetPage: true }), 250)
      return () => clearTimeout(t)
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [text])

    return (
      <div className="toolbar">
        <div className="page-head">
          <div className="page-head__titles">
            <h1>{config.title}</h1>
            {total !== null && (
              <p className="page-head__sub" aria-live="polite">
                {total.toLocaleString()} {total === 1 ? config.noun : `${config.noun}s`}
                {search && <> matching “{search}”</>}
              </p>
            )}
          </div>
          {config.newHref && (
            <div className="page-head__actions">
              <Link className="btn btn--primary" to={config.newHref}>
                <Plus aria-hidden="true" />
                {config.newLabel}
              </Link>
            </div>
          )}
        </div>
        <div className="toolbar__row">
          <label className="search">
            <span className="visually-hidden">{config.searchLabel}</span>
            <Search aria-hidden="true" />
            <input
              className="input input--search"
              type="search"
              value={text}
              placeholder={config.searchPlaceholder}
              onChange={(e) => setText(e.target.value)}
            />
          </label>
          <div className="seg" role="group" aria-label="View">
            <button type="button" className="seg__btn" aria-pressed={view === 'table'} onClick={() => update({ view: null })}>
              <Rows3 aria-hidden="true" /> Table
            </button>
            <button type="button" className="seg__btn" aria-pressed={view === 'grid'} onClick={() => update({ view: 'grid' })}>
              <LayoutGrid aria-hidden="true" /> Grid
            </button>
          </div>
        </div>
      </div>
    )
  },
})

// ---------- FLIP: rows travel to their new place when the order changes ----------
function useTravellingRows(keys: string[]) {
  const nodes = useRef(new Map<string, HTMLElement>())
  const previous = useRef(new Map<string, DOMRect>())

  useLayoutEffect(() => {
    const reduce = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
    for (const [key, el] of nodes.current) {
      const before = previous.current.get(key)
      const after = el.getBoundingClientRect()
      if (!reduce && before && Math.abs(before.top - after.top) > 1 && typeof el.animate === 'function') {
        el.animate([{ transform: `translateY(${before.top - after.top}px)` }, { transform: 'translateY(0)' }], {
          duration: 320,
          easing: 'cubic-bezier(0.16, 1, 0.3, 1)',
        })
      }
    }
    previous.current = new Map([...nodes.current].map(([k, el]) => [k, el.getBoundingClientRect()]))
  }, [keys.join('|')])

  return (key: string) => (el: HTMLElement | null) => {
    if (el) nodes.current.set(key, el)
    else nodes.current.delete(key)
  }
}

export interface RecordColumn {
  path: string
  label: string
  /** Sort key sent to the API when this header is clicked. */
  sort?: string
  kind?: 'thumb' | 'tag' | 'title' | 'text'
  /** Hidden on phone-width screens. */
  optional?: boolean
}

/** Keyboard walking shared by table and grid: arrows move the selection, Enter opens, Escape clears. */
function useSelectionKeys(ids: string[], selected: string | null, select: (id: string | null) => void, open: (id: string) => void) {
  return (e: KeyboardEvent) => {
    const index = selected ? ids.indexOf(selected) : -1
    const step = e.key === 'ArrowDown' || e.key === 'ArrowRight' ? 1 : e.key === 'ArrowUp' || e.key === 'ArrowLeft' ? -1 : 0
    if (step !== 0 && ids.length > 0) {
      e.preventDefault()
      const next = ids[Math.max(0, Math.min(ids.length - 1, index + step))]!
      select(next)
      document.querySelector<HTMLElement>(`[data-row-id="${CSS.escape(next)}"]`)?.focus()
    } else if (e.key === 'Enter' && selected) {
      e.preventDefault()
      open(selected)
    } else if (e.key === 'Escape' && selected) {
      e.preventDefault()
      select(null)
    }
  }
}

// ---------- record table ----------
defineWidget({
  id: 'record-table',
  label: 'Record table',
  slots: { rows: { type: 'list', required: true } },
  config: { columns: [] as RecordColumn[], idPath: 'id', rowHref: '', emptyText: 'Nothing found.', caption: 'Records' },
  print: {},
  component: function RecordTable({ values, config }) {
    const { view, sort, direction, selected, update } = useListParams()
    const navigate = useNavigate()
    const rows = (Array.isArray(values.rows?.value) ? values.rows.value : []) as unknown[]
    const ids = rows.map((r) => rowId(r, config.idPath))
    const ref = useTravellingRows(ids)
    const select = (id: string | null) => update({ selected: id })
    const open = (id: string) => config.rowHref && navigate(fillTemplate(config.rowHref, rows[ids.indexOf(id)]))
    const onKeyDown = useSelectionKeys(ids, selected, select, open)

    if (view !== 'table') return null
    if (rows.length === 0) return <EmptyResult text={config.emptyText} />

    const toggleSort = (key: string) =>
      update({ sort: key === 'inventoryNumber' ? null : key, direction: sort === key && direction === 'asc' ? 'desc' : null, selected: null }, { resetPage: true })

    return (
      <div className="table-wrap">
        <table className="table table--interactive table--records" onKeyDown={onKeyDown}>
          <caption className="visually-hidden">{config.caption}</caption>
          <thead>
            <tr>
              {config.columns.map((c) => (
                <th key={c.path} scope="col" className={columnClass(c)} aria-sort={c.sort && sort === c.sort ? (direction === 'asc' ? 'ascending' : 'descending') : undefined}>
                  {c.sort ? (
                    <button type="button" className="table__sort" aria-current={sort === c.sort} onClick={() => toggleSort(c.sort!)}>
                      {c.label}
                      {sort === c.sort ? direction === 'asc' ? <ArrowUp aria-hidden="true" /> : <ArrowDown aria-hidden="true" /> : <ChevronsUpDown aria-hidden="true" />}
                    </button>
                  ) : (
                    <span className={c.kind === 'thumb' ? 'visually-hidden' : undefined}>{c.label}</span>
                  )}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, i) => {
              const id = ids[i]!
              const isSelected = id === selected
              return (
                <tr
                  key={id}
                  ref={ref(id)}
                  data-row-id={id}
                  tabIndex={isSelected || (!selected && i === 0) ? 0 : -1}
                  aria-selected={isSelected}
                  onClick={() => select(isSelected ? null : id)}
                  onDoubleClick={() => open(id)}
                >
                  {config.columns.map((c) => (
                    <td key={c.path} className={columnClass(c)}>
                      <Cell row={row} column={c} selected={isSelected} href={c.kind === 'title' && config.rowHref ? fillTemplate(config.rowHref, row) : undefined} />
                    </td>
                  ))}
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    )
  },
})

function columnClass(c: RecordColumn): string | undefined {
  return [c.kind === 'thumb' && 'table__thumb', c.optional && 'col-optional'].filter(Boolean).join(' ') || undefined
}

function Cell({ row, column, selected, href }: { row: unknown; column: RecordColumn; selected: boolean; href?: string }) {
  const value = resolvePath(row, column.path)
  switch (column.kind) {
    case 'thumb':
      return <ImagePlate urls={value as ImageUrls | null} size="thumb" alt="" />
    case 'tag':
      return <span className={`tag${selected ? ' tag--selected' : ''}`}>{formatValue(value)}</span>
    case 'title':
      return href ? (
        <Link className="cell-title cell-link cell-clamp" to={href} title={formatValue(value)} onClick={(e) => e.stopPropagation()}>
          {formatValue(value)}
        </Link>
      ) : (
        <span className="cell-title cell-clamp" title={formatValue(value)}>
          {formatValue(value)}
        </span>
      )
    default:
      return (
        <span className={`cell-clamp${value == null ? ' muted' : ''}`} title={formatValue(value) || undefined}>
          {formatValue(value) || '—'}
        </span>
      )
  }
}

function EmptyResult({ text }: { text: string }) {
  const { search, update } = useListParams()
  return (
    <div className="plate empty">
      <strong>{text}</strong>
      {search && (
        <button type="button" className="btn" onClick={() => update({ search: null }, { resetPage: true })}>
          <X aria-hidden="true" /> Clear search “{search}”
        </button>
      )}
    </div>
  )
}

// ---------- record grid ----------
defineWidget({
  id: 'record-grid',
  label: 'Record grid',
  slots: { rows: { type: 'list', required: true } },
  config: { idPath: 'id', imagePath: 'primaryImage', tagPath: 'inventoryNumber', titlePath: 'title', metaPaths: [] as string[], rowHref: '', emptyText: 'Nothing found.' },
  print: {},
  component: function RecordGrid({ values, config }) {
    const { view, selected, update } = useListParams()
    const navigate = useNavigate()
    const rows = (Array.isArray(values.rows?.value) ? values.rows.value : []) as unknown[]
    const ids = rows.map((r) => rowId(r, config.idPath))
    const select = (id: string | null) => update({ selected: id })
    const open = (id: string) => config.rowHref && navigate(fillTemplate(config.rowHref, rows[ids.indexOf(id)]))
    const onKeyDown = useSelectionKeys(ids, selected, select, open)

    if (view !== 'grid') return null
    if (rows.length === 0) return <EmptyResult text={config.emptyText} />

    return (
      <ul className="record-grid" onKeyDown={onKeyDown} aria-label="Records">
        {rows.map((row, i) => {
          const id = ids[i]!
          const isSelected = id === selected
          const meta = config.metaPaths.map((p) => formatValue(resolvePath(row, p))).filter(Boolean)
          return (
            <li key={id}>
              <button
                type="button"
                className="record-card"
                data-row-id={id}
                aria-pressed={isSelected}
                tabIndex={isSelected || (!selected && i === 0) ? 0 : -1}
                onClick={() => select(isSelected ? null : id)}
                onDoubleClick={() => open(id)}
              >
                <ImagePlate urls={resolvePath(row, config.imagePath) as ImageUrls | null} size="thumb" alt="" />
                <span className={`tag record-card__tag${isSelected ? ' tag--selected' : ''}`}>{formatValue(resolvePath(row, config.tagPath))}</span>
                <span className="record-card__title">{formatValue(resolvePath(row, config.titlePath))}</span>
                {meta.length > 0 && <span className="record-card__meta">{meta.join(' · ')}</span>}
              </button>
            </li>
          )
        })}
      </ul>
    )
  },
})

// ---------- pull-out rack ----------
export interface RackFact {
  path: string
  label: string
}

defineWidget({
  id: 'rack-panel',
  label: 'Pull-out rack',
  slots: { rows: { type: 'list', required: true } },
  config: { idPath: 'id', imagePath: 'primaryImage', tagPath: 'inventoryNumber', titlePath: 'title', facts: [] as RackFact[], openHref: '', editHref: '' },
  print: { hideInPrint: true },
  component: function RackPanel({ values, config }) {
    const { selected, update } = useListParams()
    const rows = (Array.isArray(values.rows?.value) ? values.rows.value : []) as unknown[]
    const row = selected ? rows.find((r) => rowId(r, config.idPath) === selected) : undefined
    const titleRef = useRef<HTMLHeadingElement>(null)

    useEffect(() => {
      if (!row) return
      const onKey = (e: globalThis.KeyboardEvent) => {
        if (e.key === 'Escape') update({ selected: null })
      }
      window.addEventListener('keydown', onKey)
      return () => window.removeEventListener('keydown', onKey)
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [row])

    if (!row) return null
    const title = formatValue(resolvePath(row, config.titlePath))
    return (
      <aside className="rack" aria-label={`Preview: ${title}`} key={selected}>
        <div className="rack__head">
          <button type="button" className="btn btn--quiet btn--icon" aria-label="Slide back (Esc)" title="Slide back (Esc)" onClick={() => update({ selected: null })}>
            <X aria-hidden="true" />
          </button>
        </div>
        <div className="rack__mesh">
          <ImagePlate urls={resolvePath(row, config.imagePath) as ImageUrls | null} size="web" alt={title} aspect="4 / 3" />
          <span className="tag tag--selected rack__tag">{formatValue(resolvePath(row, config.tagPath))}</span>
        </div>
        <h2 className="rack__title" ref={titleRef}>
          {title}
        </h2>
        <dl className="rack__facts">
          {config.facts.map((f) => {
            const value = resolvePath(row, f.path)
            const text = formatValue(value)
            if (!text) return null
            return (
              <div key={f.path}>
                <dt className="label">{f.label}</dt>
                <dd>
                  {text}
                  {isHistoricalDate(value) && <span className="muted"> · {formatYearRange(value.earliestYear, value.latestYear)}</span>}
                </dd>
              </div>
            )
          })}
        </dl>
        <div className="rack__actions">
          {config.openHref && (
            <Link className="btn btn--primary" to={fillTemplate(config.openHref, row)}>
              Open record
            </Link>
          )}
          {config.editHref && (
            <Link className="btn" to={fillTemplate(config.editHref, row)}>
              Edit
            </Link>
          )}
        </div>
      </aside>
    )
  },
})

// ---------- pager ----------
defineWidget({
  id: 'pager',
  label: 'Pager',
  slots: { total: { type: 'number', required: true }, page: { type: 'number', required: true }, pageSize: { type: 'number', required: true } },
  config: {},
  print: { hideInPrint: true },
  component: function Pager({ values }) {
    const [, setParams] = useSearchParams()
    const total = Number(values.total?.value ?? 0)
    const page = Number(values.page?.value ?? 1)
    const size = Number(values.pageSize?.value ?? 25)
    const pages = Math.max(1, Math.ceil(total / size))
    if (total === 0) return null
    const go = (p: number) =>
      setParams((prev) => {
        const next = new URLSearchParams(prev)
        if (p <= 1) next.delete('page')
        else next.set('page', String(p))
        next.delete('selected')
        return next
      })
    const from = (page - 1) * size + 1
    const to = Math.min(total, page * size)
    return (
      <nav className="pager" aria-label="Pages">
        <span className="pager__range">
          {from.toLocaleString()}–{to.toLocaleString()} of {total.toLocaleString()}
        </span>
        <div className="pager__controls">
          <button type="button" className="btn btn--sm" disabled={page <= 1} onClick={() => go(page - 1)}>
            <ChevronLeft aria-hidden="true" /> Previous
          </button>
          <span className="pager__page">
            Page {page} of {pages}
          </span>
          <button type="button" className="btn btn--sm" disabled={page >= pages} onClick={() => go(page + 1)}>
            Next <ChevronRight aria-hidden="true" />
          </button>
        </div>
      </nav>
    )
  },
})
