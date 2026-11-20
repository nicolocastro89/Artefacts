import { typeAt } from './paths'
import type { AnyWidgetDefinition, FieldType, SlotSpec } from './types'

export interface BindingProblem {
  slot: string
  message: string
}

/** True when a value of `field` type may be bound to a slot of `slot` type. */
export function isCompatible(slot: SlotSpec['type'], field: FieldType): boolean {
  if (slot === 'any') return field !== 'list'
  return slot === field
}

/**
 * Checks a widget's bindings against its declared slots and, when the scope's field types are known,
 * against the type of each bound path. Returns every problem found (empty when valid).
 */
export function validateBindings(
  widget: AnyWidgetDefinition,
  bindings: Readonly<Record<string, string>>,
  fieldTypes: Readonly<Record<string, FieldType>> | undefined,
  prefix: string,
): BindingProblem[] {
  const problems: BindingProblem[] = []

  for (const [slot, spec] of Object.entries(widget.slots)) {
    if (spec.required && bindings[slot] === undefined)
      problems.push({ slot, message: `Required slot "${slot}" has no binding.` })
  }

  for (const [slot, path] of Object.entries(bindings)) {
    const spec = widget.slots[slot] ?? widget.dynamicSlots
    if (!spec) {
      problems.push({ slot, message: `Widget "${widget.id}" has no slot "${slot}".` })
      continue
    }
    if (!fieldTypes) continue // untyped (static) data: no type information to check against
    const fieldType = typeAt(fieldTypes, prefix, path)
    if (!fieldType) {
      problems.push({ slot, message: `"${path}" is not a field of this data source.` })
    } else if (!isCompatible(spec.type, fieldType)) {
      problems.push({ slot, message: `Slot "${slot}" expects ${spec.type} but "${path}" is ${fieldType}.` })
    }
  }

  return problems
}
