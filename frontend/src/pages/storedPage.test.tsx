import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router'
import { AppShell } from '../app/AppShell'
import type { StoredPage } from '../api/pages'
import { mockFetch, problem } from '../test/fetchMock'
import { artefactDetailRoutes, sentVariables } from '../test/queryFixtures'
import { renderWithProviders } from '../test/render'
import { CustomPageRoute } from './PageRoute'

function stored(slug: string, root: unknown): StoredPage {
  return { id: `p-${slug}`, slug, name: `Page ${slug}`, kind: 'user', showInNavigation: false, navigationLabel: null, definition: { root } as StoredPage['definition'], updatedAt: '2026-11-21T00:00:00Z' }
}

const staticPage = (text: string) => ({
  type: 'container',
  id: 'c',
  dataSource: { kind: 'static', data: { title: text } },
  children: [{ type: 'widget', id: 'h', widgetId: 'heading', bindings: { text: 'title' } }],
})

function renderCustom(route: string) {
  return renderWithProviders(
    <Routes>
      <Route path="/p/:slug" element={<CustomPageRoute />} />
    </Routes>,
    { route },
  )
}

describe('custom pages', () => {
  it('renders a stored page at /p/:slug with the same widgets as a built-in page', async () => {
    mockFetch({ 'GET /api/pages/by-slug/gifts': () => stored('gifts', staticPage('Gifts to the museum')) })

    renderCustom('/p/gifts')

    expect(await screen.findByRole('heading', { name: 'Gifts to the museum' })).toBeInTheDocument()
    expect(document.title).toBe('Page gifts · Artefacts')
  })

  it('shows a not-found state for a slug that matches no page', async () => {
    mockFetch({ 'GET /api/pages/by-slug/nothing-here': () => problem(404, { title: 'Not found' }) })

    renderCustom('/p/nothing-here')

    expect(await screen.findByText('This page does not exist.')).toBeInTheDocument()
  })

  it('shows an error with a retry when the page cannot be loaded, and renders after retrying', async () => {
    const user = userEvent.setup()
    let attempts = 0
    mockFetch({
      'GET /api/pages/by-slug/flaky': () => (++attempts === 1 ? problem(500, { title: 'Server error' }) : stored('flaky', staticPage('Back again'))),
    })

    renderCustom('/p/flaky')
    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load this page')
    await user.click(screen.getByRole('button', { name: 'Retry' }))

    expect(await screen.findByRole('heading', { name: 'Back again' })).toBeInTheDocument()
  })

  it('runs a stored query with the record id taken from the URL', async () => {
    const { calls } = mockFetch({
      ...artefactDetailRoutes({ id: 'a7', title: 'Bowl', inventoryNumber: 'COL-7', maker: null }),
      'GET /api/pages/by-slug/record': () =>
        stored('record', {
          type: 'container',
          id: 'artefact',
          dataSource: { kind: 'query', queryId: 'artefact-detail', variables: { id: { from: 'search', name: 'id' } }, require: 'artefact' },
          children: [{ type: 'widget', id: 'h', widgetId: 'heading', bindings: { text: 'artefact.title' } }],
        }),
    })

    renderCustom('/p/record?id=a7')

    expect(await screen.findByRole('heading', { name: 'Bowl' })).toBeInTheDocument()
    expect(sentVariables(calls)).toEqual([expect.objectContaining({ id: 'a7' })])
  })
})

describe('navigation', () => {
  it('lists the pages shown in navigation with their labels', async () => {
    mockFetch({
      'POST /graphql': () => ({ data: { artefacts: { total: 3 } } }),
      'GET /api/regions': () => [],
      'GET /api/object-types': () => [],
      'GET /api/pages': () => [{ id: 'p-gifts', slug: 'gifts', name: 'Gifts page', kind: 'user', showInNavigation: true, navigationLabel: 'Gifts', updatedAt: '2026-11-21T00:00:00Z' }],
    })

    renderWithProviders(
      <Routes>
        <Route path="*" element={<AppShell />} />
      </Routes>,
    )

    expect(await screen.findByRole('link', { name: 'Gifts' })).toHaveAttribute('href', '/p/gifts')
  })
})
