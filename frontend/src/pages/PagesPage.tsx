import { useQueryClient } from '@tanstack/react-query'
import { Eye, Pencil, Plus, Trash2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { addressOf, deletePage, pageKeys, usePageList, type StoredPageSummary } from '../api/pages'
import { message } from './lists'
import './PagesPage.css'

/** Lists the stored pages and opens them in the page editor. */
export function PagesPage() {
  const queryClient = useQueryClient()
  const pages = usePageList()
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    document.title = 'Pages · Artefacts'
  }, [])

  const remove = async (page: StoredPageSummary) => {
    if (!window.confirm(`Delete the page “${page.name}”?`)) return
    setError(null)
    try {
      await deletePage(page.id)
      await queryClient.invalidateQueries({ queryKey: pageKeys.all })
    } catch (e) {
      setError(message(e))
    }
  }

  return (
    <div className="pages">
      <div className="page-head">
        <div className="page-head__titles">
          <h1>Pages</h1>
          <p className="page-head__sub">
            Views composed of widgets that read stored queries. System pages ship with the application; duplicate one to change a copy.
          </p>
        </div>
        <Link className="btn btn--primary" to="/pages/new">
          <Plus aria-hidden="true" /> New page
        </Link>
      </div>

      {error && (
        <p className="field__error" role="alert">
          {error}
        </p>
      )}

      {pages.isPending ? (
        <div className="data-state" aria-busy="true">
          Loading…
        </div>
      ) : pages.isError ? (
        <div className="data-state data-state--error" role="alert">
          Could not load pages: {pages.error.message}
        </div>
      ) : (
        <div className="table-wrap">
          <table className="table">
            <caption className="visually-hidden">Pages</caption>
            <thead>
              <tr>
                <th scope="col">Name</th>
                <th scope="col">Address</th>
                <th scope="col">Kind</th>
                <th scope="col">In navigation</th>
                <th scope="col">
                  <span className="visually-hidden">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {pages.data.map((page) => (
                <tr key={page.id}>
                  <td data-label="Name">
                    <span className="cell-title">{page.name}</span>
                  </td>
                  <td data-label="Address">
                    <code>{addressOf(page)}</code>
                  </td>
                  <td data-label="Kind">{page.kind === 'system' ? 'System' : 'User'}</td>
                  <td data-label="In navigation">{page.showInNavigation ? (page.navigationLabel ?? 'Yes') : '—'}</td>
                  <td className="pages__actions">
                    <Link className="btn btn--sm" to={`/pages/${page.id}`}>
                      {page.kind === 'system' ? <Eye aria-hidden="true" /> : <Pencil aria-hidden="true" />} {page.kind === 'system' ? 'View' : 'Edit'}
                    </Link>
                    {page.kind === 'user' && (
                      <button type="button" className="btn btn--sm btn--danger" onClick={() => void remove(page)}>
                        <Trash2 aria-hidden="true" /> Delete
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
