import { Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { addRoot, removeRoot, setArgument, setWhere, settle, updateRoot } from '../edit'
import type { QueryModel, QueryRoot } from '../model'
import type { BuilderRoot, BuilderRules } from '../rules'
import { FilterBlocks } from './FilterBlocks'
import { OrderBlocks } from './OrderBlocks'
import { SelectionTree } from './SelectionTree'
import { ValueInput } from './ValueInput'
import { VariablesBlock } from './VariablesBlock'
import './Blocks.css'

interface Props {
  model: QueryModel
  rules: BuilderRules
  readOnly?: boolean
  /** Called with the edited model, with its variables settled, when an edit can be applied. */
  onChange: (model: QueryModel) => void
}

/** The query as blocks: its roots with their options, filter, ordering and fields, and its variables. */
export function BlocksView({ model, rules, readOnly, onChange }: Props) {
  const [refusal, setRefusal] = useState<string | null>(null)

  const apply = (next: QueryModel) => {
    const settled = settle(next, rules)
    if (!settled.ok) {
      setRefusal(settled.reason)
      return
    }
    setRefusal(null)
    onChange(settled.model)
  }

  const variables = model.variables.map((v) => v.name)
  const available = rules.roots.filter((r) => !model.roots.some((m) => m.name === r.name))

  return (
    <div className="blocks">
      {refusal && (
        <p className="field__error" role="alert">
          {refusal}
        </p>
      )}

      {model.roots.map((root) => {
        const definition = rules.roots.find((r) => r.name === root.name)
        return definition ? (
          <RootBlock
            key={root.name}
            root={root}
            definition={definition}
            rules={rules}
            variables={variables}
            readOnly={readOnly}
            onChange={(name, change) => apply(updateRoot(model, name, change))}
            onModel={apply}
            model={model}
          />
        ) : null
      })}

      {!readOnly && available.length > 0 && (
        <div className="blocks__add-root">
          <span className="muted">Add to the query:</span>
          {available.map((root) => (
            <button key={root.name} type="button" className="btn btn--sm" onClick={() => apply(addRoot(model, root.name))}>
              <Plus aria-hidden="true" /> {root.name}
            </button>
          ))}
        </div>
      )}

      <section className="plate blocks__section" aria-labelledby="blocks-variables">
        <h3 id="blocks-variables">Variables</h3>
        <VariablesBlock model={model} readOnly={readOnly} onChange={onChange} />
      </section>
    </div>
  )
}

function RootBlock({ root, definition, rules, variables, readOnly, model, onChange, onModel }: {
  root: QueryRoot
  definition: BuilderRoot
  rules: BuilderRules
  variables: string[]
  readOnly?: boolean
  model: QueryModel
  onChange: (name: string, change: (root: QueryRoot) => QueryRoot) => void
  onModel: (model: QueryModel) => void
}) {
  const options = definition.arguments.filter((a) => a.kind !== 'input')
  const objectType = rules.types[definition.type] ? definition.type : null

  return (
    <section className="plate blocks__section" aria-labelledby={`root-${root.name}`}>
      <header className="blocks__head">
        <h3 id={`root-${root.name}`}>
          {root.name}
        </h3>
        {!readOnly && (
          <button type="button" className="btn btn--sm btn--danger" aria-label={`Remove ${root.name}`} onClick={() => onModel(removeRoot(model, root.name))}>
            <Trash2 aria-hidden="true" />
          </button>
        )}
      </header>

      {options.length > 0 && (
        <div className="blocks__block">
          <h4>Options</h4>
          <div className="blocks__options">
            {options.map((argument) => (
              <div key={argument.name} className="field">
                <span className="field__label">{argument.name}</span>
                <ValueInput
                  label={argument.name}
                  type={argument.type}
                  enumValues={argument.kind === 'enum' ? rules.enums[argument.type] : undefined}
                  max={argument.name === 'pageSize' ? rules.limits.maxPageSize : undefined}
                  value={root.args[argument.name]}
                  variables={variables}
                  readOnly={readOnly}
                  onChange={(value) => onModel(setArgument(model, root.name, argument.name, value))}
                />
              </div>
            ))}
          </div>
        </div>
      )}

      {definition.filter && (
        <div className="blocks__block">
          <h4>Filter</h4>
          <FilterBlocks
            group={root.where}
            fields={definition.filter}
            variables={variables}
            readOnly={readOnly}
            onChange={(group) => onModel(setWhere(model, root.name, group))}
          />
        </div>
      )}

      {definition.sort && (
        <div className="blocks__block">
          <h4>Order</h4>
          <OrderBlocks order={root.order} fields={definition.sort} readOnly={readOnly} onChange={(order) => onChange(root.name, (r) => ({ ...r, order }))} />
        </div>
      )}

      {objectType && (
        <div className="blocks__block">
          <h4>Fields</h4>
          <SelectionTree rules={rules} type={objectType} nodes={root.selection} readOnly={readOnly} onChange={(selection) => onChange(root.name, (r) => ({ ...r, selection }))} />
        </div>
      )}
    </section>
  )
}
