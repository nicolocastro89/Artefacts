import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useEffect, useId, useState, type KeyboardEvent, type ReactNode } from 'react'
import { useReaderLanguage } from '../../../app/languages'

export interface PickedRef {
  id: string
  name: string
}

/** One option shown by the picker: an id, a name, and an optional secondary text (e.g. a parent region). */
export interface RefOption extends PickedRef {
  secondary?: string | null
}

/** Where a picker gets its options from and how it adds a new one. */
export interface RefSource {
  /** Query key root for caching and invalidation (e.g. 'regions'). */
  key: string
  search: (text: string) => Promise<RefOption[]>
  /** Adds an entry named `name` in `language` (the reader's language), of `kind` when the source has kinds. */
  add: (name: string, language: string, kind?: string) => Promise<PickedRef>
  /** "region", "object type": used in the placeholder and error messages. */
  noun: string
  /** When set, adding asks which kind the new entry is: one "Add" option per kind, the first being the default. */
  addKinds?: { value: string; label: string }[]
}

/**
 * Combobox for choosing an entry from a reference list (regions, object types, list entries, parties). Typing a name
 * that does not exist offers "Add …" (one per kind, for sources with kinds), which adds it to the list (or returns the
 * existing match) and selects it.
 * Reads reference options only, like a select; it never reads the record being edited.
 */
export function ReferencePicker({
  label,
  value,
  onChange,
  errors,
  source,
  hint,
  allowAdd = true,
  placeholder,
}: {
  label: string
  value: PickedRef | null
  onChange: (picked: PickedRef | null) => void
  errors?: string[]
  source: RefSource
  hint?: ReactNode
  /** When false the picker only chooses existing entries (a searchable select). */
  allowAdd?: boolean
  placeholder?: string
}) {
  const id = useId()
  const queryClient = useQueryClient()
  const readerLanguage = useReaderLanguage()
  const [text, setText] = useState(value?.name ?? '')
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const [adding, setAdding] = useState(false)
  const [addError, setAddError] = useState<string | null>(null)

  useEffect(() => setText(value?.name ?? ''), [value?.name])

  const search = text.trim()
  const { data: options = [] } = useQuery({
    queryKey: [source.key, 'search', search],
    queryFn: () => source.search(search),
    enabled: open,
    staleTime: 15_000,
  })

  const exact = options.some((r) => r.name.toLowerCase() === search.toLowerCase())
  const addKinds = source.addKinds ?? [{ value: '', label: '' }]
  const items: ({ kind: 'option'; option: RefOption } | { kind: 'add'; addKind: string; addLabel: string })[] = [
    ...options.slice(0, 8).map((option) => ({ kind: 'option' as const, option })),
    ...(allowAdd && search && !exact ? addKinds.map((k) => ({ kind: 'add' as const, addKind: k.value, addLabel: k.label })) : []),
  ]

  const choose = (picked: PickedRef) => {
    onChange(picked)
    setText(picked.name)
    setOpen(false)
  }

  const add = async (kind: string) => {
    setAdding(true)
    setAddError(null)
    try {
      const added = await source.add(search, readerLanguage, kind || undefined)
      await queryClient.invalidateQueries({ queryKey: [source.key] })
      await queryClient.invalidateQueries({ queryKey: ['count', source.key] })
      choose({ id: added.id, name: added.name })
    } catch (e) {
      setAddError(e instanceof Error ? e.message : `Could not add the ${source.noun}.`)
    } finally {
      setAdding(false)
    }
  }

  const pick = (index: number) => {
    const item = items[index]
    if (!item) return
    if (item.kind === 'add') void add(item.addKind)
    else choose({ id: item.option.id, name: item.option.name })
  }

  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setOpen(true)
      setActive((a) => Math.min(items.length - 1, a + 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setActive((a) => Math.max(0, a - 1))
    } else if (e.key === 'Enter' && open) {
      e.preventDefault()
      pick(active)
    } else if (e.key === 'Escape') {
      setOpen(false)
      setText(value?.name ?? '')
    }
  }

  const listId = `${id}-list`
  const allErrors = [...(errors ?? []), ...(addError ? [addError] : [])]
  return (
    <div className={`field region-picker${allErrors.length ? ' field--invalid' : ''}`}>
      <label className="field__label" htmlFor={id}>
        {label}
      </label>
      <div className="region-picker__box">
        <input
          id={id}
          className="input"
          role="combobox"
          aria-expanded={open}
          aria-controls={listId}
          aria-autocomplete="list"
          aria-activedescendant={open && items[active] ? `${listId}-${active}` : undefined}
          aria-invalid={allErrors.length > 0}
          value={text}
          placeholder={placeholder ?? `Choose or add ${/^[aeiou]/i.test(source.noun) ? 'an' : 'a'} ${source.noun}`}
          onChange={(e) => {
            setText(e.target.value)
            setOpen(true)
            setActive(0)
            if (value && e.target.value !== value.name) onChange(null)
          }}
          onFocus={() => setOpen(true)}
          onBlur={() => setTimeout(() => setOpen(false), 120)}
          onKeyDown={onKeyDown}
        />
        {open && items.length > 0 && (
          <ul id={listId} role="listbox" className="region-picker__list">
            {items.map((item, i) => (
              <li
                key={item.kind === 'add' ? `add-${item.addKind}` : item.option.id}
                id={`${listId}-${i}`}
                role="option"
                aria-selected={i === active}
                className="region-picker__option"
                onMouseDown={(e) => {
                  e.preventDefault()
                  pick(i)
                }}
                onMouseEnter={() => setActive(i)}
              >
                {item.kind === 'add' ? (
                  <>
                    <Plus aria-hidden="true" />{' '}
                    {adding ? (
                      'Adding…'
                    ) : (
                      <>
                        Add “{search}”{item.addLabel && <span className="muted"> as {item.addLabel}</span>}
                      </>
                    )}
                  </>
                ) : (
                  <>
                    {item.option.name}
                    {item.option.secondary && <span className="muted"> · {item.option.secondary}</span>}
                  </>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>
      {hint && <p className="field__hint">{hint}</p>}
      {allErrors.map((e) => (
        <p key={e} className="field__error">
          {e}
        </p>
      ))}
    </div>
  )
}
