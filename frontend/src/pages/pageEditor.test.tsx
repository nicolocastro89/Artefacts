import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router'
import type { StoredPage } from '../api/pages'
import { mockFetch, problem } from '../test/fetchMock'
import { artefactListRoutes } from '../test/queryFixtures'
import { renderWithProviders } from '../test/render'
import { PageEditor } from './PageEditor'

const listPage = (bindingPath: string) =>
  JSON.stringify({
    root: {
      type: 'container',
      id: 'c',
      dataSource: { kind: 'query', queryId: 'artefact-list' },
      children: [{ type: 'widget', id: 'w', widgetId: 'heading', bindings: { text: bindingPath } }],
    },
  })

const saved = (overrides: Partial<StoredPage> = {}): StoredPage => ({
  id: 'p-1',
  slug: 'gifts',
  name: 'Gifts',
  kind: 'user',
  showInNavigation: false,
  navigationLabel: null,
  definition: JSON.parse(listPage('artefacts.total')) as StoredPage['definition'],
  updatedAt: '2026-11-21T00:00:00Z',
  ...overrides,
})

const queryRoutes = artefactListRoutes(() => ({ artefacts: { items: [], total: 4, page: 1, pageSize: 25, groups: [] } }))

function renderEditor(route: string) {
  return renderWithProviders(
    <Routes>
      <Route path="/pages/new" element={<PageEditor />} />
      <Route path="/pages/:id" element={<PageEditor />} />
    </Routes>,
    { route },
  )
}

function typeDefinition(text: string) {
  fireEvent.change(screen.getByLabelText('Definition'), { target: { value: text } })
}

describe('page editor', () => {
  it('names the path of a binding problem in the preview and still lets the page be saved', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({
      ...queryRoutes,
      'POST /api/pages': () => saved(),
      'GET /api/pages/p-1': () => saved(),
    })
    renderEditor('/pages/new')

    typeDefinition(listPage('artefacts.valuation'))
    const problems = await screen.findByRole('list', { name: 'Problems found in the definition' })
    expect(await within(problems).findByText('root.children[0].bindings.text')).toBeInTheDocument()
    expect(problems).toHaveTextContent('"artefacts.valuation" is not a field of this data source')

    await user.type(screen.getByLabelText('Name'), 'Gifts')
    await user.type(screen.getByLabelText('Slug'), 'gifts')
    await user.click(screen.getByRole('button', { name: 'Create page' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST' && c.url.pathname === '/api/pages')).toBe(true))
    const body = calls.find((c) => c.method === 'POST' && c.url.pathname === '/api/pages')?.body as { name: string; slug: string; definition: { root: { id: string } } }
    expect(body).toMatchObject({ name: 'Gifts', slug: 'gifts', definition: { root: { id: 'c' } } })
  })

  it('shows the servers error with the path in the definition when saving is rejected', async () => {
    const user = userEvent.setup()
    mockFetch({
      ...queryRoutes,
      'POST /api/pages': () => problem(400, { errors: { definition: ["root.dataSource.queryId: The stored query 'no-such-query' does not exist."] } }),
    })
    renderEditor('/pages/new')

    await user.type(screen.getByLabelText('Name'), 'Gifts')
    await user.type(screen.getByLabelText('Slug'), 'gifts')
    await user.click(screen.getByRole('button', { name: 'Create page' }))

    expect(await screen.findByText(/root\.dataSource\.queryId: The stored query 'no-such-query' does not exist/)).toBeInTheDocument()
  })

  it('reports a definition that is not JSON and blocks saving', async () => {
    mockFetch(queryRoutes)
    renderEditor('/pages/new')

    typeDefinition('{ "root": ')

    expect(await screen.findByText(/Not valid JSON/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Create page' })).toBeDisabled()
  })

  it('opens a system page read-only and duplicates it into a page that can be edited', async () => {
    const user = userEvent.setup()
    const system = saved({ id: 'artefact-list', slug: 'artefact-list', name: 'Artefacts', kind: 'system' })
    const { calls } = mockFetch({
      ...queryRoutes,
      'GET /api/pages/artefact-list': () => system,
      'POST /api/pages/artefact-list/duplicate': () => saved({ id: 'p-copy', slug: 'my-list', name: 'My list' }),
      'GET /api/pages/p-copy': () => saved({ id: 'p-copy', slug: 'my-list', name: 'My list' }),
    })
    renderEditor('/pages/artefact-list')

    const definition = await screen.findByLabelText('Definition')
    expect(definition).toHaveAttribute('readonly')
    expect(screen.queryByRole('button', { name: /Save changes|Create page/ })).toBeNull()

    await user.click(screen.getByRole('button', { name: 'Duplicate' }))
    await user.clear(screen.getByLabelText('Name of the copy'))
    await user.type(screen.getByLabelText('Name of the copy'), 'My list')
    await user.clear(screen.getByLabelText('Slug of the copy'))
    await user.type(screen.getByLabelText('Slug of the copy'), 'my-list')
    await user.click(screen.getByRole('button', { name: 'Create copy' }))

    expect(await screen.findByRole('heading', { name: 'My list' })).toBeInTheDocument()
    expect(calls.find((c) => c.url.pathname === '/api/pages/artefact-list/duplicate')?.body).toEqual({ name: 'My list', slug: 'my-list' })
    expect(screen.getByRole('button', { name: 'Save changes' })).toBeInTheDocument()
  })
})
