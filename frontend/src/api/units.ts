import { useQuery } from '@tanstack/react-query'
import { api } from './client'
import { unwrap } from './problem'

export const UNITS_KEY = ['units'] as const

/** Every unit of measure, smallest first, as the unit pickers offer them. */
export function useUnits() {
  return useQuery({
    queryKey: UNITS_KEY,
    queryFn: async () => unwrap(await api.GET('/api/units')),
    staleTime: 60_000,
  })
}
