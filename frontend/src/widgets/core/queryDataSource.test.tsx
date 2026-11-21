import { screen } from '@testing-library/react'
import { mockFetch } from '../../test/fetchMock'
import { renderWithProviders } from '../../test/render'
import { coerceVariables, type QueryDescription } from './dataSources'
import type { PageDefinition } from './page'
import { PageRenderer } from './PageRenderer'

const description: QueryDescription = {
  fields: { 'artefact.title': 'text', 'artefact.maker': 'text', total: 'number' },
  variables: { id: { type: 'reference', required: true }, page: { type: 'number', required: false }, all: { type: 'boolean', required: false } },
}

const titlePage = (binding: string, require?: string): PageDefinition => ({
  id: 'p',
  title: 'P',
  root: {
    type: 'container',
    id: 'c',
    dataSource: { kind: 'query', queryId: 'artefact-detail', variables: { id: { from: 'route', name: 'id' }, page: { from: 'search', name: 'page' } }, require },
    children: [{ type: 'widget', id: 'h', widgetId: 'heading', bindings: { text: binding } }],
  },
})

const graphql = (respond: (body: { id: string; variables: Record<string, unknown> }) => unknown) => ({
  'GET /api/queries/artefact-detail/description': () => description,
  'POST /graphql': (_url: URL, init?: RequestInit) => respond(JSON.parse(String(init?.body))),
})

describe('coercion of query variables', () => {
  it('parses numbers and booleans to the declared type and keeps other texts', () => {
    expect(coerceVariables({ page: '2', all: 'true', id: 'a1' }, description.variables)).toEqual({ page: 2, all: true, id: 'a1' })
  })

  it('sends a text that does not fit its type as it is, so the server names the variable', () => {
    expect(coerceVariables({ page: 'two', all: 'maybe' }, description.variables)).toEqual({ page: 'two', all: 'maybe' })
  })
})

describe('query data source', () => {
  it('runs the stored query by identifier with variables from the route and the URL, typed as declared', async () => {
    const { calls } = mockFetch(graphql(() => ({ data: { artefact: { title: 'Piatto' } } })))

    renderWithProviders(<PageRenderer page={titlePage('artefact.title')} route={{ id: 'a1' }} search={new URLSearchParams('page=2')} />)

    expect(await screen.findByRole('heading', { name: 'Piatto' })).toBeInTheDocument()
    const post = calls.find((c) => c.method === 'POST' && c.url.pathname === '/graphql')!
    expect(post.body).toEqual({ id: 'artefact-detail', variables: { id: 'a1', page: 2 } })
  })

  it('shows a configuration error naming a path the query does not return', async () => {
    mockFetch(graphql(() => ({ data: { artefact: { title: 'Piatto' } } })))

    renderWithProviders(<PageRenderer page={titlePage('artefact.valuation')} route={{ id: 'a1' }} />)

    expect(await screen.findByText(/"artefact.valuation" is not a field of this data source/)).toBeInTheDocument()
  })

  it('shows GraphQL errors as an error state', async () => {
    mockFetch(graphql(() => ({ errors: [{ message: "Variable 'page': Int cannot coerce the value." }] })))

    renderWithProviders(<PageRenderer page={titlePage('artefact.title')} route={{ id: 'a1' }} search={new URLSearchParams('page=two')} />)

    expect(await screen.findByRole('alert')).toHaveTextContent("Variable 'page': Int cannot coerce the value.")
  })

  it('shows the not-found state when the required path is null', async () => {
    mockFetch(graphql(() => ({ data: { artefact: null } })))

    renderWithProviders(<PageRenderer page={titlePage('artefact.title', 'artefact')} route={{ id: 'gone' }} />)

    expect(await screen.findByRole('alert')).toHaveTextContent('This record does not exist')
  })

  it('shows the data when the required path is present', async () => {
    mockFetch(graphql(() => ({ data: { artefact: { title: 'Piatto' } } })))

    renderWithProviders(<PageRenderer page={titlePage('artefact.title', 'artefact')} route={{ id: 'a1' }} />)

    expect(await screen.findByRole('heading', { name: 'Piatto' })).toBeInTheDocument()
  })
})
