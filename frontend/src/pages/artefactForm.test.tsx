import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router'
import { languagesRoute, mockFetch, problem, unitsRoute } from '../test/fetchMock'
import { renderWithProviders } from '../test/render'
import { artefactEditPage, artefactNewPage } from './definitions/artefactEdit'
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
    ...languagesRoute,
    ...unitsRoute,
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
      'POST /api/regions': () => new Response(JSON.stringify({ id: 'r9', name: 'Zeeland', nameLanguage: 'en', names: { en: 'Zeeland' }, parentId: null, parentName: null }), { status: 201, headers: { 'Content-Type': 'application/json' } }),
      'POST /api/artefacts': () => new Response(JSON.stringify({ id: 'new-1' }), { status: 201, headers: { 'Content-Type': 'application/json' } }),
    })

    await user.type(await screen.findByRole('textbox', { name: /Inventory number/ }), 'INV-1')
    await user.type(screen.getByRole('textbox', { name: /Title/ }), 'Seascape')
    await user.click(screen.getByRole('button', { name: 'Add provenance step' }))
    const picker = screen.getByRole('combobox', { name: 'Region' })
    await user.type(picker, 'Zeeland')
    await user.click(await screen.findByRole('option', { name: /Add “Zeeland”/ }))

    await waitFor(() => expect(picker).toHaveValue('Zeeland'))
    expect(calls.find((c) => c.method === 'POST' && c.url.pathname === '/api/regions')?.body).toEqual({ names: { en: 'Zeeland' } })

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

describe('artefact form measurements', () => {
  it('records a height in the default centimetres and a width in the unit chosen', async () => {
    const user = userEvent.setup()
    const { calls } = renderNew({
      'POST /api/artefacts': () => new Response(JSON.stringify({ id: 'new-1' }), { status: 201, headers: { 'Content-Type': 'application/json' } }),
    })

    await user.type(await screen.findByRole('textbox', { name: /Inventory number/ }), 'INV-7')
    await user.type(screen.getByRole('textbox', { name: /Title/ }), 'Coppa')
    await user.type(screen.getByRole('textbox', { name: 'Dimensions' }), 'diam 17,6 x h 7')
    await user.type(screen.getByRole('textbox', { name: /^Height/ }), '7')
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Height unit' })).toHaveValue('u-cm'))
    await user.selectOptions(screen.getByRole('combobox', { name: 'Diameter unit' }), 'mm')
    await user.type(screen.getByRole('textbox', { name: /^Diameter/ }), '176')
    // A further measurement takes the unit used last.
    await user.type(screen.getByRole('textbox', { name: /^Width/ }), '9')
    expect(screen.getByRole('combobox', { name: 'Width unit' })).toHaveValue('u-mm')
    await user.clear(screen.getByRole('textbox', { name: /^Width/ }))
    await user.click(screen.getByRole('button', { name: 'Create artefact' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST' && c.url.pathname === '/api/artefacts')).toBe(true))
    const body = calls.find((c) => c.method === 'POST' && c.url.pathname === '/api/artefacts')!.body as Record<string, unknown>
    expect(body.dimensions).toBe('diam 17,6 x h 7')
    expect(body.height).toEqual({ value: 7, unitId: 'u-cm' })
    expect(body.diameter).toEqual({ value: 176, unitId: 'u-mm' })
    expect(body.width).toBeNull()
    expect(body.length).toBeNull()
  })

  it('shows a measurement error next to that measurement', async () => {
    const user = userEvent.setup()
    renderNew({
      'POST /api/artefacts': () => problem(400, { title: 'One or more validation errors occurred.', errors: { 'height.value': ['Must be greater than zero.'] } }),
    })

    await user.type(await screen.findByRole('textbox', { name: /Inventory number/ }), 'INV-8')
    await user.type(screen.getByRole('textbox', { name: /Title/ }), 'Coppa')
    const height = screen.getByRole('textbox', { name: /^Height/ })
    await user.type(height, '0')
    await user.click(screen.getByRole('button', { name: 'Create artefact' }))

    expect(await screen.findByText('Must be greater than zero.')).toBeInTheDocument()
    expect(fieldOf(height)).toHaveTextContent('Must be greater than zero.')
  })

  it('removes a measurement cleared while editing', async () => {
    const user = userEvent.setup()
    const detail = {
      id: 'a1', inventoryNumber: 'INV-1', title: 'Dipinto', maker: null, creationDate: null, medium: null, dimensions: '82 × 64 cm',
      description: null, descriptionLanguage: null, descriptions: {}, language: 'en', acquisition: null, provenance: [], primaryImage: null, images: [],
      objectType: null, collectionGroup: null, placeOfProduction: null, dynasty: null, exhibitions: [], catalogueEntry: null, catalogueEntryLanguage: null,
      catalogueEntries: {}, holder: null, storageLocation: null, valuation: null,
      height: { value: 82, inMetres: 0.82, unit: { id: 'u-cm', name: 'Centimetre', abbreviation: 'cm', factor: 0.01 } },
      width: { value: 64, inMetres: 0.64, unit: { id: 'u-cm', name: 'Centimetre', abbreviation: 'cm', factor: 0.01 } },
      length: null, diameter: null,
    }
    const { calls } = mockFetch({ ...languagesRoute, ...unitsRoute, 'GET /api/artefacts/a1': () => detail, 'PUT /api/artefacts/a1': () => detail })
    renderWithProviders(
      <Routes>
        <Route path="/artefacts/:id/edit" element={<PageRoute page={artefactEditPage} />} />
        <Route path="/artefacts/:id" element={<p>Saved record</p>} />
      </Routes>,
      { route: '/artefacts/a1/edit' },
    )

    const width = await screen.findByRole('textbox', { name: /^Width/ })
    expect(width).toHaveValue('64')
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Width unit' })).toHaveValue('u-cm'))
    await user.clear(width)
    await user.click(screen.getByRole('button', { name: /Save/ }))

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true))
    const body = calls.find((c) => c.method === 'PUT')!.body as Record<string, unknown>
    expect(body.width).toBeNull()
    expect(body.height).toEqual({ value: 82, unitId: 'u-cm' })
  })
})
