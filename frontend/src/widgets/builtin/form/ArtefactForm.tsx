import { useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, Plus, Trash2 } from 'lucide-react'
import { useId, useState, type FormEvent, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { api, type AcquisitionMethod, type ArtefactWriteRequest, type HistoricalDateInput, type UnitOfMeasure } from '../../../api/client'
import { useUnits } from '../../../api/units'
import { ApiError, unwrap } from '../../../api/problem'
import { defineWidget } from '../../core/registry'
import { useContentLanguages } from '../../../app/languages'
import { isHistoricalDate, isMeasurement, isMoney, MEASUREMENT_LABELS, type MeasurementName } from '../../format'
import { HistoricalDateField } from './HistoricalDateField'
import type { PickedRef } from './ReferencePicker'
import {
  CollectionGroupPicker,
  DynastyPicker,
  ObjectTypePicker,
  PartyPicker,
  RegionPicker,
  StorageLocationPicker,
  type PickedRegion,
} from './RegionPicker'
import './form.css'

interface ProvenanceRow {
  key: string
  region: PickedRegion | null
  period: HistoricalDateInput
  note: string
}

interface ExhibitionRow {
  key: string
  text: string
}

/** A measurement as edited: the value as typed and the chosen unit ('' = none yet). */
interface MeasurementDraft {
  value: string
  unitId: string
}

const MEASUREMENTS: { name: MeasurementName; label: string }[] = [
  { name: 'height', label: 'Height' },
  { name: 'width', label: 'Width' },
  { name: 'length', label: 'Length' },
  { name: 'diameter', label: 'Diameter' },
]

interface FormState {
  inventoryNumber: string
  title: string
  maker: string
  creationDate: HistoricalDateInput
  medium: string
  dimensions: string
  measurements: Record<MeasurementName, MeasurementDraft>
  /** Description per language code; languages without text are left out when saving. */
  descriptions: Record<string, string>
  method: AcquisitionMethod | ''
  acquisitionDate: string
  source: PickedRef | null
  priceAmount: string
  priceCurrency: string
  lot: string
  acquisitionNote: string
  provenance: ProvenanceRow[]
  objectType: PickedRef | null
  collectionGroup: PickedRef | null
  placeText: string
  placeRegion: PickedRef | null
  dynasty: PickedRef | null
  exhibitions: ExhibitionRow[]
  /** Catalogue entry per language code; languages without text are left out when saving. */
  catalogueEntries: Record<string, string>
  /** None means the collection itself holds the artefact. */
  holder: PickedRef | null
  storageLocation: PickedRef | null
  valuationAmount: string
  valuationCurrency: string
}

const METHODS: { value: AcquisitionMethod; label: string }[] = [
  { value: 'purchase', label: 'Purchase' },
  { value: 'gift', label: 'Gift' },
  { value: 'bequest', label: 'Bequest' },
  { value: 'transfer', label: 'Transfer' },
  { value: 'other', label: 'Other' },
]

const COMMON_CURRENCIES = ['EUR', 'USD', 'GBP', 'CHF', 'JPY', 'SEK', 'DKK', 'NOK', 'CAD', 'AUD', 'ITL', 'DEM', 'FRF']

let rowSeq = 0
const newRowKey = () => `row-${++rowSeq}`

function str(v: unknown): string {
  return typeof v === 'string' ? v : ''
}

function dateInput(v: unknown): HistoricalDateInput {
  return isHistoricalDate(v) ? { text: v.display, earliestYear: null, latestYear: null } : { text: '' }
}

function ref(id: unknown, name: unknown): PickedRef | null {
  return typeof id === 'string' ? { id, name: str(name) } : null
}

/** Parses an amount typed with either decimal separator; null when blank. */
function amountOf(text: string): number | null {
  return text.trim() === '' ? null : Number(text.replace(',', '.'))
}

function measurementDraft(v: unknown): MeasurementDraft {
  if (!isMeasurement(v)) return { value: '', unitId: '' }
  const unit = (v.unit ?? {}) as { id?: unknown }
  return { value: String(v.value), unitId: typeof unit.id === 'string' ? unit.id : '' }
}

function initialState(values: Record<string, { value: unknown }>): FormState {
  const price = values.acquisitionPrice?.value
  const valuation = values.valuation?.value
  const exhibitions = Array.isArray(values.exhibitions?.value) ? (values.exhibitions.value as unknown[]) : []
  const provenance = Array.isArray(values.provenance?.value) ? (values.provenance.value as Record<string, unknown>[]) : []
  return {
    inventoryNumber: str(values.inventoryNumber?.value),
    title: str(values.title?.value),
    maker: str(values.maker?.value),
    creationDate: dateInput(values.creationDate?.value),
    medium: str(values.medium?.value),
    dimensions: str(values.dimensions?.value),
    measurements: {
      height: measurementDraft(values.height?.value),
      width: measurementDraft(values.width?.value),
      length: measurementDraft(values.length?.value),
      diameter: measurementDraft(values.diameter?.value),
    },
    descriptions: Object.fromEntries(Object.entries((values.descriptions?.value ?? {}) as Record<string, unknown>).map(([k, v]) => [k, str(v)])),
    method: (str(values.acquisitionMethod?.value) as AcquisitionMethod) || '',
    acquisitionDate: str(values.acquisitionDate?.value),
    source: ref(values.acquisitionSourceId?.value, values.acquisitionSourceName?.value),
    priceAmount: isMoney(price) ? String(price.amount) : '',
    priceCurrency: isMoney(price) ? price.currency : 'EUR',
    lot: str(values.acquisitionLot?.value),
    acquisitionNote: str(values.acquisitionNote?.value),
    objectType: ref(values.objectTypeId?.value, values.objectTypeName?.value),
    collectionGroup: ref(values.collectionGroupId?.value, values.collectionGroupName?.value),
    placeText: str(values.placeText?.value),
    placeRegion: ref(values.placeRegionId?.value, values.placeRegionName?.value),
    dynasty: ref(values.dynastyId?.value, values.dynastyName?.value),
    exhibitions: exhibitions.map((text) => ({ key: newRowKey(), text: str(text) })),
    catalogueEntries: Object.fromEntries(Object.entries((values.catalogueEntries?.value ?? {}) as Record<string, unknown>).map(([k, v]) => [k, str(v)])),
    holder: ref(values.holderId?.value, values.holderName?.value),
    storageLocation: ref(values.storageLocationId?.value, values.storageLocationName?.value),
    valuationAmount: isMoney(valuation) ? String(valuation.amount) : '',
    valuationCurrency: isMoney(valuation) ? valuation.currency : 'EUR',
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
  const amount = amountOf(s.priceAmount)
  const valuation = amountOf(s.valuationAmount)
  // A measurement left empty is not saved; a value without a unit is sent so the server reports it on the field.
  const measurement = (m: MeasurementDraft) => (m.value.trim() === '' ? null : { value: amountOf(m.value), unitId: m.unitId || null })
  return {
    inventoryNumber: s.inventoryNumber,
    title: s.title,
    maker: opt(s.maker),
    creationDate: dateOrNull(s.creationDate),
    medium: opt(s.medium),
    dimensions: opt(s.dimensions),
    height: measurement(s.measurements.height),
    width: measurement(s.measurements.width),
    length: measurement(s.measurements.length),
    diameter: measurement(s.measurements.diameter),
    descriptions: Object.fromEntries(Object.entries(s.descriptions).filter(([, text]) => text.trim() !== '')),
    acquisition: s.method
      ? {
          method: s.method,
          date: opt(s.acquisitionDate),
          sourceId: s.source?.id ?? null,
          price: amount === null ? null : { amount, currency: s.priceCurrency.trim().toUpperCase() },
          lot: opt(s.lot),
          note: opt(s.acquisitionNote),
        }
      : null,
    objectTypeId: s.objectType?.id ?? null,
    collectionGroupId: s.collectionGroup?.id ?? null,
    placeOfProduction: s.placeText.trim() || s.placeRegion ? { text: opt(s.placeText), regionId: s.placeRegion?.id ?? null } : null,
    dynastyId: s.dynasty?.id ?? null,
    exhibitions: s.exhibitions.map((e) => e.text.trim()).filter((t) => t !== ''),
    catalogueEntries: Object.fromEntries(Object.entries(s.catalogueEntries).filter(([, text]) => text.trim() !== '')),
    holderId: s.holder?.id ?? null,
    storageLocationId: s.storageLocation?.id ?? null,
    valuation: valuation === null ? null : { amount: valuation, currency: s.valuationCurrency.trim().toUpperCase() },
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
    height: { type: 'measurement' },
    width: { type: 'measurement' },
    length: { type: 'measurement' },
    diameter: { type: 'measurement' },
    descriptions: { type: 'map' },
    acquisitionMethod: { type: 'text' },
    acquisitionDate: { type: 'date' },
    acquisitionSourceId: { type: 'reference' },
    acquisitionSourceName: { type: 'text' },
    acquisitionPrice: { type: 'money' },
    acquisitionLot: { type: 'text' },
    acquisitionNote: { type: 'text' },
    provenance: { type: 'list' },
    objectTypeId: { type: 'reference' },
    objectTypeName: { type: 'text' },
    collectionGroupId: { type: 'reference' },
    collectionGroupName: { type: 'text' },
    placeText: { type: 'text' },
    placeRegionId: { type: 'reference' },
    placeRegionName: { type: 'text' },
    dynastyId: { type: 'reference' },
    dynastyName: { type: 'text' },
    exhibitions: { type: 'any' },
    catalogueEntries: { type: 'map' },
    holderId: { type: 'reference' },
    holderName: { type: 'text' },
    storageLocationId: { type: 'reference' },
    storageLocationName: { type: 'text' },
    valuation: { type: 'money' },
  },
  config: { cancelHref: '/artefacts' },
  print: { hideInPrint: true },
  component: function ArtefactForm({ values, config }) {
    const artefactId = typeof values.id?.value === 'string' ? values.id.value : null
    const navigate = useNavigate()
    const queryClient = useQueryClient()
    const [state, setState] = useState<FormState>(() => initialState(values))
    const { data: languages = [] } = useContentLanguages()
    const { data: units = [] } = useUnits()
    // A new measurement takes the unit used last in the form, else centimetres.
    const [lastUnit, setLastUnit] = useState<string>(() => Object.values(state.measurements).find((m) => m.unitId)?.unitId ?? '')
    const defaultUnit = lastUnit || units.find((u) => u.abbreviation === 'cm')?.id || units[0]?.id || ''
    const setMeasurement = (name: MeasurementName, patch: Partial<MeasurementDraft>) => {
      if (patch.unitId) setLastUnit(patch.unitId)
      setState((s) => {
        const current = s.measurements[name]
        const next = { ...current, ...patch }
        if (patch.value !== undefined && patch.value.trim() !== '' && !next.unitId) next.unitId = defaultUnit
        return { ...s, measurements: { ...s.measurements, [name]: next } }
      })
    }
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
    const setExhibition = (index: number, text: string) =>
      setState((s) => ({ ...s, exhibitions: s.exhibitions.map((r, i) => (i === index ? { ...r, text } : r)) }))
    const moveExhibition = (index: number, by: number) =>
      setState((s) => {
        const rows = [...s.exhibitions]
        const [row] = rows.splice(index, 1)
        rows.splice(index + by, 0, row!)
        return { ...s, exhibitions: rows }
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
          <div className="measurements" role="group" aria-label="Measurements">
            {MEASUREMENTS.map(({ name, label }) => (
              <MeasurementField
                key={name}
                label={label}
                short={MEASUREMENT_LABELS[name]}
                value={state.measurements[name]}
                units={units}
                onChange={(patch) => setMeasurement(name, patch)}
                errors={errorsFor(errors, name)}
              />
            ))}
          </div>
          {languages.map((language) => (
            <TextField
              key={language.code}
              label={`Description (${language.name})`}
              multiline
              value={state.descriptions[language.code] ?? ''}
              onChange={(v) => set('descriptions', { ...state.descriptions, [language.code]: v })}
              errors={errorsFor(errors, `descriptions.${language.code}`)}
            />
          ))}
        </fieldset>

        <fieldset className="form-section">
          <legend>Classification and origin</legend>
          <div className="form-grid">
            <ObjectTypePicker label="Object type" value={state.objectType} onChange={(v) => set('objectType', v)} errors={errorsFor(errors, 'objectTypeId')} />
            <CollectionGroupPicker label="Collection group" value={state.collectionGroup} onChange={(v) => set('collectionGroup', v)} errors={errorsFor(errors, 'collectionGroupId')} />
            <TextField label="Place of production" value={state.placeText} onChange={(v) => set('placeText', v)} errors={errorsFor(errors, 'placeOfProduction.text')} hint="As written, uncertainty included" />
            <RegionPicker label="Region (optional)" value={state.placeRegion} onChange={(v) => set('placeRegion', v)} errors={errorsFor(errors, 'placeOfProduction.regionId')} />
            <DynastyPicker label="Dynasty or period" value={state.dynasty} onChange={(v) => set('dynasty', v)} errors={errorsFor(errors, 'dynastyId')} />
          </div>
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
            <PartyPicker label="Source" value={state.source} onChange={(v) => set('source', v)} errors={errorsFor(errors, 'acquisition.sourceId')} />
            <MoneyField
              amount={state.priceAmount}
              currency={state.priceCurrency}
              disabled={!state.method}
              onAmount={(v) => set('priceAmount', v)}
              onCurrency={(v) => set('priceCurrency', v)}
              errors={errorsFor(errors, 'acquisition.price')}
            />
            <TextField label="Lot" value={state.lot} onChange={(v) => set('lot', v)} errors={errorsFor(errors, 'acquisition.lot')} />
          </div>
          <TextField label="Acquisition note" multiline value={state.acquisitionNote} onChange={(v) => set('acquisitionNote', v)} errors={errorsFor(errors, 'acquisition.note')} />
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

        <fieldset className="form-section">
          <legend>Exhibitions</legend>
          <ol className="exhibition-editor">
            {state.exhibitions.map((row, i) => (
              <li key={row.key} className="exhibition-editor__row">
                <span className="provenance-editor__num" aria-hidden="true">
                  {i + 1}
                </span>
                <TextField label={`Exhibition ${i + 1}`} value={row.text} onChange={(text) => setExhibition(i, text)} errors={errorsFor(errors, `exhibitions[${i}]`)} />
                <div className="provenance-editor__tools">
                  <button type="button" className="btn btn--icon btn--sm" aria-label={`Move exhibition ${i + 1} up`} disabled={i === 0} onClick={() => moveExhibition(i, -1)}>
                    <ArrowUp aria-hidden="true" />
                  </button>
                  <button type="button" className="btn btn--icon btn--sm" aria-label={`Move exhibition ${i + 1} down`} disabled={i === state.exhibitions.length - 1} onClick={() => moveExhibition(i, 1)}>
                    <ArrowDown aria-hidden="true" />
                  </button>
                  <button type="button" className="btn btn--icon btn--sm btn--danger" aria-label={`Remove exhibition ${i + 1}`} onClick={() => set('exhibitions', state.exhibitions.filter((_, j) => j !== i))}>
                    <Trash2 aria-hidden="true" />
                  </button>
                </div>
              </li>
            ))}
          </ol>
          <button type="button" className="btn" onClick={() => set('exhibitions', [...state.exhibitions, { key: newRowKey(), text: '' }])}>
            <Plus aria-hidden="true" /> Add exhibition
          </button>
        </fieldset>

        <fieldset className="form-section">
          <legend>Catalogue entry</legend>
          {languages.map((language) => (
            <TextField
              key={language.code}
              label={`Catalogue entry (${language.name})`}
              multiline
              value={state.catalogueEntries[language.code] ?? ''}
              onChange={(v) => set('catalogueEntries', { ...state.catalogueEntries, [language.code]: v })}
              errors={errorsFor(errors, `catalogueEntries.${language.code}`)}
            />
          ))}
        </fieldset>

        <fieldset className="form-section">
          <legend>Holding</legend>
          <div className="form-grid">
            <PartyPicker
              label="Holder"
              value={state.holder}
              onChange={(v) => set('holder', v)}
              errors={errorsFor(errors, 'holderId')}
              hint="Leave empty when the collection itself holds the artefact."
            />
            <StorageLocationPicker label="Storage location" value={state.storageLocation} onChange={(v) => set('storageLocation', v)} errors={errorsFor(errors, 'storageLocationId')} />
            <MoneyField
              label="Valuation"
              amount={state.valuationAmount}
              currency={state.valuationCurrency}
              onAmount={(v) => set('valuationAmount', v)}
              onCurrency={(v) => set('valuationCurrency', v)}
              errors={errorsFor(errors, 'valuation')}
            />
          </div>
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

/** A measurement: the number, and the unit it is in. Left empty, the measurement is not recorded. */
function MeasurementField({
  label,
  short,
  value,
  units,
  onChange,
  errors,
}: {
  label: string
  short: string
  value: MeasurementDraft
  units: UnitOfMeasure[]
  onChange: (patch: Partial<MeasurementDraft>) => void
  errors?: string[]
}) {
  const id = useId()
  return (
    <div className={`field measurement-field${errors ? ' field--invalid' : ''}`}>
      <label className="field__label" htmlFor={id}>
        {label} <span className="muted">({short})</span>
      </label>
      <div className="measurement-field__inputs">
        <input id={id} className="input" inputMode="decimal" value={value.value} aria-invalid={!!errors} onChange={(e) => onChange({ value: e.target.value })} />
        <select className="select measurement-field__unit" aria-label={`${label} unit`} value={value.unitId} onChange={(e) => onChange({ unitId: e.target.value })}>
          <option value="">Unit</option>
          {units.map((u) => (
            <option key={u.id} value={u.id}>
              {u.abbreviation}
            </option>
          ))}
        </select>
      </div>
      {errors?.map((e) => (
        <p key={e} className="field__error">
          {e}
        </p>
      ))}
    </div>
  )
}

function MoneyField({ label = 'Price', amount, currency, onAmount, onCurrency, errors, disabled }: { label?: string; amount: string; currency: string; onAmount: (v: string) => void; onCurrency: (v: string) => void; errors?: string[]; disabled?: boolean }) {
  const id = useId()
  return (
    <div className={`field money-field${errors ? ' field--invalid' : ''}`}>
      <label className="field__label" htmlFor={id}>
        {label} <span className="muted">(confidential)</span>
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
