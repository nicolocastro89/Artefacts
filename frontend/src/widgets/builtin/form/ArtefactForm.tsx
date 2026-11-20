import { useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, Plus, Trash2 } from 'lucide-react'
import { useId, useState, type FormEvent, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { api, type AcquisitionMethod, type ArtefactWriteRequest, type HistoricalDateInput } from '../../../api/client'
import { ApiError, unwrap } from '../../../api/problem'
import { defineWidget } from '../../core/registry'
import { isHistoricalDate, isMoney } from '../../format'
import { HistoricalDateField } from './HistoricalDateField'
import { RegionPicker, type PickedRegion } from './RegionPicker'
import './form.css'

interface ProvenanceRow {
  key: string
  region: PickedRegion | null
  period: HistoricalDateInput
  note: string
}

interface FormState {
  inventoryNumber: string
  title: string
  maker: string
  creationDate: HistoricalDateInput
  medium: string
  dimensions: string
  description: string
  method: AcquisitionMethod | ''
  acquisitionDate: string
  source: string
  priceAmount: string
  priceCurrency: string
  provenance: ProvenanceRow[]
}

const METHODS: { value: AcquisitionMethod; label: string }[] = [
  { value: 'purchase', label: 'Purchase' },
  { value: 'gift', label: 'Gift' },
  { value: 'bequest', label: 'Bequest' },
  { value: 'transfer', label: 'Transfer' },
  { value: 'other', label: 'Other' },
]

const COMMON_CURRENCIES = ['EUR', 'USD', 'GBP', 'CHF', 'JPY', 'SEK', 'DKK', 'NOK', 'CAD', 'AUD']

let rowSeq = 0
const newRowKey = () => `row-${++rowSeq}`

function str(v: unknown): string {
  return typeof v === 'string' ? v : ''
}

function dateInput(v: unknown): HistoricalDateInput {
  return isHistoricalDate(v) ? { text: v.display, earliestYear: null, latestYear: null } : { text: '' }
}

function initialState(values: Record<string, { value: unknown }>): FormState {
  const price = values.acquisitionPrice?.value
  const provenance = Array.isArray(values.provenance?.value) ? (values.provenance.value as Record<string, unknown>[]) : []
  return {
    inventoryNumber: str(values.inventoryNumber?.value),
    title: str(values.title?.value),
    maker: str(values.maker?.value),
    creationDate: dateInput(values.creationDate?.value),
    medium: str(values.medium?.value),
    dimensions: str(values.dimensions?.value),
    description: str(values.description?.value),
    method: (str(values.acquisitionMethod?.value) as AcquisitionMethod) || '',
    acquisitionDate: str(values.acquisitionDate?.value),
    source: str(values.acquisitionSource?.value),
    priceAmount: isMoney(price) ? String(price.amount) : '',
    priceCurrency: isMoney(price) ? price.currency : 'EUR',
    provenance: provenance.map((p) => ({
      key: newRowKey(),
      region: typeof p.regionId === 'string' ? { id: p.regionId, name: str(p.regionName) } : null,
      period: dateInput(p.period),
      note: str(p.note),
    })),
  }
}

function toRequest(s: FormState): ArtefactWriteRequest {
  const opt = (v: string) => (v.trim() === '' ? null : v.trim())
  const dateOrNull = (d: HistoricalDateInput) =>
    (d.text ?? '').trim() === '' ? null : { text: d.text, earliestYear: d.earliestYear ?? null, latestYear: d.latestYear ?? null }
  const amount = s.priceAmount.trim() === '' ? null : Number(s.priceAmount.replace(',', '.'))
  return {
    inventoryNumber: s.inventoryNumber,
    title: s.title,
    maker: opt(s.maker),
    creationDate: dateOrNull(s.creationDate),
    medium: opt(s.medium),
    dimensions: opt(s.dimensions),
    description: opt(s.description),
    acquisition: s.method
      ? {
          method: s.method,
          date: opt(s.acquisitionDate),
          source: opt(s.source),
          price: amount === null ? null : { amount, currency: s.priceCurrency.trim().toUpperCase() },
        }
      : null,
    provenance: s.provenance.map((p) => ({
      regionId: p.region?.id ?? '00000000-0000-0000-0000-000000000000',
      period: { text: p.period.text, earliestYear: p.period.earliestYear ?? null, latestYear: p.period.latestYear ?? null },
      note: opt(p.note),
    })),
  }
}

/** Errors for a field: exact key plus nested keys below it (e.g. `acquisition.price` collects `.currency`). */
function errorsFor(errors: Record<string, string[]>, key: string): string[] | undefined {
  const found = Object.entries(errors)
    .filter(([k]) => k === key || k.startsWith(`${key}.`))
    .flatMap(([, v]) => v)
  return found.length > 0 ? found : undefined
}

function TextField({ label, value, onChange, errors, required, multiline, hint }: { label: string; value: string; onChange: (v: string) => void; errors?: string[]; required?: boolean; multiline?: boolean; hint?: ReactNode }) {
  const id = useId()
  return (
    <div className={`field${errors ? ' field--invalid' : ''}`}>
      <label className="field__label" htmlFor={id}>
        {label}
        {required && <span aria-hidden="true"> *</span>}
      </label>
      {multiline ? (
        <textarea id={id} className="textarea" value={value} aria-invalid={!!errors} onChange={(e) => onChange(e.target.value)} />
      ) : (
        <input id={id} className="input" value={value} required={required} aria-invalid={!!errors} onChange={(e) => onChange(e.target.value)} />
      )}
      {hint && <p className="field__hint">{hint}</p>}
      {errors?.map((e) => (
        <p key={e} className="field__error">
          {e}
        </p>
      ))}
    </div>
  )
}

defineWidget({
  id: 'artefact-form',
  label: 'Artefact form',
  slots: {
    id: { type: 'reference' },
    inventoryNumber: { type: 'text' },
    title: { type: 'text' },
    maker: { type: 'text' },
    creationDate: { type: 'historical-date' },
    medium: { type: 'text' },
    dimensions: { type: 'text' },
    description: { type: 'text' },
    acquisitionMethod: { type: 'text' },
    acquisitionDate: { type: 'date' },
    acquisitionSource: { type: 'text' },
    acquisitionPrice: { type: 'money' },
    provenance: { type: 'list' },
  },
  config: { cancelHref: '/artefacts' },
  print: { hideInPrint: true },
  component: function ArtefactForm({ values, config }) {
    const artefactId = typeof values.id?.value === 'string' ? values.id.value : null
    const navigate = useNavigate()
    const queryClient = useQueryClient()
    const [state, setState] = useState<FormState>(() => initialState(values))
    const [errors, setErrors] = useState<Record<string, string[]>>({})
    const [summary, setSummary] = useState<string | null>(null)
    const [saving, setSaving] = useState(false)

    const set = <K extends keyof FormState>(key: K, value: FormState[K]) => setState((s) => ({ ...s, [key]: value }))
    const setRow = (index: number, patch: Partial<ProvenanceRow>) =>
      setState((s) => ({ ...s, provenance: s.provenance.map((r, i) => (i === index ? { ...r, ...patch } : r)) }))
    const moveRow = (index: number, by: number) =>
      setState((s) => {
        const rows = [...s.provenance]
        const [row] = rows.splice(index, 1)
        rows.splice(index + by, 0, row!)
        return { ...s, provenance: rows }
      })

    const submit = async (e: FormEvent) => {
      e.preventDefault()
      const missingRegions = Object.fromEntries(
        state.provenance.flatMap((r, i) => (r.region ? [] : [[`provenance[${i}].regionId`, ['Choose a region, or add a new one.']]])),
      )
      if (Object.keys(missingRegions).length > 0) {
        setErrors(missingRegions)
        setSummary('Some fields need attention. The problems are shown next to each field.')
        return
      }
      setSaving(true)
      setErrors({})
      setSummary(null)
      try {
        const body = toRequest(state)
        const saved = artefactId
          ? unwrap(await api.PUT('/api/artefacts/{id}', { params: { path: { id: artefactId } }, body }))
          : unwrap(await api.POST('/api/artefacts', { body }))
        await queryClient.invalidateQueries({ queryKey: ['operation'] })
        await queryClient.invalidateQueries({ queryKey: ['count'] })
        navigate(`/artefacts/${saved.id}`)
      } catch (err) {
        if (err instanceof ApiError && err.status === 400) {
          setErrors(err.fieldErrors)
          setSummary('Some fields need attention. The problems are shown next to each field.')
        } else {
          setSummary(err instanceof Error ? `Could not save: ${err.message}` : 'Could not save.')
        }
      } finally {
        setSaving(false)
      }
    }

    return (
      <form className="record-form" onSubmit={submit} noValidate>
        {summary && (
          <div className="notice notice--error" role="alert">
            {summary}
          </div>
        )}

        <fieldset className="form-section">
          <legend>Identity</legend>
          <div className="form-grid">
            <TextField label="Inventory number" required value={state.inventoryNumber} onChange={(v) => set('inventoryNumber', v)} errors={errorsFor(errors, 'inventoryNumber')} />
            <TextField label="Title" required value={state.title} onChange={(v) => set('title', v)} errors={errorsFor(errors, 'title')} />
          </div>
        </fieldset>

        <fieldset className="form-section">
          <legend>Catalogue</legend>
          <div className="form-grid">
            <TextField label="Maker" value={state.maker} onChange={(v) => set('maker', v)} errors={errorsFor(errors, 'maker')} />
            <HistoricalDateField label="Date" value={state.creationDate} onChange={(v) => set('creationDate', v)} errors={errorsFor(errors, 'creationDate')} />
            <TextField label="Medium" value={state.medium} onChange={(v) => set('medium', v)} errors={errorsFor(errors, 'medium')} />
            <TextField label="Dimensions" value={state.dimensions} onChange={(v) => set('dimensions', v)} errors={errorsFor(errors, 'dimensions')} hint="As written, e.g. 82 × 64 cm (framed 96 × 78 cm)" />
          </div>
          <TextField label="Description" multiline value={state.description} onChange={(v) => set('description', v)} errors={errorsFor(errors, 'description')} />
        </fieldset>

        <fieldset className="form-section">
          <legend>Acquisition</legend>
          <div className="form-grid">
            <SelectField label="Method" value={state.method} onChange={(v) => set('method', v as AcquisitionMethod | '')} errors={errorsFor(errors, 'acquisition.method')}>
              <option value="">Not recorded</option>
              {METHODS.map((m) => (
                <option key={m.value} value={m.value}>
                  {m.label}
                </option>
              ))}
            </SelectField>
            <DateField label="Date" value={state.acquisitionDate} disabled={!state.method} onChange={(v) => set('acquisitionDate', v)} errors={errorsFor(errors, 'acquisition.date')} />
            <TextField label="Source" value={state.source} onChange={(v) => set('source', v)} errors={errorsFor(errors, 'acquisition.source')} />
            <MoneyField
              amount={state.priceAmount}
              currency={state.priceCurrency}
              disabled={!state.method}
              onAmount={(v) => set('priceAmount', v)}
              onCurrency={(v) => set('priceCurrency', v)}
              errors={errorsFor(errors, 'acquisition.price')}
            />
          </div>
          {!state.method && <p className="field__hint">Choose a method to record the acquisition date and price.</p>}
        </fieldset>

        <fieldset className="form-section">
          <legend>Provenance</legend>
          {errorsFor(errors, 'provenance') && state.provenance.length === 0 && <p className="field__error">{errorsFor(errors, 'provenance')}</p>}
          <ol className="provenance-editor">
            {state.provenance.map((row, i) => (
              <li key={row.key} className="provenance-editor__row">
                <span className="provenance-editor__num" aria-hidden="true">
                  {i + 1}
                </span>
                <RegionPicker label="Region" value={row.region} onChange={(region) => setRow(i, { region })} errors={errorsFor(errors, `provenance[${i}].regionId`)} />
                <HistoricalDateField label="Period" value={row.period} onChange={(period) => setRow(i, { period })} errors={errorsFor(errors, `provenance[${i}].period`)} />
                <TextField label="Note" value={row.note} onChange={(note) => setRow(i, { note })} errors={errorsFor(errors, `provenance[${i}].note`)} />
                <div className="provenance-editor__tools">
                  <button type="button" className="btn btn--icon btn--sm" aria-label={`Move step ${i + 1} up`} disabled={i === 0} onClick={() => moveRow(i, -1)}>
                    <ArrowUp aria-hidden="true" />
                  </button>
                  <button type="button" className="btn btn--icon btn--sm" aria-label={`Move step ${i + 1} down`} disabled={i === state.provenance.length - 1} onClick={() => moveRow(i, 1)}>
                    <ArrowDown aria-hidden="true" />
                  </button>
                  <button type="button" className="btn btn--icon btn--sm btn--danger" aria-label={`Remove step ${i + 1}`} onClick={() => set('provenance', state.provenance.filter((_, j) => j !== i))}>
                    <Trash2 aria-hidden="true" />
                  </button>
                </div>
              </li>
            ))}
          </ol>
          <button
            type="button"
            className="btn"
            onClick={() => set('provenance', [...state.provenance, { key: newRowKey(), region: null, period: { text: '' }, note: '' }])}
          >
            <Plus aria-hidden="true" /> Add provenance step
          </button>
        </fieldset>

        <div className="record-form__actions">
          <button type="submit" className="btn btn--primary" disabled={saving}>
            {saving ? 'Saving…' : artefactId ? 'Save changes' : 'Create artefact'}
          </button>
          <button type="button" className="btn" onClick={() => navigate(artefactId ? `/artefacts/${artefactId}` : config.cancelHref)}>
            Cancel
          </button>
        </div>
      </form>
    )
  },
})

function SelectField({ label, value, onChange, errors, children }: { label: string; value: string; onChange: (v: string) => void; errors?: string[]; children: ReactNode }) {
  const id = useId()
  return (
    <div className={`field${errors ? ' field--invalid' : ''}`}>
      <label className="field__label" htmlFor={id}>
        {label}
      </label>
      <select id={id} className="select" value={value} onChange={(e) => onChange(e.target.value)}>
        {children}
      </select>
      {errors?.map((e) => (
        <p key={e} className="field__error">
          {e}
        </p>
      ))}
    </div>
  )
}

function DateField({ label, value, onChange, errors, disabled }: { label: string; value: string; onChange: (v: string) => void; errors?: string[]; disabled?: boolean }) {
  const id = useId()
  return (
    <div className={`field${errors ? ' field--invalid' : ''}`}>
      <label className="field__label" htmlFor={id}>
        {label}
      </label>
      <input id={id} type="date" className="input" value={value} disabled={disabled} onChange={(e) => onChange(e.target.value)} />
      {errors?.map((e) => (
        <p key={e} className="field__error">
          {e}
        </p>
      ))}
    </div>
  )
}

function MoneyField({ amount, currency, onAmount, onCurrency, errors, disabled }: { amount: string; currency: string; onAmount: (v: string) => void; onCurrency: (v: string) => void; errors?: string[]; disabled?: boolean }) {
  const id = useId()
  return (
    <div className={`field money-field${errors ? ' field--invalid' : ''}`}>
      <label className="field__label" htmlFor={id}>
        Price <span className="muted">(confidential)</span>
      </label>
      <div className="money-field__inputs">
        <input id={id} className="input" inputMode="decimal" placeholder="0.00" value={amount} disabled={disabled} aria-invalid={!!errors} onChange={(e) => onAmount(e.target.value)} />
        <input
          className="input money-field__currency"
          aria-label="Currency"
          list={`${id}-currencies`}
          maxLength={3}
          value={currency}
          disabled={disabled}
          onChange={(e) => onCurrency(e.target.value.toUpperCase())}
        />
        <datalist id={`${id}-currencies`}>
          {COMMON_CURRENCIES.map((c) => (
            <option key={c} value={c} />
          ))}
        </datalist>
      </div>
      {errors?.map((e) => (
        <p key={e} className="field__error">
          {e}
        </p>
      ))}
    </div>
  )
}
