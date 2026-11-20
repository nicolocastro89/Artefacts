import { useQuery } from '@tanstack/react-query'
import { api, type Language } from '../api/client'
import { unwrap } from '../api/problem'

/** Content languages in fallback order (the languages names and descriptions can be recorded in). */
export function useContentLanguages() {
  return useQuery({
    queryKey: ['languages'],
    queryFn: async () => unwrap(await api.GET('/api/languages')),
    staleTime: 5 * 60_000,
  })
}

/**
 * The language the reader reads in: the first of the browser's languages that is a content language (exact, then by
 * primary subtag), else the first in fallback order. The API makes the same choice from Accept-Language, so the
 * frontend never needs to send `lang`.
 */
export function readerLanguageOf(languages: Language[], browser: readonly string[] = navigator.languages ?? [navigator.language]): string {
  const codes = languages.map((l) => l.code)
  for (const tag of browser) {
    const code = tag.toLowerCase()
    if (codes.includes(code)) return code
    const primary = code.split('-')[0]!
    if (codes.includes(primary)) return primary
  }
  return codes[0] ?? 'en'
}

export function useReaderLanguage(): string {
  const { data = [] } = useContentLanguages()
  return readerLanguageOf(data)
}

/** Display name of a language code, from the content language list when known. */
export function languageName(code: string, languages: Language[]): string {
  return languages.find((l) => l.code === code)?.name ?? code
}
