import { NavLink } from 'react-router'

/** Switches between the reference lists (regions, object types). Each list is its own route, so tabs are links. */
export function ReferenceTabs() {
  return (
    <nav className="seg reference-tabs" aria-label="Reference lists">
      <NavLink to="/reference/regions" className="seg__btn">
        Regions
      </NavLink>
      <NavLink to="/reference/object-types" className="seg__btn">
        Object types
      </NavLink>
    </nav>
  )
}
