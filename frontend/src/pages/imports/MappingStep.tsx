import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight, Plus, Save, X } from 'lucide-react'
import { useEffect, useId, useMemo, useState } from 'react'
import { api } from '../../api/client'
import { unwrap } from '../../api/problem'
import { useContentLanguages } from '../../app/languages'
import {
  DETECTORS,
  errorMessage,
  fieldLabel,
  FIELDS,
  type ColumnRule,
  type ColumnSummary,
  type ImportField,
  type ImportJob,
  type ImportMapping,
  type ImportReview,
  type LineRule,
} from './importApi'
import { StatusChip } from './parts'

const clean = (s: string | null | undefined) => (s ?? '').replace(/\s+/g, ' ').trim()

/** The rule of a column: matched by heading first, then by position, as the server does. */
function ruleFor(mapping: ImportMapping, column: ColumnSummary): ColumnRule | undefined {
  const heading = clean(column.heading).toLowerCase()
  return mapping.columns.find((c) => heading && clean(c.heading).toLowerCase() === heading) ?? mapping.columns.find((c) => c.column === column.column)
}

type Use = 'ignore' | 'lines' | ImportField

function ruleUse(rule: ColumnRule | undefined): Use {
  if (!rule) return 'ignore'
  if (rule.lines && rule.lines.length > 0) return 'lines'
  return rule.target ?? 'ignore'
}

/** Line rules are matched by content ("d:money") or by position ("p:0"). */
const matcherOf = (r: LineRule) => (r.detector ? `d:${r.detector}` : `p:${r.position ?? 0}`)

function withMatcher(r: LineRule, value: string): LineRule {
  return value.startsWith('d:')
    ? { ...r, detector: value.slice(2) as LineRule['detector'], position: null }
    : { ...r, detector: null, position: Number(value.slice(2)) }
}

const MAX_POSITIONS = 6

/** Maps each column to a field, or splits its lines by content and position, with a live preview of five rows. */
export function MappingStep({ job, onDone }: { job: ImportJob; onDone: () => void }) {
  const queryClient = useQueryClient()
  const [mapping, setMapping] = useState<ImportMapping>(job.mapping!)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const { data: languages = [] } = useContentLanguages()
  const columns = useQuery({
    queryKey: ['imports', job.id, 'columns'],
    queryFn: async () => unwrap(await api.GET('/api/imports/{id}/columns', { params: { path: { id: job.id } } })),
  })

  const setRule = (column: ColumnSummary, rule: ColumnRule) =>
    setMapping((m) => {
      const existing = ruleFor(m, column)
      return { ...m, columns: existing ? m.columns.map((c) => (c === existing ? rule : c)) : [...m.columns, rule] }
    })

  const warnings = useMemo(
    () =>
      (columns.data?.columns ?? [])
        .filter((c) => {
          const rule = ruleFor(mapping, c)
          return rule && ruleUse(rule) === 'lines' && !rule.rest
        })
        .map((c) => `“${clean(c.heading) || `Column ${c.column}`}” has no field for remaining lines: lines that match no rule will not be imported.`),
    [columns.data, mapping],
  )

  const save = async (then?: () => void) => {
    setSaving(true)
    setError(null)
    try {
      const result = unwrap(await api.PUT('/api/imports/{id}/mapping', { params: { path: { id: job.id } }, body: mapping }))
      queryClient.setQueryData(['imports', job.id], result.job)
      await queryClient.invalidateQueries({ queryKey: ['imports', job.id] })
      then?.()
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="imports__step-body">
      <section className="plate imports__section" aria-labelledby="columns-heading">
        <h2 id="columns-heading" className="imports__section-title">
          Columns
        </h2>
        <p className="muted imports__section-sub">
          Send each column to a field, or split its lines: a line goes to the first rule that recognises it, and every other line to the field for remaining lines, so no text is lost.
        </p>
        {columns.isPending ? (
          <div className="data-state" aria-busy="true">
            Loading columns…
          </div>
        ) : columns.isError ? (
          <div className="data-state data-state--error" role="alert">
            Could not load the columns: {columns.error.message}
          </div>
        ) : (
          <ol className="imports__columns">
            {columns.data.columns.map((c) => (
              <ColumnEditor key={c.column} column={c} rule={ruleFor(mapping, c)} onChange={(r) => setRule(c, r)} />
            ))}
          </ol>
        )}
        {warnings.length > 0 && (
          <ul className="notice notice--warning imports__warnings" aria-live="polite">
            {warnings.map((w) => (
              <li key={w}>{w}</li>
            ))}
          </ul>
        )}
      </section>

      <InventoryAndLanguage mapping={mapping} onChange={setMapping} languages={languages} />

      <Preview job={job} mapping={mapping} />

      <div className="imports__actions">
        <TemplateSaver job={job} beforeSave={() => save()} />
        <div className="imports__actions-main">
          {error && (
            <p className="field__error" role="alert">
              {error}
            </p>
          )}
          <button type="button" className="btn" disabled={saving} onClick={() => void save()}>
            <Save aria-hidden="true" /> Save mapping
          </button>
          <button type="button" className="btn btn--primary" disabled={saving} onClick={() => void save(onDone)}>
            Save and continue to places <ArrowRight aria-hidden="true" />
          </button>
        </div>
      </div>
    </div>
  )
}

function ColumnEditor({ column, rule, onChange }: { column: ColumnSummary; rule: ColumnRule | undefined; onChange: (rule: ColumnRule) => void }) {
  const useId_ = useId()
  const restId = useId()
  const name = clean(column.heading) || `Column ${column.column}`
  const use = ruleUse(rule)
  const base: ColumnRule = { column: column.column, heading: column.heading || null }
  const lines = rule?.lines ?? []

  const setUse = (value: Use) => {
    if (value === 'ignore') onChange({ ...base })
    else if (value === 'lines')
      onChange({ ...base, lines: lines.length ? lines : [{ target: 'title', detector: 'itemTitle', continues: false }], rest: rule?.rest ?? 'description' })
    else onChange({ ...base, target: value })
  }

  const setLine = (i: number, line: LineRule) => onChange({ ...base, lines: lines.map((l, j) => (j === i ? line : l)), rest: rule?.rest ?? null })
  const removeLine = (i: number) => {
    const next = lines.filter((_, j) => j !== i)
    onChange(next.length ? { ...base, lines: next, rest: rule?.rest ?? null } : { ...base, target: rule?.rest ?? null })
  }
  const addLine = () => onChange({ ...base, lines: [...lines, { target: 'description', position: lines.length, continues: false }], rest: rule?.rest ?? null })

  return (
    <li className={`imports__column${use === 'ignore' ? ' imports__column--ignored' : ''}`}>
      <div className="imports__column-source">
        <h3 className="imports__column-name">{name}</h3>
        <p className="muted imports__column-meta">
          Column {column.column} · {column.filledRows.toLocaleString()} {column.filledRows === 1 ? 'row' : 'rows'}
        </p>
        {column.samples.length > 0 ? (
          <ul className="imports__samples" aria-label={`Sample cells of ${name}`}>
            {column.samples.map((s, i) => (
              <li key={i}>{s.trim()}</li>
            ))}
          </ul>
        ) : (
          <p className="muted">Empty in the first rows.</p>
        )}
      </div>
      <div className="imports__column-target">
        <div className="field">
          <label className="field__label" htmlFor={useId_}>
            {name}: use as
          </label>
          <select id={useId_} className="select" value={use} onChange={(e) => setUse(e.target.value as Use)}>
            <option value="ignore">Ignore this column</option>
            <option value="lines">Split into lines…</option>
            <optgroup label="Whole cell to one field">
              {FIELDS.map((f) => (
                <option key={f.value} value={f.value}>
                  {f.label}
                </option>
              ))}
            </optgroup>
          </select>
        </div>
        {use === 'lines' && (
          <div className="imports__rules">
            <ol className="imports__rule-list" aria-label={`Line rules of ${name}`}>
              {lines.map((line, i) => (
                <RuleRow key={i} index={i} column={name} line={line} hitRates={column.hitRates} onChange={(l) => setLine(i, l)} onRemove={() => removeLine(i)} />
              ))}
            </ol>
            <button type="button" className="btn btn--quiet btn--sm" aria-label={`Add rule to ${name}`} onClick={addLine}>
              <Plus aria-hidden="true" /> Add rule
            </button>
            <div className="field imports__rest">
              <label className="field__label" htmlFor={restId}>
                Remaining lines of {name} go to
              </label>
              <select
                id={restId}
                className="select"
                value={rule?.rest ?? ''}
                onChange={(e) => onChange({ ...base, lines, rest: (e.target.value || null) as ColumnRule['rest'] })}
              >
                <option value="">Nowhere: not imported</option>
                {FIELDS.map((f) => (
                  <option key={f.value} value={f.value}>
                    {f.label}
                  </option>
                ))}
              </select>
            </div>
          </div>
        )}
      </div>
    </li>
  )
}

function RuleRow({
  index,
  column,
  line,
  hitRates,
  onChange,
  onRemove,
}: {
  index: number
  column: string
  line: LineRule
  hitRates: Record<string, number>
  onChange: (line: LineRule) => void
  onRemove: () => void
}) {
  const matchId = useId()
  const targetId = useId()
  const rate = line.detector ? hitRates[line.detector] : undefined
  return (
    <li className="imports__rule">
      <div className="field">
        <label className="field__label" htmlFor={matchId}>
          Rule {index + 1}: lines
        </label>
        <select id={matchId} className="select" value={matcherOf(line)} onChange={(e) => onChange(withMatcher(line, e.target.value))}>
          <optgroup label="Recognised by content">
            {DETECTORS.map((d) => (
              <option key={d.value} value={`d:${d.value}`}>
                {d.label} (“{d.example}”)
              </option>
            ))}
          </optgroup>
          <optgroup label="By position">
            {Array.from({ length: MAX_POSITIONS }, (_, p) => (
              <option key={p} value={`p:${p}`}>
                Line {p + 1}
              </option>
            ))}
          </optgroup>
        </select>
        {rate !== undefined && <p className="field__hint">Recognises {Math.round(rate * 100)}% of lines in the first rows</p>}
      </div>
      <div className="field">
        <label className="field__label" htmlFor={targetId}>
          Rule {index + 1}: go to
        </label>
        <select id={targetId} className="select" value={line.target ?? 'title'} onChange={(e) => onChange({ ...line, target: e.target.value as ImportField })}>
          {FIELDS.map((f) => (
            <option key={f.value} value={f.value}>
              {f.label}
            </option>
          ))}
        </select>
      </div>
      <label className="imports__check">
        <input type="checkbox" checked={line.continues} onChange={(e) => onChange({ ...line, continues: e.target.checked })} />
        with the unmatched lines after it
      </label>
      <button type="button" className="btn btn--icon btn--sm" aria-label={`Remove rule ${index + 1} of ${column}`} onClick={onRemove}>
        <X aria-hidden="true" />
      </button>
    </li>
  )
}

const METHODS = [
  { value: 'purchase', label: 'Purchase' },
  { value: 'gift', label: 'Gift' },
  { value: 'bequest', label: 'Bequest' },
  { value: 'transfer', label: 'Transfer' },
  { value: 'other', label: 'Other' },
] as const

function InventoryAndLanguage({
  mapping,
  onChange,
  languages,
}: {
  mapping: ImportMapping
  onChange: (m: ImportMapping) => void
  languages: { code: string; name: string }[]
}) {
  const patternId = useId()
  const preferId = useId()
  const languageId = useId()
  const methodId = useId()
  return (
    <section className="plate imports__section" aria-labelledby="numbers-heading">
      <h2 id="numbers-heading" className="imports__section-title">
        Inventory numbers and defaults
      </h2>
      <div className="imports__grid">
        <div className="field">
          <label className="field__label" htmlFor={patternId}>
            Inventory number pattern
          </label>
          <input
            id={patternId}
            className="input imports__pattern"
            value={mapping.inventoryNumber.pattern}
            onChange={(e) => onChange({ ...mapping, inventoryNumber: { ...mapping.inventoryNumber, pattern: e.target.value } })}
            aria-describedby={`${patternId}-hint`}
          />
          <p id={`${patternId}-hint`} className="field__hint">
            Text with {"{group}"}, {"{item}"}, {"{row}"}, {"{sheet}"} (three letters of the sheet name) or {'{field:catalogueNumber}'}; {'{group:2}'} pads to two digits
          </p>
        </div>
        <div className="field">
          <label className="field__label" htmlFor={preferId}>
            Use instead, when the row has one
          </label>
          <select
            id={preferId}
            className="select"
            value={mapping.inventoryNumber.prefer ?? ''}
            onChange={(e) =>
              onChange({ ...mapping, inventoryNumber: { ...mapping.inventoryNumber, prefer: (e.target.value || null) as ImportField | null } })
            }
          >
            <option value="">Always use the pattern</option>
            {FIELDS.filter((f) => ['catalogueNumber', 'inventoryNumber', 'itemNumber'].includes(f.value)).map((f) => (
              <option key={f.value} value={f.value}>
                {f.label}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label className="field__label" htmlFor={languageId}>
            Language of the workbook
          </label>
          <select id={languageId} className="select" value={mapping.language} onChange={(e) => onChange({ ...mapping, language: e.target.value })}>
            {languages.map((l) => (
              <option key={l.code} value={l.code}>
                {l.name}
              </option>
            ))}
          </select>
          <p className="field__hint">Descriptions are recorded in this language</p>
        </div>
        <div className="field">
          <label className="field__label" htmlFor={methodId}>
            Acquisition method
          </label>
          <select
            id={methodId}
            className="select"
            value={mapping.acquisitionMethod ?? 'purchase'}
            onChange={(e) => onChange({ ...mapping, acquisitionMethod: e.target.value as ImportMapping['acquisitionMethod'] })}
          >
            {METHODS.map((m) => (
              <option key={m.value} value={m.value}>
                {m.label}
              </option>
            ))}
          </select>
        </div>
      </div>
    </section>
  )
}

/** The first five rows as they would be imported with the mapping on screen, refreshed as it changes. */
function Preview({ job, mapping }: { job: ImportJob; mapping: ImportMapping }) {
  const [debounced, setDebounced] = useState(mapping)
  useEffect(() => {
    const t = setTimeout(() => setDebounced(mapping), 350)
    return () => clearTimeout(t)
  }, [mapping])

  const preview = useQuery({
    queryKey: ['imports', job.id, 'preview', debounced],
    queryFn: async () => unwrap(await api.POST('/api/imports/{id}/preview', { params: { path: { id: job.id } }, body: { mapping: debounced } })),
    placeholderData: (previous) => previous,
    retry: false,
  })

  return (
    <section className="plate imports__section" aria-labelledby="preview-heading" aria-busy={preview.isFetching}>
      <h2 id="preview-heading" className="imports__section-title">
        Preview of the first rows
      </h2>
      {preview.isError ? (
        <p className="notice notice--error" role="alert">
          {errorMessage(preview.error)}
        </p>
      ) : preview.data ? (
        <PreviewRows review={preview.data} />
      ) : (
        <div className="data-state" aria-busy="true">
          Reading rows…
        </div>
      )}
    </section>
  )
}

export function PreviewRows({ review }: { review: ImportReview }) {
  return (
    <ol className="imports__preview">
      {review.rows.map((row) => (
        <li key={row.sheetRow} className="imports__preview-row">
          <div className="imports__preview-head">
            <span className="tag">{row.inventoryNumber ?? 'No number'}</span>
            <strong>{row.title ?? <span className="muted">No title</span>}</strong>
            <span className="muted">Row {row.sheetRow}</span>
            <StatusChip status={row.status} />
          </div>
          <dl className="imports__fields">
            {row.fields
              .filter((f) => f.field !== 'title')
              .map((f) => (
                <div key={f.field}>
                  <dt>{fieldLabel(f.field)}</dt>
                  <dd>{f.value}</dd>
                </div>
              ))}
          </dl>
          {row.problems.length > 0 && (
            <ul className="imports__problems">
              {row.problems.map((p, i) => (
                <li key={i}>
                  <strong>{fieldLabel(p.field)}:</strong> {p.reason}
                  {p.text && <span className="muted"> (“{p.text}”)</span>}
                </li>
              ))}
            </ul>
          )}
        </li>
      ))}
    </ol>
  )
}

/** Saves the job's mapping and decisions as a template (updating the job's template when the name is unchanged). */
export function TemplateSaver({ job, beforeSave }: { job: ImportJob; beforeSave?: () => Promise<void> }) {
  const queryClient = useQueryClient()
  const nameId = useId()
  const [name, setName] = useState(job.template?.name ?? '')
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)

  const save = async () => {
    setMessage(null)
    try {
      await beforeSave?.()
      const existing = job.template && name.trim() === job.template.name
      const template = existing
        ? unwrap(await api.PUT('/api/import-templates/{id}', { params: { path: { id: job.template!.id } }, body: { name, fromJobId: job.id } }))
        : unwrap(await api.POST('/api/import-templates', { body: { name, fromJobId: job.id } }))
      await queryClient.invalidateQueries({ queryKey: ['import-templates'] })
      setMessage({ ok: true, text: `Saved as template “${template.name}”.` })
    } catch (e) {
      setMessage({ ok: false, text: errorMessage(e) })
    }
  }

  return (
    <div className="imports__template">
      <div className="field">
        <label className="field__label" htmlFor={nameId}>
          Template name
        </label>
        <input id={nameId} className="input" value={name} placeholder="e.g. Collection workbook 2024" onChange={(e) => setName(e.target.value)} />
      </div>
      <button type="button" className="btn" disabled={!name.trim()} onClick={() => void save()}>
        Save as template
      </button>
      <p className={message?.ok === false ? 'field__error' : 'muted'} aria-live="polite">
        {message?.text}
      </p>
    </div>
  )
}
