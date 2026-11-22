import type { BuilderRules } from '../rules'
import type { SelectionNode } from '../model'
import { isSelected, setSelected } from '../edit'

interface Props {
  rules: BuilderRules
  /** The GraphQL type whose fields are listed (the root's type, then each object's type). */
  type: string
  nodes: SelectionNode[]
  readOnly?: boolean
  onChange: (nodes: SelectionNode[]) => void
}

/** The fields of a type as a tree: tick a field to select it; objects open to show their own fields. */
export function SelectionTree({ rules, type, nodes, readOnly, onChange }: Props) {
  return <Fields rules={rules} type={type} all={nodes} path={[]} nodes={nodes} readOnly={readOnly} onChange={onChange} />
}

function Fields({ rules, type, all, path, nodes, readOnly, onChange }: Props & { all: SelectionNode[]; path: string[] }) {
  const fields = rules.types[type]?.fields ?? []
  return (
    <ul className="blocks__tree">
      {fields.map((field) => {
        const here = [...path, field.name]
        const selected = nodes.find((n) => n.name === field.name)
        if (field.kind === 'object') {
          const count = countFields(selected)
          return (
            <li key={field.name}>
              <details open={count > 0}>
                <summary>
                  <span>{field.name}</span>
                  {field.list && <span className="blocks__tag">list</span>}
                  {field.sensitivity && <span className="blocks__tag blocks__tag--sensitive">{field.sensitivity}</span>}
                  {count > 0 && <span className="blocks__count">{count} selected</span>}
                </summary>
                <Fields rules={rules} type={field.type} all={all} path={here} nodes={selected?.children ?? []} readOnly={readOnly} onChange={onChange} />
              </details>
            </li>
          )
        }
        return (
          <li key={field.name}>
            <label className="blocks__leaf">
              <input
                type="checkbox"
                checked={isSelected(all, here)}
                disabled={readOnly}
                onChange={(e) => onChange(setSelected(all, here, e.target.checked))}
              />
              <span>{field.name}</span>
              {field.list && <span className="blocks__tag">list</span>}
              {field.sensitivity && <span className="blocks__tag blocks__tag--sensitive">{field.sensitivity}</span>}
            </label>
          </li>
        )
      })}
    </ul>
  )
}

function countFields(node: SelectionNode | undefined): number {
  if (!node) return 0
  return node.children.length === 0 ? 1 : node.children.reduce((sum, child) => sum + countFields(child), 0)
}
