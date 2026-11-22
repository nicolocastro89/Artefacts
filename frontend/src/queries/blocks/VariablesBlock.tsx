import { Trash2 } from 'lucide-react'
import { useState } from 'react'
import { removeVariable, variableUses } from '../variables'
import type { QueryModel } from '../model'

interface Props {
  model: QueryModel
  readOnly?: boolean
  onChange: (model: QueryModel) => void
}

/** The variables of the query, with where each is used. A variable that is in use cannot be removed. */
export function VariablesBlock({ model, readOnly, onChange }: Props) {
  const [refusal, setRefusal] = useState<string | null>(null)
  if (model.variables.length === 0) return <p className="muted">No variables. Set a value to a variable to let a page or a person give it.</p>

  const remove = (name: string) => {
    const result = removeVariable(model, name)
    if (result.ok) {
      setRefusal(null)
      onChange(result.model)
    } else setRefusal(result.reason)
  }

  return (
    <div>
      <ul className="blocks__items">
        {model.variables.map((variable) => (
          <li key={variable.name} className="blocks__condition">
            <code>
              ${variable.name}: {variable.type}
            </code>
            <span className="muted">used by {variableUses(model, variable.name).join(', ') || 'nothing'}</span>
            {!readOnly && (
              <button type="button" className="btn btn--sm btn--danger" aria-label={`Remove $${variable.name}`} onClick={() => remove(variable.name)}>
                <Trash2 aria-hidden="true" />
              </button>
            )}
          </li>
        ))}
      </ul>
      {refusal && (
        <p className="field__error" role="alert">
          {refusal}
        </p>
      )}
    </div>
  )
}
