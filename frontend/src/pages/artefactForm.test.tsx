import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router'
import { mockFetch, problem } from '../test/fetchMock'
import { renderWithProviders } from '../test/render'
import { artefactNewPage } from './definitions/artefactEdit'
import { PageRoute } from './PageRoute'

/** Mirrors the server's interpretation for the phrases used here (the real parser is tested in the backend). */
function parseDate(text: string) {
  switch (text) {
    case 'late 17th century':
      return { display: text, earliestYear: 1667, latestYear: 1700, precision: 'century' }
    case '1780s':
      return { display: text, earliestYear: 1780, latestYear: 1789, precision: 'decade' }
    default:
      return problem(400, { title: 'Validation failed', errors: { text: ['This date could not be interpreted.'] } })
  }
}

function renderNew(routes: Parameters<typeof mockFetch>[0] = {}) {
  const mock = mockFetch({
    'POST /api/historical-dates/parse': (_url, init) => parseDate(JSON.parse(String(init?.body)).text),
    ...routes,
  })
  renderWithProviders(
    <Routes>
      <Route path="/artefacts/new" element={<PageRoute page={artefactNewPage} />} />
      <Route path="/artefacts/:id" element={<p>Saved record</p>} />
    </Routes>,
    { route: '/artefacts/new' },
  )
  return mock
}

/** The `.field` wrapper around a control, where its own errors are rendered. */
const fieldOf = (control: HTMLElement) => control.closest('.field') as HTMLElement

describe('artefact form', () => {
  it('shows how a historical date is interpreted before saving', async () => {
    const user = userEvent.setup()
    renderNew()

    await user.type(await screen.findByRole('textbox', { name: 'Date' }), 'late 17th century')

    const feedback = await screen.findByText(/Interpreted as/)
    expect(feedback).toHaveTextContent('Interpreted as 1667–1700 (century)')
  })

  it('reports a date it cannot interpret', async () => {
    const user = userEvent.setup()
    renderNew()

    await user.type(await screen.findByRole('textbox', { name: 'Date' }), 'sometime')

    expect(await screen.findByText('This date could not be interpreted.')).toBeInTheDocument()
  })

  it('adds a region that does not exist yet from the provenance picker and selects it', async () => {
    const user = userEvent.setup()
    const { calls } = renderNew({
      'GET /api/regions': () => [],
      'POST /api/regions': () => new Response(JSON.stringify({ id: 'r9', name: 'Zeeland', parentId: null, parentName: null }), { status: 201, headers: { 'Content-Type': 'application/json' } }),
      'POST /api/artefacts': () => new Response(JSON.stringify({ id: 'new-1' }), { status: 201, headers: { 'Content-Type': 'application/json' } }),
    })

    await user.type(await screen.findByRole('textbox', { name: /Inventory number/ }), 'INV-1')
    await user.type(screen.getByRole('textbox', { name: /Title/ }), 'Seascape')
    await user.click(screen.getByRole('button', { name: 'Add provenance step' }))
    const picker = screen.getByRole('combobox', { name: 'Region' })
    await user.type(picker, 'Zeeland')
    await user.click(await screen.findByRole('option', { name: /Add “Zeeland”/ }))

    await waitFor(() => expect(picker).toHaveValue('Zeeland'))
    expect(calls.find((c) => c.method === 'POST' && c.url.pathname === '/api/regions')?.body).toEqual({ name: 'Zeeland' })

    await user.type(screen.getByRole('textbox', { name: 'Period' }), '1780s')
    await user.click(screen.getByRole('button', { name: 'Create artefact' }))

    expect(await screen.findByText('Saved record')).toBeInTheDocument()
    const saved = calls.find((c) => c.method === 'POST' && c.url.pathname === '/api/artefacts')!.body as { provenance: { regionId: string }[] }
    expect(saved.provenance).toEqual([expect.objectContaining({ regionId: 'r9', period: expect.objectContaining({ text: '1780s' }) })])
  })

  it('shows a duplicate inventory number error next to the inventory number field', async () => {
    const user = userEvent.setup()
    renderNew({
      'POST /api/artefacts': () =>
        problem(400, { title: 'One or more validation errors occurred.', errors: { inventoryNumber: ['Inventory number "INV-1" is already used.'] } }),
    })

    const inventory = await screen.findByRole('textbox', { name: /Inventory number/ })
    await user.type(inventory, 'INV-1')
    await user.type(screen.getByRole('textbox', { name: /Title/ }), 'Seascape')
    await user.click(screen.getByRole('button', { name: 'Create artefact' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Some fields need attention')
    expect(fieldOf(inventory)).toHaveTextContent('Inventory number "INV-1" is already used.')
    expect(inventory).toHaveAttribute('aria-invalid', 'true')
    expect(fieldOf(screen.getByRole('textbox', { name: /Title/ }))).not.toHaveTextContent('already used')
  })
})
