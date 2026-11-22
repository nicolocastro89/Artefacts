import type { BuilderRules } from './rules'
import type { Condition, Group, QueryModel, QueryVariable } from './model'

/** Where a variable is used: a root argument, or the value of a filter condition. */
export type Place = { kind: 'argument'; root: string; argument: string } | { kind: 'condition'; root: string; field: string; operator: string }

/**
 * The GraphQL type a variable must have to be used in a place: the type of the argument or of the condition's operator.
 * It is nullable unless the argument must be given (a non-null argument without a default), so an argument that has a
 * default (`page: Int! = 1`) takes a variable that may be left out.
 */
export function placeType(rules: BuilderRules, place: Place): string | null {
  const root = rules.roots.find((r) => r.name === place.root)
  if (!root) return null
  if (place.kind === 'argument') {
    const argument = root.arguments.find((a) => a.name === place.argument)
    if (!argument) return null
    const type = argument.list ? `[${argument.type}]` : argument.type
    return argument.required ? `${type}!` : type
  }
  const field = root.filter?.find((f) => f.name === place.field)
  const operator = field?.operators.find((o) => o.name === place.operator)
  if (!field || !operator) return null
  return operator.list ? `[${field.graphQlType}]` : field.graphQlType
}

export type VariableResult = { ok: true; model: QueryModel } | { ok: false; reason: string }

/** Declares the variable if it is new, or checks that an existing one has the type of the place it is used in. */
export function declareVariable(model: QueryModel, rules: BuilderRules, name: string, place: Place): VariableResult {
  const type = placeType(rules, place)
  if (!type) return { ok: false, reason: 'There is no such place for a variable.' }
  const existing = model.variables.find((v) => v.name === name)
  if (existing) {
    return sameType(existing.type, type)
      ? { ok: true, model }
      : { ok: false, reason: `$${name} is declared as ${existing.type}, but this place takes ${type}.` }
  }
  const variable: QueryVariable = { name, type, default: null }
  return { ok: true, model: { ...model, variables: [...model.variables, variable] } }
}

/** The places that use a variable, described for a person. */
export function variableUses(model: QueryModel, name: string): string[] {
  const uses: string[] = []
  for (const root of model.roots) {
    for (const [argument, value] of Object.entries(root.args)) {
      if (value.kind === 'variable' && value.name === name) uses.push(`${root.name}: ${argument}`)
    }
    const visit = (group: Group) => {
      for (const item of group.items) {
        if (item.type === 'group') visit(item)
        else if (item.value.kind === 'variable' && item.value.name === name) uses.push(`${root.name}: ${describe(item)}`)
      }
    }
    if (root.where) visit(root.where)
  }
  return uses
}

const describe = (condition: Condition) => `${condition.field} ${condition.operator}`

/** Removes a variable that nothing uses; refuses, naming the places, when it is in use. */
export function removeVariable(model: QueryModel, name: string): VariableResult {
  const uses = variableUses(model, name)
  if (uses.length > 0) return { ok: false, reason: `$${name} is used by ${uses.join(', ')}.` }
  return { ok: true, model: { ...model, variables: model.variables.filter((v) => v.name !== name) } }
}

/** Drops variables that nothing uses any more (after a block that used one is removed). */
export function pruneVariables(model: QueryModel): QueryModel {
  return { ...model, variables: model.variables.filter((v) => variableUses(model, v.name).length > 0) }
}

/** `Int` and `Int!` are the same place for a variable that may be left out; list and non-null marks must otherwise agree. */
function sameType(declared: string, wanted: string): boolean {
  const strip = (type: string) => type.replace(/!/g, '')
  return strip(declared) === strip(wanted)
}
