import type { AnyWidgetDefinition, WidgetDefinition } from './types'

const widgets = new Map<string, AnyWidgetDefinition>()

/** Declares a widget with a typed config and registers it under its stable id. */
export function defineWidget<C extends object>(definition: WidgetDefinition<C>): WidgetDefinition<C> {
  if (widgets.has(definition.id) && widgets.get(definition.id) !== definition)
    throw new Error(`A widget with id "${definition.id}" is already registered.`)
  widgets.set(definition.id, definition)
  return definition
}

export function getWidget(id: string): AnyWidgetDefinition | undefined {
  return widgets.get(id)
}

/** Lists every registered widget and its contract (slots, config defaults, print behaviour). */
export function listWidgets(): AnyWidgetDefinition[] {
  return [...widgets.values()].sort((a, b) => a.id.localeCompare(b.id))
}

/** Test helper: removes a widget registered by a test. */
export function unregisterWidget(id: string): void {
  widgets.delete(id)
}
