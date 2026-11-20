import type { ComponentType } from 'react'

/**
 * Value types a binding slot can accept. Mirrors the backend field catalog types
 * (text, number, date, historical-date, money, image, reference, boolean) plus `list`.
 */
export type FieldType =
  | 'text'
  | 'number'
  | 'date'
  | 'historical-date'
  | 'money'
  | 'image'
  | 'reference'
  | 'boolean'
  | 'list'

/** A slot type: a concrete field type, or `any` for display widgets that format whatever scalar they receive. */
export type SlotType = FieldType | 'any'

export interface SlotSpec {
  type: SlotType
  required?: boolean
  label?: string
}

export interface PrintSpec {
  /** Never split across a printed page; moves to the next page (scaled down if taller than a page). */
  keepTogether?: boolean
  /** Not rendered in print (interactive editors, buttons). */
  hideInPrint?: boolean
}

/** A bound value together with its field type (when known), so widgets can format by type. */
export interface BoundValue {
  value: unknown
  type?: FieldType
  /** Fully qualified field key (scope prefix + path), used to look up types of nested paths. */
  key: string
}

export interface WidgetProps<C extends object = Record<string, unknown>> {
  /** Resolved values keyed by slot name (for widgets with dynamic slots: by binding name, in binding order). */
  values: Record<string, BoundValue>
  config: C
  /** Field types of the enclosing data source (undefined for untyped static data). */
  fieldTypes?: Readonly<Record<string, FieldType>>
}

/** The contract a widget declares when it is registered. */
export interface WidgetDefinition<C extends object = Record<string, unknown>> {
  id: string
  label: string
  /** Fixed binding slots. */
  slots: Record<string, SlotSpec>
  /** When set, the widget accepts any number of extra bindings of this slot type (e.g. a field list). */
  dynamicSlots?: SlotSpec
  /** Configuration options with their defaults. */
  config: C
  print: PrintSpec
  component: ComponentType<WidgetProps<C>>
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type AnyWidgetDefinition = WidgetDefinition<any>
