import { STATE_LABELS, STATUS_LABELS, type ImportJobState, type ImportRowStatus } from './importApi'

const STATE_TONE: Record<ImportJobState, string> = {
  reading: 'busy',
  awaitingMapping: 'todo',
  readyForReview: 'todo',
  importing: 'busy',
  finished: 'done',
  failed: 'bad',
  discarded: 'off',
}

/** The state of an import, as a small condensed label whose tone carries meaning (never the selection yellow). */
export function StateChip({ state }: { state: ImportJobState }) {
  return <span className={`chip chip--${STATE_TONE[state]}`}>{STATE_LABELS[state]}</span>
}

const STATUS_TONE: Record<ImportRowStatus, string> = {
  pending: 'off',
  ready: 'todo',
  problem: 'bad',
  imported: 'done',
  skipped: 'off',
}

export function StatusChip({ status }: { status: ImportRowStatus }) {
  return <span className={`chip chip--${STATUS_TONE[status]}`}>{STATUS_LABELS[status]}</span>
}

/** Lines a discarding rule dropped from a row ("Nome File: “vedi foto”"), crossed out: they are imported nowhere. */
export function DiscardedLines({ lines }: { lines: string[] | null | undefined }) {
  if (!lines || lines.length === 0) return null
  return (
    <ul className="imports__discarded" aria-label="Discarded lines">
      {lines.map((line, i) => (
        <li key={i}>
          <del>{line}</del> <span className="muted">not imported</span>
        </li>
      ))}
    </ul>
  )
}
