import { useId, type KeyboardEvent, type ReactNode } from 'react'
import type { Language } from '../api/client'
import type { NameDraft } from './names'

/** One labelled input per content language, for add forms. */
export function NameInputs({
  languages,
  value,
  onChange,
  noun,
  placeholder,
  invalid,
}: {
  languages: Language[]
  value: NameDraft
  onChange: (draft: NameDraft) => void
  noun: string
  placeholder?: Record<string, string>
  invalid?: boolean
}) {
  const id = useId()
  return (
    <>
      {languages.map((language) => (
        <div key={language.code} className={`field${invalid ? ' field--invalid' : ''}`}>
          <label className="field__label" htmlFor={`${id}-${language.code}`}>
            New {noun} <span className="muted">({language.name})</span>
          </label>
          <input
            id={`${id}-${language.code}`}
            className="input"
            value={value[language.code] ?? ''}
            placeholder={placeholder?.[language.code]}
            aria-invalid={invalid}
            onChange={(e) => onChange({ ...value, [language.code]: e.target.value })}
          />
        </div>
      ))}
    </>
  )
}

/**
 * The name cells of one reference-list row: one cell per language, showing the name or, while editing, an input.
 * Enter saves, Escape cancels.
 */
export function NameCells({
  languages,
  names,
  editing,
  draft,
  onDraft,
  onSave,
  onCancel,
  label,
  note,
}: {
  languages: Language[]
  names: Record<string, string>
  editing: boolean
  draft: NameDraft
  onDraft: (draft: NameDraft) => void
  onSave: () => void
  onCancel: () => void
  /** The entry's name for accessible labels. */
  label: string
  /** Shown under the first name (e.g. the row's error). */
  note?: ReactNode
}) {
  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault()
      onSave()
    } else if (e.key === 'Escape') onCancel()
  }
  return (
    <>
      {languages.map((language, i) => (
        <td key={language.code} className="names__cell" data-label={language.name}>
          {editing ? (
            <input
              className="input"
              aria-label={`${language.name} name for ${label}`}
              value={draft[language.code] ?? ''}
              autoFocus={i === 0}
              onChange={(e) => onDraft({ ...draft, [language.code]: e.target.value })}
              onKeyDown={onKeyDown}
            />
          ) : names[language.code] ? (
            <span className="cell-title">{names[language.code]}</span>
          ) : (
            <span className="muted" aria-label={`No ${language.name} name`}>
              —
            </span>
          )}
          {i === 0 && note}
        </td>
      ))}
    </>
  )
}
