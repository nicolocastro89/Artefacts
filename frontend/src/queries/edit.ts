import type { BuilderRules } from './rules'
import type { ArgValue, Group, QueryModel, QueryRoot, QueryVariable, SelectionNode } from './model'
import { placeType, type Place } from './variables'

export type Settled = { ok: true; model: QueryModel } | { ok: false; reason: string }

/** The model with the root of this name changed. */
export function updateRoot(model: QueryModel, name: string, change: (root: QueryRoot) => QueryRoot): QueryModel {
  return { ...model, roots: model.roots.map((root) => (root.name === name ? change(root) : root)) }
}

/** A new root with a selection that returns something useful straight away. */
export function newRoot(name: string): QueryRoot {
  const field = (n: string, children: SelectionNode[] = []): SelectionNode => ({ name: n, children })
  switch (name) {
    case 'artefacts':
      return { name, args: {}, where: null, order: [], selection: [field('total'), field('items', [field('id'), field('title')])] }
    case 'artefact':
      return { name, args: { id: { kind: 'variable', name: 'id' } }, where: null, order: [], selection: [field('id'), field('title')] }
    default:
      return { name, args: {}, where: null, order: [], selection: [] }
  }
}

export const addRoot = (model: QueryModel, name: string): QueryModel =>
  model.roots.some((r) => r.name === name) ? model : { ...model, roots: [...model.roots, newRoot(name)] }

export const removeRoot = (model: QueryModel, name: string): QueryModel => ({ ...model, roots: model.roots.filter((r) => r.name !== name) })

/** Sets a root argument (null removes it). */
export const setArgument = (model: QueryModel, root: string, argument: string, value: ArgValue | null): QueryModel =>
  updateRoot(model, root, (r) => {
    const args = { ...r.args }
    if (value) args[argument] = value
    else delete args[argument]
    return { ...r, args }
  })

export const setWhere = (model: QueryModel, root: string, where: Group | null): QueryModel =>
  updateRoot(model, root, (r) => ({ ...r, where: where && where.items.length > 0 ? where : null }))

export function isSelected(nodes: SelectionNode[], path: string[]): boolean {
  const [head, ...rest] = path
  const node = nodes.find((n) => n.name === head)
  if (!node) return false
  return rest.length === 0 ? true : isSelected(node.children, rest)
}

/**
 * Selects or deselects the field at a path. Selecting a field creates the objects above it that are not selected yet;
 * deselecting the last field of an object removes the object, which could not be selected on its own.
 */
export function setSelected(nodes: SelectionNode[], path: string[], selected: boolean): SelectionNode[] {
  const [head, ...rest] = path
  if (head === undefined) return nodes
  const index = nodes.findIndex((n) => n.name === head)
  if (rest.length === 0) {
    if (selected) return index >= 0 ? nodes : [...nodes, { name: head, children: [] }]
    return nodes.filter((n) => n.name !== head)
  }
  if (index < 0) return selected ? [...nodes, { name: head, children: setSelected([], rest, true) }] : nodes
  const children = setSelected(nodes[index]!.children, rest, selected)
  const next = nodes.slice()
  if (children.length === 0) next.splice(index, 1)
  else next[index] = { ...nodes[index]!, children }
  return next
}

/** Every place a variable is used, with the name of the variable there. */
function uses(model: QueryModel): { name: string; place: Place }[] {
  const found: { name: string; place: Place }[] = []
  for (const root of model.roots) {
    for (const [argument, value] of Object.entries(root.args)) {
      if (value.kind === 'variable') found.push({ name: value.name, place: { kind: 'argument', root: root.name, argument } })
    }
    const visit = (group: Group) => {
      for (const item of group.items) {
        if (item.type === 'group') visit(item)
        else if (item.value.kind === 'variable')
          found.push({ name: item.value.name, place: { kind: 'condition', root: root.name, field: item.field, operator: item.operator } })
      }
    }
    if (root.where) visit(root.where)
  }
  return found
}

const bare = (type: string) => type.replace(/!/g, '')

/**
 * Makes the declared variables match their uses after an edit: a variable used for the first time is declared with the
 * type of the place, one that is no longer used is dropped, and a variable used in places that take different types is
 * refused, naming the variable.
 */
export function settle(model: QueryModel, rules: BuilderRules): Settled {
  const declared = new Map<string, QueryVariable>(model.variables.map((v) => [v.name, v]))
  const used = new Map<string, QueryVariable>()
  for (const { name, place } of uses(model)) {
    const type = placeType(rules, place)
    if (!type) return { ok: false, reason: `$${name} is used in a place that does not take a variable.` }
    const known = used.get(name) ?? declared.get(name)
    if (known && bare(known.type) !== bare(type)) return { ok: false, reason: `$${name} is declared as ${known.type}, but this place takes ${type}.` }
    if (!used.has(name)) used.set(name, known ?? { name, type, default: null })
  }
  const kept = model.variables.filter((v) => used.has(v.name)).map((v) => used.get(v.name)!)
  const added = [...used.values()].filter((v) => !declared.has(v.name))
  return { ok: true, model: { ...model, variables: [...kept, ...added] } }
}
