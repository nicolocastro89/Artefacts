import rulesFile from '../test/builderRules.json'
import { addRoot, isSelected, removeRoot, setArgument, setSelected, setWhere, settle } from './edit'
import { toDocument } from './toDocument'
import { toModel } from './fromDocument'
import { emptyModel } from './model'
import type { BuilderRules } from './rules'

const rules = rulesFile as unknown as BuilderRules

describe('selection', () => {
  it('creates the objects above a selected field and removes an object when its last field goes', () => {
    let nodes = setSelected([], ['acquisition', 'price', 'amount'], true)
    expect(nodes).toEqual([{ name: 'acquisition', children: [{ name: 'price', children: [{ name: 'amount', children: [] }] }] }])
    expect(isSelected(nodes, ['acquisition', 'price', 'amount'])).toBe(true)

    nodes = setSelected(nodes, ['acquisition', 'price', 'currency'], true)
    nodes = setSelected(nodes, ['acquisition', 'price', 'amount'], false)
    expect(nodes[0]?.children[0]?.children.map((c) => c.name)).toEqual(['currency'])

    nodes = setSelected(nodes, ['acquisition', 'price', 'currency'], false)
    expect(nodes).toEqual([])
  })
})

describe('settling variables after an edit', () => {
  it('declares a variable with the type of its place and drops it when it is no longer used', () => {
    let model = addRoot(emptyModel(), 'artefacts')
    model = setArgument(model, 'artefacts', 'search', { kind: 'variable', name: 'search' })

    const withVariable = settle(model, rules)
    expect(withVariable.ok && withVariable.model.variables).toEqual([{ name: 'search', type: 'String', default: null }])

    const without = settle(setArgument(withVariable.ok ? withVariable.model : model, 'artefacts', 'search', null), rules)
    expect(without.ok && without.model.variables).toEqual([])
  })

  it('refuses a variable used in places that take different types', () => {
    let model = addRoot(emptyModel(), 'artefacts')
    model = setArgument(model, 'artefacts', 'search', { kind: 'variable', name: 'x' })
    model = setArgument(model, 'artefacts', 'page', { kind: 'variable', name: 'x' })

    expect(settle(model, rules)).toEqual({ ok: false, reason: '$x is declared as String, but this place takes Int.' })
  })

  it('adds the identifier variable of a new single-artefact root and prints a document the server accepts in shape', () => {
    const settled = settle(addRoot(emptyModel(), 'artefact'), rules)

    expect(settled.ok && toDocument(settled.model)).toBe('query ($id: UUID!) {\n  artefact(id: $id) {\n    id\n    title\n  }\n}')
  })

  it('uses a variable in a condition with the type of the operator', () => {
    let model = addRoot(emptyModel(), 'artefacts')
    model = setWhere(model, 'artefacts', {
      type: 'group',
      op: 'and',
      items: [{ type: 'condition', field: 'imageCount', operator: 'in', value: { kind: 'variable', name: 'counts' } }],
    })

    const settled = settle(model, rules)

    expect(settled.ok && settled.model.variables).toEqual([{ name: 'counts', type: '[Int]', default: null }])
  })

  it('round trips what an edit produced', () => {
    let model = addRoot(emptyModel(), 'artefacts')
    model = setArgument(model, 'artefacts', 'pageSize', { kind: 'literal', value: 10 })
    model = removeRoot(addRoot(model, 'language'), 'language')

    const document = toDocument(model)
    const read = toModel(document, rules)

    expect(read.ok && read.model.roots[0]?.args).toEqual({ pageSize: { kind: 'literal', value: 10 } })
  })
})
