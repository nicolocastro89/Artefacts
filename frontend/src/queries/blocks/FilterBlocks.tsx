import { Plus, Trash2 } from 'lucide-react'
import type { BuilderFilterField } from '../rules'
import type { Condition, ConditionValue, Group, Scalar } from '../model'
import { defaultScalar, parseScalar } from '../values'
import { ScalarInput, VariableName } from './ValueInput'

/** Operators in plain words; the others are shown only when a condition already uses one. */
const OPERATORS: Record<string, string> = {
  eq: 'equals',
  neq: 'does not equal',
  contains: 'contains',
  ncontains: 'does not contain',
  startsWith: 'starts with',
  endsWith: 'ends with',
  in: 'is one of',
  nin: 'is none of',
  gt: 'is greater than',
  gte: 'is at least',
  lt: 'is less than',
  lte: 'is at most',
}

const OTHER_OPERATORS: Record<string, string> = {
  ngt: 'is not greater than',
  ngte: 'is not at least',
  nlt: 'is not less than',
  nlte: 'is not at most',
  nstartsWith: 'does not start with',
  nendsWith: 'does not end with',
}

interface Props {
  group: Group | null
  fields: BuilderFilterField[]
  variables: string[]
  readOnly?: boolean
  onChange: (group: Group | null) => void
}

/** The filter of a list: conditions, grouped with and/or. */
export function FilterBlocks({ group, fields, variables, readOnly, onChange }: Props) {
  const root: Group = group ?? { type: 'group', op: 'and', items: [] }
  return (
    <div className="blocks__filter">
      <FilterGroup group={root} fields={fields} variables={variables} readOnly={readOnly} depth={0} onChange={(next) => onChange(next)} />
    </div>
  )
}

interface GroupProps {
  group: Group
  fields: BuilderFilterField[]
  variables: string[]
  readOnly?: boolean
  depth: number
  onChange: (group: Group) => void
  onRemove?: () => void
}

function FilterGroup({ group, fields, variables, readOnly, depth, onChange, onRemove }: GroupProps) {
  const setItem = (index: number, item: Group | Condition | null) =>
    onChange({ ...group, items: item ? group.items.map((x, i) => (i === index ? item : x)) : group.items.filter((_, i) => i !== index) })
  const addCondition = () => {
    const field = fields[0]
    if (field) onChange({ ...group, items: [...group.items, newCondition(field)] })
  }

  return (
    <div className="blocks__group" data-depth={depth}>
      <div className="blocks__group-head">
        <label>
          <span className="visually-hidden">Combine the conditions of this group with</span>
          <select className="input" aria-label="Match" value={group.op} disabled={readOnly} onChange={(e) => onChange({ ...group, op: e.target.value as 'and' | 'or' })}>
            <option value="and">All of these</option>
            <option value="or">Any of these</option>
          </select>
        </label>
        {!readOnly && (
          <>
            <button type="button" className="btn btn--sm" onClick={addCondition}>
              <Plus aria-hidden="true" /> Condition
            </button>
            <button type="button" className="btn btn--sm" onClick={() => onChange({ ...group, items: [...group.items, { type: 'group', op: 'or', items: [] }] })}>
              <Plus aria-hidden="true" /> Group
            </button>
            {onRemove && (
              <button type="button" className="btn btn--sm btn--danger" aria-label="Remove this group" onClick={onRemove}>
                <Trash2 aria-hidden="true" />
              </button>
            )}
          </>
        )}
      </div>
      {group.items.length === 0 && <p className="muted">No conditions: every artefact matches.</p>}
      <ul className="blocks__items">
        {group.items.map((item, index) => (
          <li key={index}>
            {item.type === 'group' ? (
              <FilterGroup
                group={item}
                fields={fields}
                variables={variables}
                readOnly={readOnly}
                depth={depth + 1}
                onChange={(next) => setItem(index, next)}
                onRemove={() => setItem(index, null)}
              />
            ) : (
              <ConditionRow condition={item} fields={fields} variables={variables} readOnly={readOnly} onChange={(next) => setItem(index, next)} />
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}

function newCondition(field: BuilderFilterField): Condition {
  return { type: 'condition', field: field.name, operator: 'eq', value: { kind: 'literal', value: defaultScalar(field.graphQlType) } }
}

function ConditionRow({ condition, fields, variables, readOnly, onChange }: {
  condition: Condition
  fields: BuilderFilterField[]
  variables: string[]
  readOnly?: boolean
  onChange: (condition: Condition | null) => void
}) {
  const field = fields.find((f) => f.name === condition.field)
  const operators = field?.operators ?? []
  const operator = operators.find((o) => o.name === condition.operator)
  const offered = operators.filter((o) => o.name in OPERATORS || o.name === condition.operator)
  const label = (name: string) => OPERATORS[name] ?? OTHER_OPERATORS[name] ?? name

  const changeField = (name: string) => {
    const next = fields.find((f) => f.name === name)
    if (next) onChange(newCondition(next))
  }
  const changeOperator = (name: string) => {
    const list = operators.find((o) => o.name === name)?.list ?? false
    const type = field?.graphQlType ?? 'String'
    const value: ConditionValue =
      condition.value.kind === 'variable'
        ? condition.value
        : list
          ? { kind: 'list', items: condition.value.kind === 'list' ? condition.value.items : [] }
          : { kind: 'literal', value: condition.value.kind === 'literal' ? condition.value.value : defaultScalar(type) }
    onChange({ ...condition, operator: name, value })
  }

  return (
    <div className="blocks__condition">
      <select className="input" aria-label="Field" value={condition.field} disabled={readOnly} onChange={(e) => changeField(e.target.value)}>
        {fields.map((f) => (
          <option key={f.name} value={f.name}>
            {f.label}
          </option>
        ))}
      </select>
      <select className="input" aria-label="Operator" value={condition.operator} disabled={readOnly} onChange={(e) => changeOperator(e.target.value)}>
        {offered.map((o) => (
          <option key={o.name} value={o.name}>
            {label(o.name)}
          </option>
        ))}
      </select>
      <ConditionValueInput
        key={`${condition.field}:${condition.operator}`}
        value={condition.value}
        type={field?.graphQlType ?? 'String'}
        list={operator?.list ?? false}
        variables={variables}
        readOnly={readOnly}
        onChange={(value) => onChange({ ...condition, value })}
      />
      {!readOnly && (
        <button type="button" className="btn btn--sm btn--danger" aria-label="Remove this condition" onClick={() => onChange(null)}>
          <Trash2 aria-hidden="true" />
        </button>
      )}
    </div>
  )
}

function ConditionValueInput({ value, type, list, variables, readOnly, onChange }: {
  value: ConditionValue
  type: string
  list: boolean
  variables: string[]
  readOnly?: boolean
  onChange: (value: ConditionValue) => void
}) {
  const mode = value.kind === 'variable' ? 'variable' : 'value'
  const setMode = (next: string) => {
    if (next === 'variable') onChange({ kind: 'variable', name: variables[0] ?? 'value' })
    else onChange(list ? { kind: 'list', items: [] } : { kind: 'literal', value: defaultScalar(type) })
  }

  return (
    <span className="blocks__argument">
      <select className="input" aria-label="Value from" value={mode} disabled={readOnly} onChange={(e) => setMode(e.target.value)}>
        <option value="value">{list ? 'Values' : 'Value'}</option>
        <option value="variable">Variable</option>
      </select>
      {value.kind === 'variable' && (
        <VariableName key={value.name} label="Variable" name={value.name} variables={variables} readOnly={readOnly} onCommit={(name) => onChange({ kind: 'variable', name })} />
      )}
      {value.kind === 'literal' && <ScalarInput label="Value" type={type} value={value.value} readOnly={readOnly} onCommit={(scalar) => onChange({ kind: 'literal', value: scalar })} />}
      {value.kind === 'list' && <ListInput items={value.items} type={type} readOnly={readOnly} onChange={(items) => onChange({ kind: 'list', items })} />}
    </span>
  )
}

/** A list of values written separated by commas. */
function ListInput({ items, type, readOnly, onChange }: { items: Scalar[]; type: string; readOnly?: boolean; onChange: (items: Scalar[]) => void }) {
  return (
    <input
      className="input"
      aria-label="Values, separated by commas"
      defaultValue={items.join(', ')}
      readOnly={readOnly}
      onBlur={(e) => {
        const parsed = e.target.value.split(',').map((part) => part.trim()).filter((part) => part !== '').map((part) => parseScalar(part, type))
        if (parsed.every((p): p is Scalar => p !== null)) onChange(parsed)
      }}
    />
  )
}
