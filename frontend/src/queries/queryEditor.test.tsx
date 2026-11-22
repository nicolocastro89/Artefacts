import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { Route, Routes } from 'react-router'
import type { StoredQuery } from '../api/queries'
import rulesFile from '../test/builderRules.json'
import { mockFetch } from '../test/fetchMock'
import { renderWithProviders } from '../test/render'
import { QueryEditor } from './QueryEditor'

const system = (id: string) => readFileSync(join(process.cwd(), '..', 'backend/src/Artefacts.Infrastructure/Graph/Queries', `${id}.graphql`), 'utf8')

const stored = (id: string, document: string, kind: 'user' | 'system' = 'user'): StoredQuery => ({
  id,
  name: `Query ${id}`,
  kind,
  document,
  updatedAt: '2026-11-22T00:00:00Z',
})

const valid = (variables: Record<string, { type: string; required: boolean }> = {}) => ({
  description: { fields: { 'artefacts.total': 'number', 'artefacts.items[].title': 'text' }, variables },
  problems: [],
})

function renderEditor(route: string, routes: Record<string, () => unknown> = {}) {
  const mock = mockFetch({
    'GET /api/queries/builder-schema': () => rulesFile,
    'POST /api/queries/description': () => valid(),
    ...routes,
  })
  const view = renderWithProviders(
    <Routes>
      <Route path="/queries/new" element={<QueryEditor />} />
      <Route path="/queries/:id" element={<QueryEditor />} />
    </Routes>,
    { route },
  )
  return { ...mock, ...view }
}

const textOf = () => (screen.getByLabelText('Query text') as HTMLTextAreaElement).value

describe('opening queries in blocks', () => {
  it('shows a copy of the detail system query as blocks, with its nested fields and its identifier variable', async () => {
    renderEditor('/queries/q-detail', { 'GET /api/queries/q-detail': () => stored('q-detail', system('artefact-detail')) })

    expect(await screen.findByRole('heading', { name: /^artefact/ })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Blocks' })).toHaveAttribute('aria-selected', 'true')
    const acquisition = screen.getByText('acquisition').closest('details')!
    expect(within(acquisition).getByText('9 selected')).toBeInTheDocument()
    expect(screen.getByText('$id: UUID!')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /^language/ })).toBeInTheDocument()
  })

  it('shows the list system query with its options set from variables', async () => {
    renderEditor('/queries/q-list', { 'GET /api/queries/q-list': () => stored('q-list', system('artefact-list')) })

    expect(await screen.findByRole('heading', { name: /^artefacts/ })).toBeInTheDocument()
    expect(screen.getByLabelText('search: variable')).toHaveValue('search')
    expect(screen.getByLabelText('pageSize: variable')).toHaveValue('pageSize')
  })

  it('opens a system query read-only, with a duplicate action and no save', async () => {
    renderEditor('/queries/artefact-list', { 'GET /api/queries/artefact-list': () => stored('artefact-list', system('artefact-list'), 'system') })

    expect(await screen.findByRole('button', { name: 'Duplicate' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Save changes|Create query/ })).toBeNull()
    expect(screen.getByLabelText('Name')).toHaveAttribute('readonly')
    expect(screen.getByLabelText('search: how it is set')).toBeDisabled()
  })
})

describe('building with blocks', () => {
  it('shows a filter built from blocks in the text', async () => {
    const user = userEvent.setup()
    renderEditor('/queries/new')

    await user.click(await screen.findByRole('button', { name: /Condition/ }))
    await user.selectOptions(screen.getByLabelText('Field'), 'acquisitionMethod')
    fireEvent.change(screen.getByLabelText('Value'), { target: { value: 'Gift' } })
    await user.click(screen.getByRole('tab', { name: 'Text' }))

    expect(textOf()).toContain('where: { acquisitionMethod: { eq: "Gift" } }')
  })

  it('offers the operators of the field type', async () => {
    const user = userEvent.setup()
    renderEditor('/queries/new')

    await user.click(await screen.findByRole('button', { name: /Condition/ }))
    await user.selectOptions(screen.getByLabelText('Field'), 'imageCount')

    const operators = within(screen.getByLabelText('Operator')).getAllByRole('option').map((o) => o.textContent)
    expect(operators).toContain('is greater than')
    expect(operators).not.toContain('contains')
  })

  it('selects a nested field and shows it in the document', async () => {
    const user = userEvent.setup()
    renderEditor('/queries/new')

    const maker = await screen.findByRole('checkbox', { name: 'maker' })
    await user.click(maker)
    await user.click(screen.getByRole('tab', { name: 'Text' }))

    expect(textOf()).toMatch(/items \{\s+id\s+title\s+maker\s+\}/)
  })

  it('uses a variable for the search and declares it', async () => {
    const user = userEvent.setup()
    renderEditor('/queries/new')

    await user.selectOptions(await screen.findByLabelText('search: how it is set'), 'variable')
    await user.click(screen.getByRole('tab', { name: 'Text' }))

    expect(textOf()).toContain('query ($value: String)')
    expect(textOf()).toContain('search: $value')
  })

  it('does not accept a page size above the maximum', async () => {
    const user = userEvent.setup()
    renderEditor('/queries/new')

    await user.selectOptions(await screen.findByLabelText('pageSize: how it is set'), 'value')
    fireEvent.change(screen.getByRole('textbox', { name: 'pageSize' }), { target: { value: '500' } })

    expect(await screen.findByText('The most is 100.')).toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'Text' }))
    expect(textOf()).toContain('pageSize: 0')
  })

  it('refuses to remove a variable that is in use, naming where', async () => {
    const user = userEvent.setup()
    renderEditor('/queries/q1', { 'GET /api/queries/q1': () => stored('q1', 'query($s: String) { artefacts(search: $s) { total } }') })

    await user.click(await screen.findByRole('button', { name: 'Remove $s' }))

    expect(await screen.findByText('$s is used by artefacts: search.')).toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'Text' }))
    expect(textOf()).toContain('$s')
  })
})

describe('text the blocks cannot show', () => {
  it('disables blocks, names the alias and leaves the text unchanged', async () => {
    const document = '{ artefacts { items { name: title } } }'
    renderEditor('/queries/q2', { 'GET /api/queries/q2': () => stored('q2', document) })

    const status = await screen.findByRole('status')

    expect(status).toHaveTextContent('alias "name"')
    expect(screen.getByRole('tab', { name: 'Blocks' })).toBeDisabled()
    expect(textOf()).toBe(document)
  })
})

describe('comments', () => {
  const commented = '# name: mine\nquery { artefacts { total items { title } } }'

  // The `id` of the listed artefacts themselves, not of the linked records nested under `items`.
  const itemsId = async () => {
    const items = (await screen.findByText('items')).closest('details')!
    const own = items.querySelector(':scope > ul')!
    return within(items).getAllByRole('checkbox', { name: 'id' }).find((box) => box.closest('ul') === own)!
  }

  it('keeps the document when the user does not accept losing the comment', async () => {
    const user = userEvent.setup()
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    renderEditor('/queries/q3', { 'GET /api/queries/q3': () => stored('q3', commented) })

    await user.click(await itemsId())
    await user.click(screen.getByRole('tab', { name: 'Text' }))

    expect(confirm).toHaveBeenCalledWith(expect.stringContaining('comments'))
    expect(textOf()).toBe(commented)
    confirm.mockRestore()
  })

  it('applies the change and drops the comment once the user accepts', async () => {
    const user = userEvent.setup()
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true)
    renderEditor('/queries/q3', { 'GET /api/queries/q3': () => stored('q3', commented) })

    await user.click(await itemsId())
    await user.click(screen.getByRole('tab', { name: 'Text' }))

    expect(textOf()).not.toContain('# name')
    expect(textOf()).toMatch(/items \{\s+title\s+id\s+\}/)
    confirm.mockRestore()
  })
})

describe('run panel', () => {
  it('runs the unsaved document with a value for its variable and shows the result and the paths', async () => {
    const user = userEvent.setup()
    const { calls } = renderEditor('/queries/q4', {
      'GET /api/queries/q4': () => stored('q4', 'query($search: String) { artefacts(search: $search) { total items { title } } }'),
      'POST /api/queries/description': () => valid({ search: { type: 'text', required: false } }),
      'POST /graphql': () => ({ data: { artefacts: { total: 1, items: [{ title: 'Lemons' }] } } }),
    })

    await user.type(await screen.findByRole('textbox', { name: /^\$search/ }), 'lemons')
    await user.click(screen.getByRole('button', { name: 'Run' }))

    expect(await screen.findByLabelText('Result')).toHaveTextContent('Lemons')
    const run = calls.find((c) => c.url.pathname === '/graphql')!.body as { query: string; variables: Record<string, unknown> }
    expect(run.variables).toEqual({ search: 'lemons' })
    expect(run.query).toContain('artefacts(search: $search)')
    expect(screen.getByText('artefacts.items[].title')).toBeInTheDocument()
    expect(calls.some((c) => c.method === 'POST' && c.url.pathname === '/api/queries')).toBe(false)
  })

  it('shows a server error with its line and column', async () => {
    const user = userEvent.setup()
    renderEditor('/queries/q5', {
      'GET /api/queries/q5': () => stored('q5', '{ artefacts { total } }'),
      'POST /graphql': () => ({ errors: [{ message: 'The field `nonsense` does not exist.', locations: [{ line: 3, column: 5 }] }] }),
    })

    await user.click(await screen.findByRole('button', { name: 'Run' }))

    expect(await screen.findByText(/The field `nonsense` does not exist\. \(line 3, column 5\)/)).toBeInTheDocument()
  })

  it('shows the problems found in the document, with their position, and does not run it', async () => {
    renderEditor('/queries/q6', {
      'GET /api/queries/q6': () => stored('q6', '{ artefacts { nonsense } }'),
      'POST /api/queries/description': () => ({ description: null, problems: [{ message: 'The field `nonsense` does not exist.', line: 1, column: 15 }] }),
    })

    const problems = await screen.findByRole('list', { name: 'Problems found in the query' })

    expect(problems).toHaveTextContent('(line 1, column 15)')
    await waitFor(() => expect(screen.getByRole('button', { name: 'Run' })).toBeDisabled())
  })
})
