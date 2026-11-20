import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useEffect, useId, useState, type KeyboardEvent } from 'react'
import { api, type Region } from '../../../api/client'
import { unwrap } from '../../../api/problem'

export interface PickedRegion {
  id: string
  name: string
}

/**
 * Combobox for choosing a region from the reference list. Typing a name that does not exist offers
 * "Add …", which adds it to the list (or returns the existing match) and selects it.
 * Reads reference options only, like a select; it never reads the record being edited.
 */
export function RegionPicker({
  label,
  value,
  onChange,
  errors,
}: {
  label: string
  value: PickedRegion | null
  onChange: (region: PickedRegion | null) => void
  errors?: string[]
}) {
  const id = useId()
  const queryClient = useQueryClient()
  const [text, setText] = useState(value?.name ?? '')
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const [adding, setAdding] = useState(false)
  const [addError, setAddError] = useState<string | null>(null)

  useEffect(() => setText(value?.name ?? ''), [value?.name])

  const search = text.trim()
  const { data: options = [] } = useQuery({
    queryKey: ['regions', 'search', search],
    queryFn: async () => unwrap(await api.GET('/api/regions', { params: { query: search ? { search } : {} } })),
    enabled: open,
    staleTime: 15_000,
  })

  const exact = options.some((r) => r.name.toLowerCase() === search.toLowerCase())
  const items: ({ kind: 'region'; region: Region } | { kind: 'add' })[] = [
    ...options.slice(0, 8).map((region) => ({ kind: 'region' as const, region })),
    ...(search && !exact ? [{ kind: 'add' as const }] : []),
  ]

  const choose = (region: PickedRegion) => {
    onChange(region)
    setText(region.name)
    setOpen(false)
  }

  const add = async () => {
    setAdding(true)
    setAddError(null)
    try {
      const region = unwrap(await api.POST('/api/regions', { body: { name: search } }))
      await queryClient.invalidateQueries({ queryKey: ['regions'] })
      await queryClient.invalidateQueries({ queryKey: ['count', 'regions'] })
      choose({ id: region.id, name: region.name })
    } catch (e) {
      setAddError(e instanceof Error ? e.message : 'Could not add the region.')
    } finally {
      setAdding(false)
    }
  }

  const pick = (index: number) => {
    const item = items[index]
    if (!item) return
    if (item.kind === 'add') void add()
    else choose({ id: item.region.id, name: item.region.name })
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
          placeholder="Choose or add a region"
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
                key={item.kind === 'add' ? 'add' : item.region.id}
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
                    <Plus aria-hidden="true" /> {adding ? 'Adding…' : <>Add “{search}”</>}
                  </>
                ) : (
                  <>
                    {item.region.name}
                    {item.region.parentName && <span className="muted"> · {item.region.parentName}</span>}
                  </>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>
      {allErrors.map((e) => (
        <p key={e} className="field__error">
          {e}
        </p>
      ))}
    </div>
  )
}
