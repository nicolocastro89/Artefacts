import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight } from 'lucide-react'
import { useId, useMemo, useState } from 'react'
import { api, type Language, type PartyKind } from '../../api/client'
import { unwrap } from '../../api/problem'
import { useContentLanguages } from '../../app/languages'
import { ReferencePicker, type RefSource } from '../../widgets/builtin/form/ReferencePicker'
import { listSources, partySource } from '../../widgets/builtin/form/pickerSources'
import { partyKindLabels } from '../lists'
import { errorMessage, fieldLabel, type ImportJob, type ImportList, type ImportListGroup, type ImportListValue, type ListDecision } from './importApi'

type Choice =
  | { kind: 'undecided' }
  | { kind: 'link'; entryId: string; name: string }
  | { kind: 'create'; names: Record<string, string>; name: string; partyKind: PartyKind }

interface ListMeta {
  title: string
  noun: string
  /** Names per language when creating (dynasties, collection groups); otherwise a single name. */
  translated: boolean
  source: RefSource
  hint: string
}

const LISTS: Record<ImportList, ListMeta> = {
  collectionGroup: {
    title: 'Collection groups',
    noun: 'collection group',
    translated: true,
    source: listSources.collectionGroup,
    hint: 'From the group headings of the sheet (“01. CINA”), or a column mapped to the collection group.',
  },
  dynasty: { title: 'Dynasties', noun: 'dynasty', translated: true, source: listSources.dynasty, hint: 'Dynasties and periods as written in the workbook.' },
  storageLocation: {
    title: 'Storage locations',
    noun: 'storage location',
    translated: false,
    source: listSources.storageLocation,
    hint: 'Location codes found in the workbook.',
  },
  party: {
    title: 'Sources and holders',
    noun: 'party',
    translated: false,
    source: partySource,
    hint: 'Sellers and holders are one list of people and organisations: a name used for both is decided once.',
  },
}

/** A name for a new entry from a workbook text: trailing question marks dropped. */
const cleanName = (text: string) => text.replace(/\?+$/, '').trim()

function choiceOf(value: ImportListValue, language: string, names: Map<string, string>): Choice {
  const d = value.decision
  if (!d) return { kind: 'undecided' }
  if (d.entryId) {
    const suggested = value.suggestions.find((s) => s.entryId === d.entryId)
    return { kind: 'link', entryId: d.entryId, name: suggested?.name ?? names.get(d.entryId) ?? '' }
  }
  return {
    kind: 'create',
    names: { ...(d.names ?? { [language]: cleanName(value.text) }) },
    name: d.name ?? cleanName(value.text),
    partyKind: d.partyKind ?? 'organisation',
  }
}

function decisionOf(list: ImportList, text: string, choice: Choice): ListDecision | null {
  switch (choice.kind) {
    case 'link':
      return choice.entryId ? { list, text, action: 'link', entryId: choice.entryId } : null
    case 'create':
      if (list === 'party') return { list, text, action: 'create', name: choice.name.trim(), partyKind: choice.partyKind }
      if (!LISTS[list].translated) return { list, text, action: 'create', name: choice.name.trim() }
      return {
        list,
        text,
        action: 'create',
        names: Object.fromEntries(Object.entries(choice.names).filter(([, n]) => n.trim() !== '').map(([k, n]) => [k, n.trim()])),
      }
    default:
      return null
  }
}

const key = (list: ImportList, text: string) => `${list}\u0000${text}`

/**
 * Every distinct collection group, dynasty, storage location and party text of the workbook. Each must be linked to
 * an entry (a suggestion, or any entry picked from the list) or created; rows with an undecided text are not imported.
 */
export function ListsStep({ job, onDone }: { job: ImportJob; onDone: () => void }) {
  const queryClient = useQueryClient()
  const { data: languages = [] } = useContentLanguages()
  const sourceLanguage = job.mapping?.language ?? 'it'
  const lists = useQuery({
    queryKey: ['imports', job.id, 'lists'],
    queryFn: async () => unwrap(await api.GET('/api/imports/{id}/lists', { params: { path: { id: job.id } } })),
  })
  // Names of every entry and party, for decisions that link an entry not among the suggestions.
  const names = useEntryNames()
  // Choices made on this page, on top of the decisions in force.
  const [choices, setChoices] = useState<Record<string, Choice>>({})
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const current = (list: ImportList, value: ImportListValue): Choice => choices[key(list, value.text)] ?? choiceOf(value, sourceLanguage, names)

  const save = async () => {
    setSaving(true)
    setError(null)
    try {
      const decisions = (lists.data ?? []).flatMap((g) => g.values.map((v) => decisionOf(g.list, v.text, current(g.list, v)))).filter((d) => d !== null)
      unwrap(await api.PUT('/api/imports/{id}/lists', { params: { path: { id: job.id } }, body: { lists: decisions } }))
      await queryClient.invalidateQueries({ queryKey: ['imports', job.id] })
      onDone()
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      setSaving(false)
    }
  }

  if (lists.isPending)
    return (
      <div className="data-state" aria-busy="true">
        Matching list values…
      </div>
    )
  if (lists.isError)
    return (
      <div className="data-state data-state--error" role="alert">
        Could not load the list values: {lists.error.message}
      </div>
    )

  const undecided = lists.data.reduce((n, g) => n + g.values.filter((v) => current(g.list, v).kind === 'undecided').length, 0)

  return (
    <div className="imports__step-body">
      {lists.data.map((group) => (
        <ListSection
          key={group.list}
          group={group}
          current={(v) => current(group.list, v)}
          languages={languages}
          sourceLanguage={sourceLanguage}
          onChange={(text, c) => setChoices((all) => ({ ...all, [key(group.list, text)]: c }))}
        />
      ))}
      <div className="imports__actions">
        <p className="muted" aria-live="polite">
          {undecided > 0 ? `${undecided} undecided: their rows will not be imported` : 'Every value has a decision.'}
        </p>
        <div className="imports__actions-main">
          {error && (
            <p className="field__error" role="alert">
              {error}
            </p>
          )}
          <button type="button" className="btn btn--primary" disabled={saving} onClick={() => void save()}>
            Save and continue to dates <ArrowRight aria-hidden="true" />
          </button>
        </div>
      </div>
    </div>
  )
}

/** Every entry's and party's name by id, read once for labelling links. */
function useEntryNames(): Map<string, string> {
  const query = useQuery({
    queryKey: ['imports', 'entry-names'],
    queryFn: async () => {
      const [dynasties, groups, locations, parties] = await Promise.all([
        api.GET('/api/lists/{list}', { params: { path: { list: 'dynasties' } } }),
        api.GET('/api/lists/{list}', { params: { path: { list: 'collection-groups' } } }),
        api.GET('/api/lists/{list}', { params: { path: { list: 'storage-locations' } } }),
        api.GET('/api/parties'),
      ])
      return [...unwrap(dynasties), ...unwrap(groups), ...unwrap(locations), ...unwrap(parties)].map((e) => [e.id, e.name] as const)
    },
    staleTime: 30_000,
  })
  return useMemo(() => new Map(query.data ?? []), [query.data])
}

function ListSection({
  group,
  current,
  languages,
  sourceLanguage,
  onChange,
}: {
  group: ImportListGroup
  current: (value: ImportListValue) => Choice
  languages: Language[]
  sourceLanguage: string
  onChange: (text: string, choice: Choice) => void
}) {
  const meta = LISTS[group.list]
  const headingId = useId()
  const filterId = useId()
  const [onlyUndecided, setOnlyUndecided] = useState(false)
  const undecided = group.values.filter((v) => current(v).kind === 'undecided').length
  // Filtered on the decisions in force when the page loaded, so a row being decided stays in view until saved.
  const shown = onlyUndecided ? group.values.filter((v) => !v.decision) : group.values

  return (
    <section className="plate imports__section" aria-labelledby={headingId}>
      <div className="imports__list-head">
        <h2 id={headingId} className="imports__section-title">
          {meta.title}
        </h2>
        {group.values.length > 0 && (
          <span className={`chip chip--${undecided > 0 ? 'bad' : 'done'}`}>{undecided > 0 ? `${undecided} undecided` : 'All decided'}</span>
        )}
      </div>
      <p className="muted imports__section-sub">
        {group.values.length === 0
          ? `No row has a ${meta.noun}.`
          : `${group.values.length} distinct ${group.values.length === 1 ? 'value' : 'values'}. ${meta.hint}`}
      </p>
      {group.values.length > 0 && (undecided > 0 || onlyUndecided) && (
        <label className="imports__check imports__list-filter" htmlFor={filterId}>
          <input id={filterId} type="checkbox" checked={onlyUndecided} onChange={(e) => setOnlyUndecided(e.target.checked)} /> Show only undecided
        </label>
      )}
      {shown.length > 0 && (
        <ul className="imports__places">
          {shown.map((v) => (
            <ValueRow
              key={v.text}
              list={group.list}
              value={v}
              choice={current(v)}
              languages={languages}
              sourceLanguage={sourceLanguage}
              onChange={(c) => onChange(v.text, c)}
            />
          ))}
        </ul>
      )}
    </section>
  )
}

function ValueRow({
  list,
  value,
  choice,
  languages,
  sourceLanguage,
  onChange,
}: {
  list: ImportList
  value: ImportListValue
  choice: Choice
  languages: Language[]
  sourceLanguage: string
  onChange: (c: Choice) => void
}) {
  const meta = LISTS[list]
  const id = useId()
  const nameId = useId()
  const kindId = useId()
  const [picking, setPicking] = useState(false)
  const suggested = new Set(value.suggestions.map((s) => s.entryId))
  const selectValue =
    choice.kind === 'link' ? (suggested.has(choice.entryId) && !picking ? `s:${choice.entryId}` : 'other') : choice.kind === 'undecided' ? (picking ? 'other' : '') : 'create'

  const select = (v: string) => {
    setPicking(v === 'other')
    if (v.startsWith('s:')) {
      const s = value.suggestions.find((x) => x.entryId === v.slice(2))!
      onChange({ kind: 'link', entryId: s.entryId, name: s.name })
    } else if (v === 'other') onChange(choice.kind === 'link' ? choice : { kind: 'undecided' })
    else if (v === 'create')
      onChange({ kind: 'create', names: { [sourceLanguage]: cleanName(value.text) }, name: cleanName(value.text), partyKind: 'organisation' })
    else onChange({ kind: 'undecided' })
  }

  const uses = list === 'party' ? value.fields.map((f) => (f === 'acquisitionSource' ? 'source' : f === 'holder' ? 'holder' : fieldLabel(f))).join(' and ') : null

  return (
    <li className="imports__place">
      <div className="imports__place-text">
        <strong lang={sourceLanguage}>{value.text}</strong>
        <span className="muted">
          {value.rows} {value.rows === 1 ? 'row' : 'rows'}
          {uses && <> · as {uses}</>}
        </span>
      </div>
      <div className="imports__place-decision">
        <div className="field">
          <label className="field__label" htmlFor={id}>
            {meta.noun.charAt(0).toUpperCase() + meta.noun.slice(1)} for “{value.text}”
          </label>
          <select id={id} className="select" value={selectValue} onChange={(e) => select(e.target.value)}>
            <option value="">Undecided: rows are not imported</option>
            {value.suggestions.map((s) => (
              <option key={s.entryId} value={`s:${s.entryId}`}>
                {s.name}
                {s.exact ? ' — same name' : ` — ${Math.round(s.score * 100)}% similar`}
                {s.alias ? ' (linked before)' : ''}
              </option>
            ))}
            <option value="other">Pick from the list…</option>
            <option value="create">Create a new {meta.noun}…</option>
          </select>
          {choice.kind === 'undecided' && !picking && value.suggestions.length > 0 && (
            <p className="field__hint">Similar: {value.suggestions.map((s) => `${s.name} (${Math.round(s.score * 100)}%)`).join(', ')}</p>
          )}
        </div>
        {selectValue === 'other' && (
          <ReferencePicker
            label={`Any ${meta.noun}`}
            value={choice.kind === 'link' ? { id: choice.entryId, name: choice.name } : null}
            onChange={(picked) => onChange(picked ? { kind: 'link', entryId: picked.id, name: picked.name } : { kind: 'undecided' })}
            source={meta.source}
            allowAdd={false}
            placeholder={`Search ${meta.title.toLowerCase()}`}
          />
        )}
        {choice.kind === 'create' && (
          <div className="imports__create">
            {meta.translated ? (
              languages.map((l) => (
                <NameInput
                  key={l.code}
                  label={`New ${meta.noun} (${l.name})`}
                  lang={l.code}
                  value={choice.names[l.code] ?? ''}
                  onChange={(v) => onChange({ ...choice, names: { ...choice.names, [l.code]: v } })}
                />
              ))
            ) : (
              <NameInput id={nameId} label={`New ${meta.noun}`} value={choice.name} onChange={(v) => onChange({ ...choice, name: v })} />
            )}
            {list === 'party' && (
              <div className="field">
                <label className="field__label" htmlFor={kindId}>
                  Kind
                </label>
                <select id={kindId} className="select" value={choice.partyKind} onChange={(e) => onChange({ ...choice, partyKind: e.target.value as PartyKind })}>
                  <option value="organisation">{partyKindLabels.organisation}</option>
                  <option value="person">{partyKindLabels.person}</option>
                </select>
              </div>
            )}
          </div>
        )}
      </div>
    </li>
  )
}

function NameInput({ id, label, lang, value, onChange }: { id?: string; label: string; lang?: string; value: string; onChange: (v: string) => void }) {
  const generated = useId()
  const inputId = id ?? generated
  return (
    <div className="field">
      <label className="field__label" htmlFor={inputId}>
        {label}
      </label>
      <input id={inputId} className="input" lang={lang} value={value} onChange={(e) => onChange(e.target.value)} />
    </div>
  )
}
