import { useQuery } from '@tanstack/react-query'
import { Archive, FileText, FileUp, LayoutTemplate, Map as MapIcon, Monitor, Moon, Sun } from 'lucide-react'
import { NavLink, Outlet, useLocation } from 'react-router'
import { api } from '../api/client'
import { useNavigationPages } from '../api/pages'
import { runStoredQuery } from '../widgets/core/dataSources'
import { useTheme, type ThemeChoice } from './theme'
import './AppShell.css'

function useCounts() {
  const artefacts = useQuery({
    queryKey: ['count', 'artefacts'],
    // The total of the artefact list query, asking for a single item.
    queryFn: async () => ((await runStoredQuery('artefact-list', { pageSize: 1 })) as { artefacts?: { total?: number } }).artefacts?.total ?? null,
    staleTime: 30_000,
  })
  const regions = useQuery({
    queryKey: ['count', 'regions'],
    queryFn: async () => (await api.GET('/api/regions')).data?.length ?? null,
    staleTime: 30_000,
  })
  const objectTypes = useQuery({
    queryKey: ['count', 'object-types'],
    queryFn: async () => (await api.GET('/api/object-types')).data?.length ?? null,
    staleTime: 30_000,
  })
  const reference = regions.data != null && objectTypes.data != null ? regions.data + objectTypes.data : null
  return { artefacts: artefacts.data, reference }
}

const THEMES: { value: ThemeChoice; label: string; Icon: typeof Sun }[] = [
  { value: 'light', label: 'Light', Icon: Sun },
  { value: 'dark', label: 'Dark', Icon: Moon },
  { value: 'system', label: 'Match system', Icon: Monitor },
]

export function AppShell() {
  const counts = useCounts()
  const pages = useNavigationPages().data ?? []
  const { pathname } = useLocation()
  const [theme, setTheme] = useTheme()

  return (
    <div className="shell">
      <a className="skip-link" href="#main">
        Skip to content
      </a>
      <nav className="rail" aria-label="Main">
        <div className="rail__brand">Artefacts</div>
        <ul className="rail__nav">
          <li>
            <NavLink to="/artefacts" className="rail__link">
              <Archive aria-hidden="true" />
              <span>Artefacts</span>
              {counts.artefacts != null && <span className="rail__count">{counts.artefacts.toLocaleString()}</span>}
            </NavLink>
          </li>
          <li>
            <NavLink to="/reference/regions" className={() => `rail__link${pathname.startsWith('/reference/') ? ' active' : ''}`}>
              <MapIcon aria-hidden="true" />
              <span>Reference lists</span>
              {counts.reference != null && <span className="rail__count">{counts.reference.toLocaleString()}</span>}
            </NavLink>
          </li>
          <li>
            <NavLink to="/imports" className="rail__link">
              <FileUp aria-hidden="true" />
              <span>Import</span>
            </NavLink>
          </li>
          <li>
            <NavLink to="/pages" end className={() => `rail__link${pathname === '/pages' || pathname.startsWith('/pages/') ? ' active' : ''}`}>
              <LayoutTemplate aria-hidden="true" />
              <span>Pages</span>
            </NavLink>
          </li>
        </ul>
        {pages.length > 0 && (
          <ul className="rail__nav rail__nav--pages" aria-label="Pages">
            {pages.map((page) => (
              <li key={page.id}>
                <NavLink to={`/p/${page.slug}`} className="rail__link">
                  <FileText aria-hidden="true" />
                  <span>{page.navigationLabel ?? page.name}</span>
                </NavLink>
              </li>
            ))}
          </ul>
        )}
        <fieldset className="rail__theme">
          <legend className="visually-hidden">Theme</legend>
          {THEMES.map(({ value, label, Icon }) => (
            <button
              key={value}
              type="button"
              className="rail__theme-btn"
              aria-pressed={theme === value}
              aria-label={label}
              title={label}
              onClick={() => setTheme(value)}
            >
              <Icon aria-hidden="true" />
            </button>
          ))}
        </fieldset>
      </nav>
      <main id="main" className="work" tabIndex={-1}>
        <Outlet />
      </main>
    </div>
  )
}
