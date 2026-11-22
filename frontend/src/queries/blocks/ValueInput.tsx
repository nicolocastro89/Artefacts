import { useId, useState } from 'react'
import type { ArgValue, Scalar } from '../model'
import { defaultScalar, isNumberType, parseScalar } from '../values'

const NAME = /^[A-Za-z_][A-Za-z0-9_]*$/

interface ScalarInputProps {
  label: string
  type: string
  value: Scalar
  max?: number
  readOnly?: boolean
  onCommit: (value: Scalar) => void
}

/** A text, number or yes/no input for one value; a value that does not read as the type is not committed. */
export function ScalarInput({ label, type, value, max, readOnly, onCommit }: ScalarInputProps) {
  const [text, setText] = useState(String(value))
  const [problem, setProblem] = useState<string | null>(null)

  if (type === 'Boolean') {
    return (
      <select className="input" aria-label={label} value={String(value)} disabled={readOnly} onChange={(e) => onCommit(e.target.value === 'true')}>
        <option value="true">Yes</option>
        <option value="false">No</option>
      </select>
    )
  }

  const change = (next: string) => {
    setText(next)
    const parsed = parseScalar(next, type)
    if (parsed === null) {
      setProblem('Enter a number.')
    } else if (typeof parsed === 'number' && max !== undefined && parsed > max) {
      setProblem(`The most is ${max}.`)
    } else {
      setProblem(null)
      onCommit(parsed)
    }
  }

  return (
    <span className="blocks__value">
      <input
        className="input"
        aria-label={label}
        aria-invalid={problem !== null}
        inputMode={isNumberType(type) ? 'decimal' : undefined}
        value={text}
        readOnly={readOnly}
        onChange={(e) => change(e.target.value)}
      />
      {problem && (
        <span className="field__error" role="alert">
          {problem}
        </span>
      )}
    </span>
  )
}

interface VariableNameProps {
  label: string
  name: string
  variables: string[]
  readOnly?: boolean
  onCommit: (name: string) => void
}

/** The name of a variable: an existing one or a new one, taken when the field is left or Enter is pressed. */
export function VariableName({ label, name, variables, readOnly, onCommit }: VariableNameProps) {
  const listId = useId()
  const [text, setText] = useState(name)
  const valid = NAME.test(text)
  const commit = () => {
    if (valid && text !== name) onCommit(text)
  }
  return (
    <span className="blocks__value">
      <span className="blocks__sigil" aria-hidden="true">
        $
      </span>
      <input
        className="input"
        aria-label={label}
        aria-invalid={!valid}
        list={listId}
        value={text}
        readOnly={readOnly}
        onChange={(e) => setText(e.target.value)}
        onBlur={commit}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            e.preventDefault()
            commit()
          }
        }}
      />
      <datalist id={listId}>
        {variables.map((v) => (
          <option key={v} value={v} />
        ))}
      </datalist>
      {!valid && (
        <span className="field__error" role="alert">
          Use letters, digits and underscores.
        </span>
      )}
    </span>
  )
}

interface ValueInputProps {
  label: string
  /** The GraphQL type of the argument: a scalar (`Int`) or an enum (`ArtefactSort`). */
  type: string
  enumValues?: string[]
  value: ArgValue | undefined
  max?: number
  variables: string[]
  readOnly?: boolean
  onChange: (value: ArgValue | null) => void
}

/** The value of an argument: not set, a value, or a variable. */
export function ValueInput({ label, type, enumValues, value, max, variables, readOnly, onChange }: ValueInputProps) {
  const mode = value?.kind === 'variable' ? 'variable' : value ? 'value' : 'unset'
  const choose = (next: string) => {
    if (next === 'unset') onChange(null)
    else if (next === 'variable') onChange({ kind: 'variable', name: variables[0] ?? 'value' })
    else if (enumValues) onChange({ kind: 'enum', value: enumValues[0] ?? '' })
    else onChange({ kind: 'literal', value: defaultScalar(type) })
  }

  return (
    <span className="blocks__argument">
      <select className="input" aria-label={`${label}: how it is set`} value={mode} disabled={readOnly} onChange={(e) => choose(e.target.value)}>
        <option value="unset">Not set</option>
        <option value="value">{enumValues ? 'Choose' : 'Value'}</option>
        <option value="variable">Variable</option>
      </select>
      {value?.kind === 'variable' && (
        <VariableName key={value.name} label={`${label}: variable`} name={value.name} variables={variables} readOnly={readOnly} onCommit={(name) => onChange({ kind: 'variable', name })} />
      )}
      {value?.kind === 'enum' && enumValues && (
        <select className="input" aria-label={label} value={value.value} disabled={readOnly} onChange={(e) => onChange({ kind: 'enum', value: e.target.value })}>
          {enumValues.map((v) => (
            <option key={v} value={v}>
              {v}
            </option>
          ))}
        </select>
      )}
      {value?.kind === 'literal' && (
        <ScalarInput label={label} type={type} value={value.value} max={max} readOnly={readOnly} onCommit={(scalar) => onChange({ kind: 'literal', value: scalar })} />
      )}
    </span>
  )
}
