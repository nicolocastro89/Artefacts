import { useQuery } from '@tanstack/react-query'
import { Archive, Map as MapIcon, Monitor, Moon, Sun } from 'lucide-react'
import { NavLink, Outlet } from 'react-router'
import { api } from '../api/client'
import { useTheme, type ThemeChoice } from './theme'
import './AppShell.css'

function useCounts() {
  const artefacts = useQuery({
    queryKey: ['count', 'artefacts'],
    queryFn: async () => (await api.GET('/api/artefacts', { params: { query: { pageSize: 1 } } })).data?.total ?? null,
    staleTime: 30_000,
  })
  const regions = useQuery({
    queryKey: ['count', 'regions'],
    queryFn: async () => (await api.GET('/api/regions')).data?.length ?? null,
    staleTime: 30_000,
  })
  return { artefacts: artefacts.data, regions: regions.data }
}

const THEMES: { value: ThemeChoice; label: string; Icon: typeof Sun }[] = [
  { value: 'light', label: 'Light', Icon: Sun },
  { value: 'dark', label: 'Dark', Icon: Moon },
  { value: 'system', label: 'Match system', Icon: Monitor },
]

export function AppShell() {
  const counts = useCounts()
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
            <NavLink to="/regions" className="rail__link">
              <MapIcon aria-hidden="true" />
              <span>Regions</span>
              {counts.regions != null && <span className="rail__count">{counts.regions.toLocaleString()}</span>}
            </NavLink>
          </li>
        </ul>
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
