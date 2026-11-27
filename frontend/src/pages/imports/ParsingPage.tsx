import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Copy, Pencil, Plus, Trash2 } from 'lucide-react'
import { useEffect, useId, useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { api } from '../../api/client'
import { ApiError, unwrap } from '../../api/problem'
import {
  errorMessage,
  useImportDetectors,
  useImportLookupTables,
  type DetectorDefinition,
  type ImportDetector,
  type ImportLookupTable,
} from './importApi'
import './imports.css'

type Tab = 'detectors' | 'tables'

/** Field errors of a failed save by field name, for showing them next to the field. */
function fieldErrors(e: unknown): Record<string, string> {
  if (!(e instanceof ApiError)) return {}
  return Object.fromEntries(Object.entries(e.fieldErrors).map(([k, v]) => [k, v[0] ?? '']))
}

/**
 * How imports read workbook text: the detectors that recognise lines of a cell, and the tables of words (months,
 * currencies, measurement labels) they look up. Built-in entries are read-only; a user's own extend or override them.
 */
export function ParsingPage() {
  const [tab, setTab] = useState<Tab>('detectors')

  useEffect(() => {
    document.title = 'Import parsing · Artefacts'
  }, [])

  return (
    <div className="imports">
      <div className="page-head">
        <div className="page-head__titles">
          <p className="page-head__sub">
            <Link to="/imports">Import</Link>
          </p>
          <h1>Parsing</h1>
          <p className="page-head__sub">Teach imports to recognise lines and words your workbooks use. Built-in ones cannot be changed; copy one to customise it.</p>
        </div>
      </div>
      <div className="seg" role="tablist" aria-label="Parsing">
        <button type="button" role="tab" aria-selected={tab === 'detectors'} className={`seg__btn${tab === 'detectors' ? ' active' : ''}`} onClick={() => setTab('detectors')}>
          Detectors
        </button>
        <button type="button" role="tab" aria-selected={tab === 'tables'} className={`seg__btn${tab === 'tables' ? ' active' : ''}`} onClick={() => setTab('tables')}>
          Lookup tables
        </button>
      </div>
      {tab === 'detectors' ? <DetectorsSection /> : <TablesSection />}
    </div>
  )
}

// ---------- detectors ----------

function DetectorsSection() {
  const queryClient = useQueryClient()
  const { detectors, isPending, isError, error } = useImportDetectors()
  const [editing, setEditing] = useState<ImportDetector | 'new' | null>(null)
  const [rowError, setRowError] = useState<string | null>(null)
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['import-detectors'] })

  const duplicate = async (d: ImportDetector) => {
    setRowError(null)
    try {
      unwrap(await api.POST('/api/imports/detectors/{id}/duplicate', { params: { path: { id: d.id } }, body: { name: `${d.label} copy` } }))
      await refresh()
    } catch (e) {
      setRowError(errorMessage(e))
    }
  }

  const remove = async (d: ImportDetector) => {
    if (!window.confirm(`Delete the detector “${d.label}”?`)) return
    setRowError(null)
    try {
      unwrap(await api.DELETE('/api/imports/detectors/{id}', { params: { path: { id: d.id } } }))
      if (typeof editing === 'object' && editing?.id === d.id) setEditing(null)
      await refresh()
    } catch (e) {
      setRowError(errorMessage(e))
    }
  }

  if (isPending) return <div className="data-state" aria-busy="true">Loading…</div>
  if (isError)
    return (
      <div className="data-state data-state--error" role="alert">
        Could not load detectors: {error.message}
      </div>
    )

  return (
    <>
      <div>
        <button type="button" className="btn btn--primary" onClick={() => setEditing('new')}>
          <Plus aria-hidden="true" /> New detector
        </button>
      </div>
      {rowError && (
        <p className="field__error" role="alert">
          {rowError}
        </p>
      )}
      <div className="table-wrap">
        <table className="table">
          <caption className="visually-hidden">Detectors</caption>
          <thead>
            <tr>
              <th scope="col">Detector</th>
              <th scope="col">Example</th>
              <th scope="col">
                <span className="visually-hidden">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {detectors.map((d) => (
              <tr key={d.id}>
                <td>
                  <span className="cell-title">{d.label}</span> {d.isSystem && <span className="tag">Built-in</span>}
                </td>
                <td data-label="Example">{d.example}</td>
                <td>
                  {d.isSystem ? (
                    <span className="field__hint">Read-only</span>
                  ) : (
                    <>
                      <button type="button" className="btn btn--sm" aria-label={`Edit ${d.label}`} onClick={() => setEditing(d)}>
                        <Pencil aria-hidden="true" /> Edit
                      </button>
                      <button type="button" className="btn btn--sm" aria-label={`Duplicate ${d.label}`} onClick={() => void duplicate(d)}>
                        <Copy aria-hidden="true" /> Duplicate
                      </button>
                      <button type="button" className="btn btn--sm btn--danger" aria-label={`Delete ${d.label}`} onClick={() => void remove(d)}>
                        <Trash2 aria-hidden="true" /> Delete
                      </button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {editing && (
        <DetectorEditor
          key={editing === 'new' ? 'new' : editing.id}
          detector={editing === 'new' ? null : editing}
          onSaved={async () => {
            setEditing(null)
            await refresh()
          }}
          onCancel={() => setEditing(null)}
        />
      )}
    </>
  )
}

const BLANK: DetectorDefinition = { kind: 'regex', pattern: '', ignoreCase: true, parse: 'none', keywords: [], match: 'contains', example: '' }

function DetectorEditor({ detector, onSaved, onCancel }: { detector: ImportDetector | null; onSaved: () => Promise<unknown>; onCancel: () => void }) {
  const id = useId()
  const [name, setName] = useState(detector?.label ?? '')
  const [def, setDef] = useState<DetectorDefinition>({ ...BLANK, ...detector?.definition })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)
  const set = (patch: Partial<DetectorDefinition>) => setDef((d) => ({ ...d, ...patch }))

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setErrors({})
    try {
      const body = { name, definition: def }
      if (detector) unwrap(await api.PUT('/api/imports/detectors/{id}', { params: { path: { id: detector.id } }, body }))
      else unwrap(await api.POST('/api/imports/detectors', { body }))
      await onSaved()
    } catch (err) {
      const found = fieldErrors(err)
      setErrors(Object.keys(found).length > 0 ? found : { form: errorMessage(err) })
    } finally {
      setSaving(false)
    }
  }

  const input = (key: string, label: string, control: React.ReactNode, hint?: string) => (
    <div className={`field${errors[key] ? ' field--invalid' : ''}`}>
      <label className="field__label" htmlFor={`${id}-${key}`}>
        {label}
      </label>
      {control}
      {hint && <p className="field__hint">{hint}</p>}
      {errors[key] && (
        <p className="field__error" role="alert">
          {errors[key]}
        </p>
      )}
    </div>
  )

  return (
    <form className="plate" onSubmit={submit} aria-label={detector ? `Edit ${detector.label}` : 'New detector'}>
      <h2 className="imports__section-title">{detector ? `Edit ${detector.label}` : 'New detector'}</h2>
      {input('name', 'Name', <input id={`${id}-name`} className="input" value={name} required onChange={(e) => setName(e.target.value)} />)}
      {input(
        'definition.kind',
        'Recognises lines by',
        <select id={`${id}-definition.kind`} className="input" value={def.kind} onChange={(e) => set({ kind: e.target.value as DetectorDefinition['kind'] })}>
          <option value="regex">A pattern</option>
          <option value="keywords">Keywords</option>
        </select>,
      )}
      {def.kind === 'regex' ? (
        <>
          {input(
            'definition.pattern',
            'Pattern',
            <input id={`${id}-definition.pattern`} className="input" value={def.pattern ?? ''} onChange={(e) => set({ pattern: e.target.value })} spellCheck={false} />,
            'A regular expression. Name the parts to keep: (?<value>…) is the recognised text, (?<remainder>…) what is left of the line, (?<item>…) an item number.',
          )}
          {input(
            'definition.parse',
            'Read the value as',
            <select id={`${id}-definition.parse`} className="input" value={def.parse ?? 'none'} onChange={(e) => set({ parse: e.target.value as DetectorDefinition['parse'] })}>
              <option value="none">Plain text</option>
              <option value="number">A number</option>
              <option value="calendarDate">A calendar date</option>
              <option value="historicalDate">A historical date</option>
              <option value="money">An amount of money</option>
            </select>,
          )}
        </>
      ) : (
        <>
          {input(
            'definition.keywords',
            'Keywords',
            <textarea
              id={`${id}-definition.keywords`}
              className="input"
              rows={4}
              value={(def.keywords ?? []).join('\n')}
              onChange={(e) => set({ keywords: e.target.value.split('\n') })}
            />,
            'One per line.',
          )}
          {input(
            'definition.match',
            'A line matches when it',
            <select id={`${id}-definition.match`} className="input" value={def.match ?? 'contains'} onChange={(e) => set({ match: e.target.value as DetectorDefinition['match'] })}>
              <option value="contains">contains a keyword</option>
              <option value="startsWith">starts with a keyword</option>
              <option value="equals">is a keyword</option>
            </select>,
          )}
        </>
      )}
      <label className="check">
        <input type="checkbox" checked={def.ignoreCase} onChange={(e) => set({ ignoreCase: e.target.checked })} /> Ignore capital letters
      </label>
      {input('definition.example', 'Example line', <input id={`${id}-definition.example`} className="input" value={def.example ?? ''} onChange={(e) => set({ example: e.target.value })} />)}

      <TryPanel definition={def} example={def.example ?? ''} />

      {errors.form && (
        <p className="field__error" role="alert">
          {errors.form}
        </p>
      )}
      <div>
        <button type="submit" className="btn btn--primary" disabled={saving || !name.trim()}>
          Save detector
        </button>{' '}
        <button type="button" className="btn" onClick={onCancel}>
          Cancel
        </button>
      </div>
    </form>
  )
}

/** Runs the definition being edited against sample lines as it changes, without saving it. */
function TryPanel({ definition, example }: { definition: DetectorDefinition; example: string }) {
  const id = useId()
  const [text, setText] = useState('')
  const lines = (text || example).split('\n').filter((l) => l.trim().length > 0)
  const key = JSON.stringify([definition, lines])
  const [debounced, setDebounced] = useState(key)
  useEffect(() => {
    const t = setTimeout(() => setDebounced(key), 400)
    return () => clearTimeout(t)
  }, [key])

  const { data, error } = useQuery({
    queryKey: ['try-import-detector', debounced],
    enabled: lines.length > 0,
    retry: false,
    queryFn: async () => {
      const [def, sample] = JSON.parse(debounced) as [DetectorDefinition, string[]]
      return unwrap(await api.POST('/api/imports/detectors/try', { body: { definition: def, lines: sample } }))
    },
  })

  return (
    <section aria-labelledby={`${id}-h`}>
      <h3 id={`${id}-h`} className="imports__section-title">
        Try it
      </h3>
      <div className="field">
        <label className="field__label" htmlFor={id}>
          Sample lines
        </label>
        <textarea id={id} className="input" rows={3} value={text} placeholder={example || 'One line per row'} onChange={(e) => setText(e.target.value)} />
      </div>
      {error && (
        <p className="field__hint" role="status">
          {Object.values(fieldErrors(error))[0] ?? errorMessage(error)}
        </p>
      )}
      {!error && data && (
        <div className="table-wrap">
          <table className="table">
            <caption className="visually-hidden">Result for each sample line</caption>
            <thead>
              <tr>
                <th scope="col">Line</th>
                <th scope="col">Recognised</th>
                <th scope="col">Value</th>
                <th scope="col">Rest of the line</th>
              </tr>
            </thead>
            <tbody>
              {data.lines.map((l, i) => (
                <tr key={i}>
                  <td>{l.line}</td>
                  <td>{l.matched ? l.text : <span className="field__hint">No match</span>}</td>
                  <td>
                    {l.parsed != null && (typeof l.parsed === 'object' ? JSON.stringify(l.parsed) : String(l.parsed))}
                    {l.guessed && <span className="tag"> Guessed</span>}
                  </td>
                  <td>{l.remainder}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

// ---------- lookup tables ----------

const MEASURES = ['width', 'height', 'length', 'diameter']

function TablesSection() {
  const { tables, isPending, isError, error } = useImportLookupTables()
  if (isPending) return <div className="data-state" aria-busy="true">Loading…</div>
  if (isError)
    return (
      <div className="data-state data-state--error" role="alert">
        Could not load lookup tables: {error.message}
      </div>
    )
  return (
    <>
      {tables.map((t) => (
        <TableEditor key={t.id} table={t} />
      ))}
    </>
  )
}

type Row = { word: string; value: string }

function TableEditor({ table }: { table: ImportLookupTable }) {
  const queryClient = useQueryClient()
  const id = useId()
  const initial = table.entries.filter((e) => e.isUser).map((e) => ({ word: e.word, value: e.value }))
  const [rows, setRows] = useState<Row[]>(initial)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)
  const builtIn = table.entries.filter((e) => !e.isUser || e.builtInValue != null)
  const dirty = JSON.stringify(rows) !== JSON.stringify(initial)

  const update = (i: number, patch: Partial<Row>) => {
    setSaved(false)
    setRows((r) => r.map((row, j) => (j === i ? { ...row, ...patch } : row)))
  }

  const save = async () => {
    setSaving(true)
    setErrors({})
    setSaved(false)
    try {
      unwrap(await api.PUT('/api/imports/lookup-tables/{id}', { params: { path: { id: table.id } }, body: { entries: rows } }))
      await queryClient.invalidateQueries({ queryKey: ['import-lookup-tables'] })
      setSaved(true)
    } catch (e) {
      const found = fieldErrors(e)
      setErrors(Object.keys(found).length > 0 ? found : { form: errorMessage(e) })
    } finally {
      setSaving(false)
    }
  }

  const valueInput = (row: Row, i: number) => {
    const label = `Value of ${row.word || 'new entry'}`
    if (table.id === 'measureLabels')
      return (
        <select className="input" aria-label={label} value={row.value} onChange={(e) => update(i, { value: e.target.value })}>
          <option value="">Choose…</option>
          {MEASURES.map((m) => (
            <option key={m}>{m}</option>
          ))}
        </select>
      )
    return <input className="input" aria-label={label} value={row.value} onChange={(e) => update(i, { value: e.target.value })} readOnly={table.id === 'inchAliases'} />
  }

  return (
    <section className="plate" aria-labelledby={id}>
      <h2 id={id} className="imports__section-title">
        {table.label}
      </h2>
      <details>
        <summary>
          {builtIn.length} built-in {builtIn.length === 1 ? 'entry' : 'entries'}
        </summary>
        <p>
          {builtIn.map((e) => (
            <span key={e.word} className="tag" title={e.isUser ? `Built-in: ${e.builtInValue}` : undefined}>
              {e.word} → {e.isUser ? e.builtInValue : e.value}{' '}
            </span>
          ))}
        </p>
      </details>
      <h3 className="field__label">Your entries</h3>
      {rows.length === 0 && <p className="field__hint">None. Add a word to extend the table, or use a built-in word to change what it means.</p>}
      {rows.map((row, i) => (
        <div key={i} className={`field${errors[`entries[${i}].word`] || errors[`entries[${i}].value`] ? ' field--invalid' : ''}`}>
          <input className="input" aria-label={`Word ${i + 1}`} value={row.word} onChange={(e) => update(i, { word: e.target.value })} />{' '}
          {valueInput(row, i)}{' '}
          <button type="button" className="btn btn--sm btn--danger" aria-label={`Remove ${row.word || 'entry'}`} onClick={() => setRows((r) => r.filter((_, j) => j !== i))}>
            <Trash2 aria-hidden="true" />
          </button>
          {(errors[`entries[${i}].word`] ?? errors[`entries[${i}].value`]) && (
            <p className="field__error" role="alert">
              {errors[`entries[${i}].word`] ?? errors[`entries[${i}].value`]}
            </p>
          )}
        </div>
      ))}
      {errors.entries && (
        <p className="field__error" role="alert">
          {errors.entries}
        </p>
      )}
      {errors.form && (
        <p className="field__error" role="alert">
          {errors.form}
        </p>
      )}
      <div>
        <button type="button" className="btn" onClick={() => setRows((r) => [...r, { word: '', value: table.id === 'inchAliases' ? 'in' : '' }])}>
          <Plus aria-hidden="true" /> Add entry
        </button>{' '}
        <button type="button" className="btn btn--primary" disabled={saving || !dirty} onClick={() => void save()}>
          Save {table.label.toLowerCase()}
        </button>
        <span aria-live="polite"> {saved && 'Saved.'}</span>
      </div>
    </section>
  )
}
