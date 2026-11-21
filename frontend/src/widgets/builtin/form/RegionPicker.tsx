import { api } from '../../../api/client'
import type { ReactNode } from 'react'
import { unwrap } from '../../../api/problem'
import { listSources, partySource } from './pickerSources'
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
  add: async (name, language) => unwrap(await api.POST('/api/regions', { body: { names: { [language]: name } } })),
}

const objectTypeSource: RefSource = {
  key: 'object-types',
  noun: 'object type',
  search: async (text) => unwrap(await api.GET('/api/object-types', { params: { query: text ? { search: text } : {} } })),
  add: async (name, language) => unwrap(await api.POST('/api/object-types', { body: { names: { [language]: name } } })),
}

interface PickerProps {
  label: string
  value: PickedRef | null
  onChange: (picked: PickedRef | null) => void
  errors?: string[]
  hint?: ReactNode
}

/** Picks a region from the region reference list, adding it inline when it does not exist. */
export function RegionPicker(props: PickerProps) {
  return <ReferencePicker {...props} source={regionSource} />
}

/** Picks an object type from the object type reference list, adding it inline when it does not exist. */
export function ObjectTypePicker(props: PickerProps) {
  return <ReferencePicker {...props} source={objectTypeSource} />
}

/** Picks a dynasty or period, adding it inline (named in the reader's language) when it does not exist. */
export function DynastyPicker(props: PickerProps) {
  return <ReferencePicker {...props} source={listSources.dynasty} />
}

/** Picks a collection group, adding it inline when it does not exist. */
export function CollectionGroupPicker(props: PickerProps) {
  return <ReferencePicker {...props} source={listSources.collectionGroup} />
}

/** Picks a storage location (a single-name code), adding it inline when it does not exist. */
export function StorageLocationPicker(props: PickerProps) {
  return <ReferencePicker {...props} source={listSources.storageLocation} />
}

/** Picks a person or organisation, adding it inline as the kind chosen (organisation first) when it does not exist. */
export function PartyPicker(props: PickerProps) {
  return <ReferencePicker {...props} source={partySource} />
}
