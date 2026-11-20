import type { OperationId } from '../../api/fieldTypes.generated'

/**
 * A page is serialisable data: containers own data sources, widgets bind response paths to slots.
 * Built-in pages are written as PageDefinition constants; user-defined pages will be stored the same way.
 */

/** A parameter value: a literal, or taken from the route params or the URL search params. */
export type ParamValue =
  | string
  | number
  | boolean
  | { from: 'route'; name: string }
  | { from: 'search'; name: string; default?: string | number }

export type DataSource =
  | { kind: 'endpoint'; operationId: OperationId; params?: Record<string, ParamValue> }
  /** A stored custom query (query engine epic). Not executable yet. */
  | { kind: 'query'; queryId: string }
  /** Inline data, typed against an operation's response when `typedAs` is given (tests, previews). */
  | { kind: 'static'; data: unknown; typedAs?: OperationId }

export interface WidgetNode {
  type: 'widget'
  id: string
  widgetId: string
  /** slot name → path (relative to the enclosing scope) */
  bindings: Record<string, string>
  config?: Record<string, unknown>
}

/** Renders its children once per item of a list in the enclosing scope; children bind relative to the item. */
export interface RepeatNode {
  type: 'repeat'
  id: string
  itemsPath: string
  children: PageNode[]
  /** Optional layout of the repeated items. */
  layout?: LayoutKind
  /** Shown when the list is empty. */
  emptyText?: string
}

export interface ContainerNode {
  type: 'container'
  id: string
  dataSource: DataSource
  children: PageNode[]
}

/** `rack`: main area plus the pull-out rack beside it (a sheet on narrow screens). `steps`: a numbered sequence. */
export type LayoutKind = 'stack' | 'row' | 'split' | 'grid' | 'section' | 'rack' | 'steps'

/** Arranges children; `section` adds an optional heading. Layout never changes the data scope. */
export interface LayoutNode {
  type: 'layout'
  id: string
  layout: LayoutKind
  title?: string
  children: PageNode[]
}

export type PageNode = WidgetNode | RepeatNode | ContainerNode | LayoutNode

export interface PageDefinition {
  id: string
  title: string
  root: PageNode
}
