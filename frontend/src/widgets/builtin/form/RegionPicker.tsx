import { api } from '../../../api/client'
import { unwrap } from '../../../api/problem'
import { ReferencePicker, type PickedRef, type RefSource } from './ReferencePicker'

export type PickedRegion = PickedRef

const regionSource: RefSource = {
  key: 'regions',
  noun: 'region',
  search: async (text) =>
    unwrap(await api.GET('/api/regions', { params: { query: text ? { search: text } : {} } })).map((r) => ({
      id: r.id,
      name: r.name,
      secondary: r.parentName,
    })),
  add: async (name) => unwrap(await api.POST('/api/regions', { body: { name } })),
}

const objectTypeSource: RefSource = {
  key: 'object-types',
  noun: 'object type',
  search: async (text) => unwrap(await api.GET('/api/object-types', { params: { query: text ? { search: text } : {} } })),
  add: async (name) => unwrap(await api.POST('/api/object-types', { body: { name } })),
}

interface PickerProps {
  label: string
  value: PickedRef | null
  onChange: (picked: PickedRef | null) => void
  errors?: string[]
}

/** Picks a region from the region reference list, adding it inline when it does not exist. */
export function RegionPicker(props: PickerProps) {
  return <ReferencePicker {...props} source={regionSource} />
}

/** Picks an object type from the object type reference list, adding it inline when it does not exist. */
export function ObjectTypePicker(props: PickerProps) {
  return <ReferencePicker {...props} source={objectTypeSource} />
}
