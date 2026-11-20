import { useEffect, useId, useState } from 'react'
import type { HistoricalDate, HistoricalDateInput } from '../../../api/client'
import { ApiError } from '../../../api/problem'
import { formatYearRange } from '../../format'

const PRECISION: Record<string, string> = { year: 'year', decade: 'decade', century: 'century', approximate: 'approximate' }

async function parse(input: HistoricalDateInput, signal: AbortSignal): Promise<HistoricalDate> {
  const response = await fetch('/api/historical-dates/parse', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  const body = await response.json().catch(() => ({}))
  if (!response.ok) throw new ApiError(response.status, body)
  return body as HistoricalDate
}

type Interpretation = { state: 'idle' } | { state: 'ok'; date: HistoricalDate } | { state: 'error'; message: string }

/**
 * Free-text historical date with live feedback on how it is interpreted, using the server's own rules.
 * Text that cannot be interpreted can be given an explicit earliest/latest year range.
 */
export function HistoricalDateField({
  label,
  value,
  onChange,
  errors,
  required,
}: {
  label: string
  value: HistoricalDateInput
  onChange: (v: HistoricalDateInput) => void
  errors?: string[]
  required?: boolean
}) {
  const id = useId()
  const [interpretation, setInterpretation] = useState<Interpretation>({ state: 'idle' })
  const [showRange, setShowRange] = useState(value.earliestYear != null || value.latestYear != null)
  const text = value.text ?? ''

  useEffect(() => {
    if (!text.trim()) {
      setInterpretation({ state: 'idle' })
      return
    }
    const controller = new AbortController()
    const timer = setTimeout(() => {
      parse({ text, earliestYear: value.earliestYear ?? null, latestYear: value.latestYear ?? null }, controller.signal)
        .then((date) => setInterpretation({ state: 'ok', date }))
        .catch((e: unknown) => {
          if (controller.signal.aborted) return
          const message = e instanceof ApiError ? (Object.values(e.fieldErrors)[0]?.[0] ?? e.message) : 'Could not check this date.'
          setInterpretation({ state: 'error', message })
        })
    }, 300)
    return () => {
      clearTimeout(timer)
      controller.abort()
    }
  }, [text, value.earliestYear, value.latestYear])

  const year = (v: string) => (v.trim() === '' ? null : Number.parseInt(v, 10))
  const hasErrors = (errors?.length ?? 0) > 0

  return (
    <div className={`field date-field${hasErrors ? ' field--invalid' : ''}`}>
      <label className="field__label" htmlFor={id}>
        {label}
        {required && <span aria-hidden="true"> *</span>}
      </label>
      <input
        id={id}
        className="input"
        value={text}
        placeholder="e.g. c. 1650, 1680s, late 17th century"
        aria-describedby={`${id}-feedback`}
        aria-invalid={hasErrors}
        onChange={(e) => onChange({ ...value, text: e.target.value })}
      />
      <p id={`${id}-feedback`} className="date-field__feedback" aria-live="polite">
        {interpretation.state === 'ok' && (
          <>
            Interpreted as <strong>{formatYearRange(interpretation.date.earliestYear, interpretation.date.latestYear)}</strong>{' '}
            <span className="muted">({PRECISION[interpretation.date.precision] ?? interpretation.date.precision})</span>
          </>
        )}
        {interpretation.state === 'error' && <span className="date-field__problem">{interpretation.message}</span>}
      </p>
      {errors?.map((e) => (
        <p key={e} className="field__error">
          {e}
        </p>
      ))}
      {showRange ? (
        <div className="date-field__range">
          <label className="field">
            <span className="field__label">Earliest year</span>
            <input className="input" inputMode="numeric" value={value.earliestYear ?? ''} onChange={(e) => onChange({ ...value, earliestYear: year(e.target.value) })} />
          </label>
          <label className="field">
            <span className="field__label">Latest year</span>
            <input className="input" inputMode="numeric" value={value.latestYear ?? ''} onChange={(e) => onChange({ ...value, latestYear: year(e.target.value) })} />
          </label>
        </div>
      ) : (
        <button type="button" className="btn btn--quiet btn--sm date-field__toggle" onClick={() => setShowRange(true)}>
          Set the year range yourself
        </button>
      )}
    </div>
  )
}
