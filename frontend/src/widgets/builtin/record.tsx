import { ArrowLeft, Pencil, Printer } from 'lucide-react'
import { Link } from 'react-router'
import { defineWidget } from '../core/registry'
import { formatValue, formatYearRange, isHistoricalDate } from '../format'
import { fillTemplate } from './display'
import './record.css'

const ICONS = { edit: Pencil, print: Printer, back: ArrowLeft } as const

export interface RecordAction {
  label: string
  /** Route template; `{id}` is replaced with the bound id. */
  href: string
  icon?: keyof typeof ICONS
  primary?: boolean
}

// ---------- record actions: links built from the record id ----------
defineWidget({
  id: 'record-actions',
  label: 'Record actions',
  slots: { id: { type: 'reference', required: true } },
  config: { actions: [] as RecordAction[] },
  print: { hideInPrint: true },
  component: ({ values, config }) => (
    <div className="record-actions">
      {config.actions.map((a) => {
        const Icon = a.icon ? ICONS[a.icon] : null
        return (
          <Link key={a.href} className={`btn${a.primary ? ' btn--primary' : ''}`} to={fillTemplate(a.href, { id: values.id?.value })}>
            {Icon && <Icon aria-hidden="true" />}
            {a.label}
          </Link>
        )
      })}
    </div>
  ),
})

// ---------- text block: long-form text with preserved paragraphs ----------
defineWidget({
  id: 'text-block',
  label: 'Text block',
  slots: { text: { type: 'text', required: true } },
  config: { emptyText: '' },
  print: {},
  component: ({ values, config }) => {
    const text = formatValue(values.text?.value)
    if (!text) return config.emptyText ? <p className="muted">{config.emptyText}</p> : null
    return (
      <div className="text-block">
        {text.split(/\n{2,}/).map((para, i) => (
          <p key={i}>{para}</p>
        ))}
      </div>
    )
  },
})

// ---------- text list: an ordered list of free-text entries (e.g. exhibitions) ----------
defineWidget({
  id: 'text-list',
  label: 'Text list',
  slots: { items: { type: 'any', required: true } },
  config: { emptyText: '' },
  print: { keepTogether: true },
  component: ({ values, config }) => {
    const items = (Array.isArray(values.items?.value) ? values.items.value : []).map((v) => formatValue(v)).filter((t) => t !== '')
    if (items.length === 0) return config.emptyText ? <p className="muted">{config.emptyText}</p> : null
    return (
      <ol className="text-list">
        {items.map((text, i) => (
          <li key={i}>{text}</li>
        ))}
      </ol>
    )
  },
})

// ---------- list row: one step in an ordered sequence (e.g. provenance) ----------
defineWidget({
  id: 'list-row',
  label: 'List row',
  slots: { primary: { type: 'any', required: true }, secondary: { type: 'any' }, note: { type: 'any' } },
  config: {},
  print: { keepTogether: true },
  component: ({ values }) => {
    const secondary = values.secondary?.value
    const note = formatValue(values.note?.value, values.note?.type)
    return (
      <div className="list-row">
        <span className="list-row__primary">{formatValue(values.primary?.value, values.primary?.type)}</span>
        {secondary != null && (
          <span className="list-row__secondary">
            {formatValue(secondary, values.secondary?.type)}
            {isHistoricalDate(secondary) && (
              <span className="muted"> · {formatYearRange(secondary.earliestYear, secondary.latestYear)}</span>
            )}
          </span>
        )}
        {note && <span className="list-row__note">{note}</span>}
      </div>
    )
  },
})
