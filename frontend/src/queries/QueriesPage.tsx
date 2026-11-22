import { useQueryClient } from '@tanstack/react-query'
import { Eye, Pencil, Plus, Trash2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { deleteQuery, queryKeys, useQueryList, type StoredQuerySummary } from '../api/queries'
import { message } from '../pages/lists'
import '../pages/PagesPage.css'

/** Lists the stored queries and opens them in the query editor. */
export function QueriesPage() {
  const queryClient = useQueryClient()
  const queries = useQueryList()
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    document.title = 'Queries · Artefacts'
  }, [])

  const remove = async (query: StoredQuerySummary) => {
    if (!window.confirm(`Delete the query “${query.name}”?`)) return
    setError(null)
    try {
      await deleteQuery(query.id)
      await queryClient.invalidateQueries({ queryKey: queryKeys.all })
    } catch (e) {
      setError(message(e))
    }
  }

  return (
    <div className="pages">
      <div className="page-head">
        <div className="page-head__titles">
          <h1>Queries</h1>
          <p className="page-head__sub">
            What pages read: build a query from blocks or write it as text. System queries ship with the application; duplicate one to change a copy.
          </p>
        </div>
        <Link className="btn btn--primary" to="/queries/new">
          <Plus aria-hidden="true" /> New query
        </Link>
      </div>

      {error && (
        <p className="field__error" role="alert">
          {error}
        </p>
      )}

      {queries.isPending ? (
        <div className="data-state" aria-busy="true">
          Loading…
        </div>
      ) : queries.isError ? (
        <div className="data-state data-state--error" role="alert">
          Could not load queries: {queries.error.message}
        </div>
      ) : (
        <div className="table-wrap">
          <table className="table">
            <caption className="visually-hidden">Queries</caption>
            <thead>
              <tr>
                <th scope="col">Name</th>
                <th scope="col">Identifier</th>
                <th scope="col">Kind</th>
                <th scope="col">Changed</th>
                <th scope="col">
                  <span className="visually-hidden">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {queries.data.map((query) => (
                <tr key={query.id}>
                  <td data-label="Name">
                    <span className="cell-title">{query.name}</span>
                  </td>
                  <td data-label="Identifier">
                    <code>{query.id}</code>
                  </td>
                  <td data-label="Kind">{query.kind === 'system' ? 'System' : 'User'}</td>
                  <td data-label="Changed">{new Date(query.updatedAt).toLocaleDateString()}</td>
                  <td className="pages__actions">
                    <Link className="btn btn--sm" to={`/queries/${query.id}`}>
                      {query.kind === 'system' ? <Eye aria-hidden="true" /> : <Pencil aria-hidden="true" />} {query.kind === 'system' ? 'View' : 'Edit'}
                    </Link>
                    {query.kind === 'user' && (
                      <button type="button" className="btn btn--sm btn--danger" onClick={() => void remove(query)}>
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
