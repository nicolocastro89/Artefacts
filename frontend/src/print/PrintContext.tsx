import { createContext, useContext } from 'react'

/** True while a page renders for print (preview or printing). Widgets switch to their print rendering. */
export const PrintContext = createContext(false)

export function usePrintMode(): boolean {
  return useContext(PrintContext)
}
