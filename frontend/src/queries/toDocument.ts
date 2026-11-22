import { Kind, parseType, print } from 'graphql'
import type {
  ArgumentNode,
  ConstValueNode,
  DocumentNode,
  FieldNode,
  OperationTypeNode,
  SelectionNode as GraphSelection,
  ValueNode,
  VariableDefinitionNode,
} from 'graphql'
import type { ArgValue, Condition, ConditionValue, Group, QueryModel, QueryRoot, Scalar, SelectionNode } from './model'

const name = (value: string) => ({ kind: Kind.NAME, value }) as const

/** Prints a model as a GraphQL document. Comments of the document it was read from are not kept. */
export function toDocument(model: QueryModel): string {
  const document: DocumentNode = {
    kind: Kind.DOCUMENT,
    definitions: [
      {
        kind: Kind.OPERATION_DEFINITION,
        operation: 'query' as OperationTypeNode,
        name: model.name ? name(model.name) : undefined,
        variableDefinitions: model.variables.map(variableDefinition),
        selectionSet: { kind: Kind.SELECTION_SET, selections: model.roots.map(rootField) },
      },
    ],
  }
  return print(document)
}

function variableDefinition(variable: QueryModel['variables'][number]): VariableDefinitionNode {
  return {
    kind: Kind.VARIABLE_DEFINITION,
    variable: { kind: Kind.VARIABLE, name: name(variable.name) },
    type: parseType(variable.type),
    // A default is a literal or an enum value, never a variable (the reader refuses one).
    defaultValue: variable.default ? (argValue(variable.default) as ConstValueNode) : undefined,
  }
}

function rootField(root: QueryRoot): FieldNode {
  const args: ArgumentNode[] = Object.entries(root.args).map(([key, value]) => ({ kind: Kind.ARGUMENT, name: name(key), value: argValue(value) }))
  if (root.where && root.where.items.length > 0) args.push({ kind: Kind.ARGUMENT, name: name('where'), value: groupValue(root.where) })
  if (root.order.length > 0) {
    args.push({
      kind: Kind.ARGUMENT,
      name: name('order'),
      value: {
        kind: Kind.LIST,
        values: root.order.map((o) => ({
          kind: Kind.OBJECT,
          fields: [{ kind: Kind.OBJECT_FIELD, name: name(o.field), value: { kind: Kind.ENUM, value: o.direction } }],
        })),
      },
    })
  }
  return {
    kind: Kind.FIELD,
    name: name(root.name),
    arguments: args,
    selectionSet: root.selection.length > 0 ? selectionSet(root.selection) : undefined,
  }
}

function selectionSet(nodes: SelectionNode[]) {
  return { kind: Kind.SELECTION_SET, selections: nodes.map(selectionField) } as const
}

function selectionField(node: SelectionNode): GraphSelection {
  return { kind: Kind.FIELD, name: name(node.name), selectionSet: node.children.length > 0 ? selectionSet(node.children) : undefined }
}

function scalarValue(value: Scalar): ValueNode {
  if (typeof value === 'string') return { kind: Kind.STRING, value }
  if (typeof value === 'boolean') return { kind: Kind.BOOLEAN, value }
  return Number.isInteger(value) ? { kind: Kind.INT, value: String(value) } : { kind: Kind.FLOAT, value: String(value) }
}

function argValue(value: ArgValue): ValueNode {
  switch (value.kind) {
    case 'variable':
      return { kind: Kind.VARIABLE, name: name(value.name) }
    case 'enum':
      return { kind: Kind.ENUM, value: value.value }
    case 'literal':
      return scalarValue(value.value)
  }
}

function conditionValue(value: ConditionValue): ValueNode {
  if (value.kind === 'list') return { kind: Kind.LIST, values: value.items.map(scalarValue) }
  return argValue(value)
}

/** A group prints as `{ and: [...] }` or `{ or: [...] }`; an `and` of one entry prints as that entry alone. */
function groupValue(group: Group): ValueNode {
  const only = group.items[0]
  if (group.op === 'and' && group.items.length === 1 && only) return itemValue(only)
  return {
    kind: Kind.OBJECT,
    fields: [{ kind: Kind.OBJECT_FIELD, name: name(group.op), value: { kind: Kind.LIST, values: group.items.map(itemValue) } }],
  }
}

function itemValue(item: Group | Condition): ValueNode {
  if (item.type === 'group') return groupValue(item)
  return {
    kind: Kind.OBJECT,
    fields: [
      {
        kind: Kind.OBJECT_FIELD,
        name: name(item.field),
        value: { kind: Kind.OBJECT, fields: [{ kind: Kind.OBJECT_FIELD, name: name(item.operator), value: conditionValue(item.value) }] },
      },
    ],
  }
}
