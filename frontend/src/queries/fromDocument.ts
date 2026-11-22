import { parse, print, TokenKind } from 'graphql'
import type { DocumentNode, FieldNode, ObjectValueNode, SelectionSetNode, Token, ValueNode } from 'graphql'
import type { BuilderField, BuilderFilterField, BuilderRoot, BuilderRules } from './rules'
import type { ArgValue, Condition, ConditionValue, Conversion, Group, OrderItem, QueryRoot, QueryVariable, Scalar, SelectionNode } from './model'

/**
 * Reads a document into blocks. Every part of the document must have a block: when something does not (an alias, a
 * fragment, a directive, a field or argument the rules do not know, a shape of filter the blocks cannot show), the
 * result lists each reason and the document is edited as text.
 */
export function toModel(document: string, rules: BuilderRules): Conversion {
  let parsed: DocumentNode
  try {
    parsed = parse(document)
  } catch (error) {
    return { ok: false, reasons: [`The document has a syntax error: ${error instanceof Error ? error.message : 'it cannot be read'}.`] }
  }

  const reasons: string[] = []
  const comments = commentsOf(parsed)

  const operations = parsed.definitions.filter((d) => d.kind === 'OperationDefinition')
  for (const definition of parsed.definitions) {
    if (definition.kind === 'FragmentDefinition') reasons.push(`The document defines the fragment "${definition.name.value}".`)
    else if (definition.kind !== 'OperationDefinition') reasons.push('The document defines something other than a query.')
  }
  if (operations.length > 1) reasons.push('The document has more than one operation.')
  const operation = operations[0]
  if (!operation) return { ok: false, reasons: reasons.length > 0 ? reasons : ['The document has no query.'] }
  if (operation.operation !== 'query') reasons.push(`The document is a ${operation.operation}, not a query.`)
  if ((operation.directives?.length ?? 0) > 0) reasons.push('The query has a directive.')

  const declared = new Set<string>()
  const variables: QueryVariable[] = []
  for (const definition of operation.variableDefinitions ?? []) {
    const name = definition.variable.name.value
    declared.add(name)
    if ((definition.directives?.length ?? 0) > 0) reasons.push(`The variable $${name} has a directive.`)
    let value: ArgValue | null = null
    if (definition.defaultValue) {
      value = argValue(definition.defaultValue, `the default of $${name}`, reasons, false)
    }
    variables.push({ name, type: print(definition.type), default: value })
  }

  const roots: QueryRoot[] = []
  for (const selection of operation.selectionSet.selections) {
    if (selection.kind !== 'Field') {
      reasons.push(selection.kind === 'FragmentSpread' ? `The query spreads the fragment "${selection.name.value}".` : 'The query has an inline fragment.')
      continue
    }
    const root = readRoot(selection, rules, reasons)
    if (root) roots.push(root)
  }

  const used = usedVariables(parsed)
  for (const name of used) if (!declared.has(name)) reasons.push(`The variable $${name} is used but not declared.`)

  return reasons.length > 0
    ? { ok: false, reasons }
    : { ok: true, model: { name: operation.name?.value ?? null, variables, roots, comments } }
}

function readRoot(field: FieldNode, rules: BuilderRules, reasons: string[]): QueryRoot | null {
  const name = field.name.value
  if (field.alias) reasons.push(`The field "${name}" has the alias "${field.alias.value}".`)
  if ((field.directives?.length ?? 0) > 0) reasons.push(`The field "${name}" has a directive.`)
  const root: BuilderRoot | undefined = rules.roots.find((r) => r.name === name)
  if (!root) {
    reasons.push(`"${name}" is not a field the query can start from.`)
    return null
  }

  const args: Record<string, ArgValue> = {}
  let where: Group | null = null
  let order: OrderItem[] = []
  for (const argument of field.arguments ?? []) {
    const argName = argument.name.value
    if (!root.arguments.some((a) => a.name === argName)) {
      reasons.push(`"${name}" has no argument "${argName}".`)
      continue
    }
    if (argName === 'where' && root.filter) where = readWhere(argument.value, root.filter, name, reasons)
    else if (argName === 'order' && root.sort) order = readOrder(argument.value, root.sort.map((s) => s.name), name, reasons)
    else args[argName] = argValue(argument.value, `the argument "${argName}" of "${name}"`, reasons, true) ?? { kind: 'literal', value: '' }
  }

  const objectType = rules.types[root.type]
  let selection: SelectionNode[] = []
  if (objectType) {
    if (!field.selectionSet) reasons.push(`"${name}" needs a selection.`)
    else selection = readSelection(field.selectionSet, root.type, rules, name, reasons)
  } else if (field.selectionSet) reasons.push(`"${name}" has a value, not fields to select.`)

  return { name, args, where, order, selection }
}

function readSelection(set: SelectionSetNode, typeName: string, rules: BuilderRules, path: string, reasons: string[]): SelectionNode[] {
  const nodes: SelectionNode[] = []
  for (const selection of set.selections) {
    if (selection.kind !== 'Field') {
      reasons.push(selection.kind === 'FragmentSpread' ? `${path} spreads the fragment "${selection.name.value}".` : `${path} has an inline fragment.`)
      continue
    }
    const name = selection.name.value
    const here = `${path}.${name}`
    if (selection.alias) reasons.push(`${here} has the alias "${selection.alias.value}".`)
    if ((selection.directives?.length ?? 0) > 0) reasons.push(`${here} has a directive.`)
    if ((selection.arguments?.length ?? 0) > 0) reasons.push(`${here} has arguments.`)
    const definition: BuilderField | undefined = (rules.types[typeName]?.fields ?? []).find((f) => f.name === name)
    if (!definition) {
      reasons.push(`${path} has no field "${name}".`)
      continue
    }
    if (definition.kind === 'object') {
      if (!selection.selectionSet) reasons.push(`${here} needs a selection.`)
      else nodes.push({ name, children: readSelection(selection.selectionSet, definition.type, rules, here, reasons) })
    } else {
      if (selection.selectionSet) reasons.push(`${here} has a value, not fields to select.`)
      nodes.push({ name, children: [] })
    }
  }
  return nodes
}

/** A scalar, enum or variable; anything else (a list, an object, null) has no block here. */
function argValue(value: ValueNode, where: string, reasons: string[], allowVariable: boolean): ArgValue | null {
  switch (value.kind) {
    case 'Variable':
      if (!allowVariable) {
        reasons.push(`${where} is a variable.`)
        return null
      }
      return { kind: 'variable', name: value.name.value }
    case 'StringValue':
      return { kind: 'literal', value: value.value }
    case 'IntValue':
    case 'FloatValue':
      return { kind: 'literal', value: Number(value.value) }
    case 'BooleanValue':
      return { kind: 'literal', value: value.value }
    case 'EnumValue':
      return { kind: 'enum', value: value.value }
    default:
      reasons.push(`${where} is a ${value.kind === 'ListValue' ? 'list' : value.kind === 'ObjectValue' ? 'object' : 'null'}, which has no block.`)
      return null
  }
}

function readOrder(value: ValueNode, sortable: string[], root: string, reasons: string[]): OrderItem[] {
  const items = value.kind === 'ListValue' ? value.values : [value]
  const order: OrderItem[] = []
  for (const item of items) {
    if (item.kind !== 'ObjectValue' || item.fields.length !== 1) {
      reasons.push(`"order" of "${root}" has an entry that is not a single field and direction.`)
      continue
    }
    const field = item.fields[0]!
    const direction = field.value.kind === 'EnumValue' ? field.value.value : null
    if (!sortable.includes(field.name.value)) reasons.push(`"${root}" cannot be ordered by "${field.name.value}".`)
    else if (direction !== 'ASC' && direction !== 'DESC') reasons.push(`The direction of "${field.name.value}" in "order" is not ASC or DESC.`)
    else order.push({ field: field.name.value, direction })
  }
  return order
}

function readWhere(value: ValueNode, filter: BuilderFilterField[], root: string, reasons: string[]): Group | null {
  if (value.kind !== 'ObjectValue') {
    reasons.push(`"where" of "${root}" is ${value.kind === 'Variable' ? 'a variable' : 'not an object'}, which has no block.`)
    return null
  }
  const item = readItem(value, filter, reasons)
  if (!item) return null
  return item.type === 'group' ? item : { type: 'group', op: 'and', items: [item] }
}

/** One object of a filter: its fields are ANDed, so several become a group and one stays as it is. */
function readItem(object: ObjectValueNode, filter: BuilderFilterField[], reasons: string[]): Group | Condition | null {
  const items: (Group | Condition)[] = []
  for (const field of object.fields) {
    const name = field.name.value
    if (name === 'and' || name === 'or') {
      const entries = field.value.kind === 'ListValue' ? field.value.values : [field.value]
      const group: Group = { type: 'group', op: name, items: [] }
      for (const entry of entries) {
        if (entry.kind !== 'ObjectValue') {
          reasons.push(`An entry of "${name}" in "where" is not an object.`)
          continue
        }
        const inner = readItem(entry, filter, reasons)
        if (inner) group.items.push(inner)
      }
      items.push(group)
      continue
    }
    const definition = filter.find((f) => f.name === name)
    if (!definition) {
      reasons.push(`"where" cannot filter on "${name}".`)
      continue
    }
    if (field.value.kind !== 'ObjectValue') {
      reasons.push(`The filter on "${name}" is not a set of operators.`)
      continue
    }
    for (const op of field.value.fields) {
      const operator = op.name.value
      if (operator === 'and' || operator === 'or') {
        reasons.push(`The filter on "${name}" combines operators with "${operator}", which has no block.`)
        continue
      }
      if (!definition.operators.some((o) => o.name === operator)) {
        reasons.push(`"${name}" has no operator "${operator}".`)
        continue
      }
      const conditionValue = readConditionValue(op.value, `${name} ${operator}`, reasons)
      if (conditionValue) items.push({ type: 'condition', field: name, operator, value: conditionValue })
    }
  }
  if (items.length === 1 && items[0]) return items[0]
  return { type: 'group', op: 'and', items }
}

function readConditionValue(value: ValueNode, where: string, reasons: string[]): ConditionValue | null {
  if (value.kind === 'Variable') return { kind: 'variable', name: value.name.value }
  if (value.kind === 'ListValue') {
    const items: Scalar[] = []
    for (const entry of value.values) {
      const scalar = scalarOf(entry)
      if (scalar === undefined) {
        reasons.push(`The list of ${where} has a value that is not a text, number or boolean.`)
        return null
      }
      items.push(scalar)
    }
    return { kind: 'list', items }
  }
  const scalar = scalarOf(value)
  if (scalar === undefined) {
    reasons.push(`The value of ${where} is not a text, number or boolean.`)
    return null
  }
  return { kind: 'literal', value: scalar }
}

function scalarOf(value: ValueNode): Scalar | undefined {
  switch (value.kind) {
    case 'StringValue':
      return value.value
    case 'IntValue':
    case 'FloatValue':
      return Number(value.value)
    case 'BooleanValue':
      return value.value
    default:
      return undefined
  }
}

/** The text of every comment in the document, which an AST does not keep. */
function commentsOf(document: DocumentNode): string[] {
  const comments: string[] = []
  for (let token: Token | null | undefined = document.loc?.startToken; token; token = token.next) {
    if (token.kind === TokenKind.COMMENT) comments.push(token.value ?? '')
  }
  return comments
}

/** Every variable used in a value of the document, not counting its declarations. */
function usedVariables(document: DocumentNode): Set<string> {
  const used = new Set<string>()
  const visitValue = (value: ValueNode) => {
    if (value.kind === 'Variable') used.add(value.name.value)
    else if (value.kind === 'ListValue') value.values.forEach(visitValue)
    else if (value.kind === 'ObjectValue') value.fields.forEach((f) => visitValue(f.value))
  }
  const visitSet = (set: SelectionSetNode) => {
    for (const selection of set.selections) {
      if (selection.kind === 'Field') {
        selection.arguments?.forEach((a) => visitValue(a.value))
        if (selection.selectionSet) visitSet(selection.selectionSet)
      }
    }
  }
  for (const definition of document.definitions) if (definition.kind === 'OperationDefinition') visitSet(definition.selectionSet)
  return used
}
