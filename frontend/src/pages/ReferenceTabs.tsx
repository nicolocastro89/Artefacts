import { useEffect, useRef } from 'react'
import { NavLink, useLocation } from 'react-router'

const TABS = [
  { to: '/reference/regions', label: 'Regions' },
  { to: '/reference/object-types', label: 'Object types' },
  { to: '/reference/dynasties', label: 'Dynasties' },
  { to: '/reference/collection-groups', label: 'Collection groups' },
  { to: '/reference/storage-locations', label: 'Storage locations' },
  { to: '/reference/parties', label: 'Parties' },
  { to: '/reference/units', label: 'Units' },
  { to: '/reference/languages', label: 'Languages' },
]

/**
 * Switches between the reference lists. Each list is its own route, so tabs are links. On narrow screens the strip
 * scrolls sideways, keeping the current list's tab in view.
 */
export function ReferenceTabs() {
  const nav = useRef<HTMLElement>(null)
  const { pathname } = useLocation()

  useEffect(() => {
    const strip = nav.current
    const active = strip?.querySelector<HTMLElement>('.active')
    if (!strip || !active || strip.scrollWidth <= strip.clientWidth) return
    strip.scrollLeft = active.offsetLeft - (strip.clientWidth - active.offsetWidth) / 2
  }, [pathname])

  return (
    <nav ref={nav} className="seg reference-tabs" aria-label="Reference lists">
      {TABS.map((tab) => (
        <NavLink key={tab.to} to={tab.to} className="seg__btn">
          {tab.label}
        </NavLink>
      ))}
    </nav>
  )
}
