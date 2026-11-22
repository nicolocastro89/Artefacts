/**
 * The block model of a query: what the Blocks view edits. A document converts to it (`toModel`) when every part of the
 * document has a block, and it prints back to a document (`toDocument`). The document stays the only stored form.
 */

export type Scalar = string | number | boolean

/** The value of a root argument: a variable, a literal, or an enum value (`TITLE`). */
export type ArgValue =
  | { kind: 'variable'; name: string }
  | { kind: 'literal'; value: Scalar }
  | { kind: 'enum'; value: string }

/** The value of a filter condition: a variable, a literal, or (for `in`, `nin`) a list of literals. */
export type ConditionValue =
  | { kind: 'variable'; name: string }
  | { kind: 'literal'; value: Scalar }
  | { kind: 'list'; items: Scalar[] }

export interface Condition {
  type: 'condition'
  field: string
  operator: string
  value: ConditionValue
}

export interface Group {
  type: 'group'
  op: 'and' | 'or'
  items: (Group | Condition)[]
}

export interface OrderItem {
  field: string
  direction: 'ASC' | 'DESC'
}

/** A selected field; `children` is empty for a field with a value, and lists the selected fields of an object. */
export interface SelectionNode {
  name: string
  children: SelectionNode[]
}

export interface QueryRoot {
  name: string
  /** Arguments other than `where` and `order`, in the order they are written. */
  args: Record<string, ArgValue>
  where: Group | null
  order: OrderItem[]
  selection: SelectionNode[]
}

export interface QueryVariable {
  name: string
  /** The GraphQL type as written, e.g. `String`, `UUID!`, `[String]`. */
  type: string
  default: ArgValue | null
}

export interface QueryModel {
  /** The operation's name, if it has one. */
  name: string | null
  variables: QueryVariable[]
  roots: QueryRoot[]
  /** Comments found in the document; printing drops them. */
  comments: string[]
}

export type Conversion = { ok: true; model: QueryModel } | { ok: false; reasons: string[] }

export const emptyGroup = (): Group => ({ type: 'group', op: 'and', items: [] })

export const emptyModel = (): QueryModel => ({ name: null, variables: [], roots: [], comments: [] })
