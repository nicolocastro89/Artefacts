/** Names keyed by language code, as edited (blank = no name in that language). */
export type NameDraft = Record<string, string>

/** Drops blank names; the API takes the remaining ones as the entry's names. */
export function namesOf(draft: NameDraft): Record<string, string> {
  return Object.fromEntries(
    Object.entries(draft)
      .map(([k, v]) => [k, v.trim()])
      .filter(([, v]) => v !== ''),
  )
}
