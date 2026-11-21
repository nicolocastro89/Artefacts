import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useEffect } from 'react'
import { Route, Routes, useLocation } from 'react-router'
import { languagesRoute, mockFetch, problem } from '../test/fetchMock'
import { renderWithProviders } from '../test/render'
import { artefactDetailPage } from './definitions/artefactDetail'
import { artefactNewPage } from './definitions/artefactEdit'
import { artefactListPage } from './definitions/artefactList'
import { ObjectTypesPage } from './ObjectTypesPage'
import { PageRoute } from './PageRoute'

const row = (id: string, title: string, groupKey: string | null, groupLabel: string | null) => ({
  id,
  inventoryNumber: `CER-${id}`,
  title,
  maker: null,
  creationDate: null,
  medium: null,
  primaryImage: null,
  imageCount: 0,
  objectType: groupLabel,
  collectionGroup: null,
  placeOfProduction: null,
  dynasty: null,
  groupKey,
  groupLabel,
})

let lastLocation = ''
function LocationSpy() {
  const location = useLocation()
  useEffect(() => {
    lastLocation = location.pathname + location.search
  }, [location])
  return null
}

describe('grouped artefact list', () => {
  function renderList(route: string) {
    return renderWithProviders(
      <>
        <Routes>
          <Route path="/artefacts" element={<PageRoute page={artefactListPage} />} />
        </Routes>
        <LocationSpy />
      </>,
      { route },
    )
  }

  const groups = [
    { key: 't1', label: 'Ceramics', count: 30 },
    { key: 't2', label: 'Carpets', count: 2 },
    { key: null, label: null, count: 1 },
  ]

  it('shows a heading with label and total count before each group, and the no-value group last', async () => {
    const { calls } = mockFetch({
      'GET /api/artefacts': () => ({
        items: [row('1', 'Bowl', 't1', 'Ceramics'), row('2', 'Dish', 't1', 'Ceramics'), row('3', 'Rug', 't2', 'Carpets'), row('4', 'Mask', null, null)],
        total: 33,
        page: 1,
        pageSize: 25,
        groups,
      }),
    })
    renderList('/artefacts?group=objectType')

    const table = await screen.findByRole('table', { name: 'Artefacts' })
    const headings = within(table).getAllByRole('columnheader').filter((h) => h.getAttribute('scope') === 'colgroup')
    expect(headings.map((h) => h.textContent)).toEqual(['Ceramics30', 'Carpets2', 'No object type1'])
    expect(calls.find((c) => c.url.pathname === '/api/artefacts')!.url.searchParams.get('group')).toBe('objectType')
  })

  it('repeats the heading of a group that continues from the previous page', async () => {
    mockFetch({
      'GET /api/artefacts': () => ({ items: [row('26', 'Vase', 't1', 'Ceramics')], total: 33, page: 2, pageSize: 25, groups }),
    })
    renderList('/artefacts?group=objectType&page=2')

    const table = await screen.findByRole('table', { name: 'Artefacts' })
    expect(within(table).getByRole('columnheader', { name: 'Ceramics 30' })).toBeInTheDocument()
  })

  it('records the grouping in the URL and starts again from page 1', async () => {
    const user = userEvent.setup()
    mockFetch({ 'GET /api/artefacts': () => ({ items: [row('1', 'Bowl', null, null)], total: 1, page: 1, pageSize: 25, groups: [] }) })
    renderList('/artefacts?page=2')

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Group by' }), 'Century')

    await waitFor(() => expect(lastLocation).toBe('/artefacts?group=century'))
  })

  it('shows group headings in the grid too', async () => {
    mockFetch({
      'GET /api/artefacts': () => ({ items: [row('1', 'Bowl', 't1', 'Ceramics'), row('3', 'Rug', 't2', 'Carpets')], total: 2, page: 1, pageSize: 25, groups }),
    })
    renderList('/artefacts?group=objectType&view=grid')

    const grid = await screen.findByRole('list', { name: 'Records' })
    expect(within(grid).getByText('Ceramics')).toHaveClass('group-heading__label')
    expect(within(grid).getByText('Carpets')).toHaveClass('group-heading__label')
  })
})

describe('artefact detail with catalogue fields', () => {
  it('shows classification, origin, holding, exhibitions in order and the catalogue entry with its line breaks', async () => {
    mockFetch({
      'GET /api/artefacts/a1': () => ({
        id: 'a1',
        inventoryNumber: 'CER-01-02',
        title: 'Piatto Celadon',
        maker: null,
        creationDate: null,
        medium: null,
        dimensions: null,
        description: null,
        acquisition: { method: 'purchase', date: '1990-06-12', source: { id: 'p1', name: "Sotheby's London", kind: 'organisation' }, price: { amount: 7865, currency: 'GBP' }, lot: '170', note: 'acquistato fuori asta' },
        provenance: [],
        primaryImage: null,
        images: [],
        objectType: { id: 't1', name: 'Ceramics' },
        collectionGroup: { id: 'g1', name: 'CINA' },
        placeOfProduction: { text: 'Cina', region: { id: 'r1', name: 'China' } },
        dynasty: { id: 'd1', name: 'Sung' },
        exhibitions: ['Arte Ottomana, Genova, 1999', 'Persian Art, New York, 1940'],
        catalogueEntryEn: 'A JUNYAO DISH\nJIN DYNASTY (1115-1234)',
        holder: null,
        storageLocation: { id: 'l1', name: 'MI F3' },
        valuation: null,
      }),
    })
    renderWithProviders(
      <Routes>
        <Route path="/artefacts/:id" element={<PageRoute page={artefactDetailPage} />} />
      </Routes>,
      { route: '/artefacts/a1' },
    )

    await screen.findByRole('heading', { name: 'Piatto Celadon' })
    const classification = screen.getByRole('heading', { name: 'Classification and origin' }).closest('section') as HTMLElement
    expect(within(classification).getByText('Ceramics')).toBeInTheDocument()
    expect(within(classification).getByText('CINA')).toBeInTheDocument()
    expect(within(classification).getByText('China')).toBeInTheDocument()
    expect(within(classification).getByText('Sung')).toBeInTheDocument()

    const holding = screen.getByRole('heading', { name: 'Holding' }).closest('section') as HTMLElement
    expect(within(holding).getByText('MI F3')).toBeInTheDocument()
    expect(within(holding).queryByText('Holder')).toBeNull()
    expect(within(holding).queryByText('Valuation')).toBeNull()

    expect(screen.getByText("Sotheby's London")).toBeInTheDocument()
    expect(screen.getByText('170')).toBeInTheDocument()
    expect(screen.getByText('acquistato fuori asta')).toBeInTheDocument()

    const exhibitions = screen.getByRole('heading', { name: 'Exhibitions' }).closest('section') as HTMLElement
    expect(within(exhibitions).getAllByRole('listitem').map((li) => li.textContent)).toEqual(['Arte Ottomana, Genova, 1999', 'Persian Art, New York, 1940'])

    const entry = screen.getByText(/A JUNYAO DISH/)
    expect(entry.textContent).toBe('A JUNYAO DISH\nJIN DYNASTY (1115-1234)')
  })
})

describe('artefact form with catalogue fields', () => {
  function renderNew(routes: Parameters<typeof mockFetch>[0]) {
    const mock = mockFetch(routes)
    renderWithProviders(
      <Routes>
        <Route path="/artefacts/new" element={<PageRoute page={artefactNewPage} />} />
        <Route path="/artefacts/:id" element={<p>Saved record</p>} />
      </Routes>,
      { route: '/artefacts/new' },
    )
    return mock
  }

  it('adds an object type inline and saves it with the exhibitions in order', async () => {
    const user = userEvent.setup()
    const { calls } = renderNew({
      ...languagesRoute,
      'GET /api/object-types': () => [],
      'POST /api/object-types': () => ({ id: 't9', name: 'Metalwork', nameLanguage: 'en', names: { en: 'Metalwork' }, artefactCount: 0 }),
      'GET /api/lists/storage-locations': () => [{ id: 'l1', kind: 'storageLocation', name: 'cella 16', nameLanguage: null, names: null, artefactCount: 3 }],
      'POST /api/artefacts': () => ({ id: 'new1' }),
    })

    await user.type(await screen.findByRole('textbox', { name: 'Inventory number' }), 'MET-01-01')
    await user.type(screen.getByRole('textbox', { name: 'Title' }), 'Candeliere')
    await user.type(screen.getByRole('combobox', { name: 'Object type' }), 'Metalwork')
    await user.click(await screen.findByRole('option', { name: 'Add “Metalwork”' }))
    await user.click(screen.getByRole('button', { name: 'Add exhibition' }))
    await user.type(screen.getByRole('textbox', { name: 'Exhibition 1' }), 'Genova nel Medioevo')
    await user.click(screen.getByRole('button', { name: 'Add exhibition' }))
    await user.type(screen.getByRole('textbox', { name: 'Exhibition 2' }), 'Arte Ottomana')
    await user.click(screen.getByRole('button', { name: 'Move exhibition 2 up' }))
    await user.type(screen.getByRole('combobox', { name: 'Storage location' }), 'cella')
    await user.click(await screen.findByRole('option', { name: 'cella 16' }))
    await user.click(screen.getByRole('button', { name: 'Create artefact' }))

    expect(await screen.findByText('Saved record')).toBeInTheDocument()
    const body = calls.find((c) => c.method === 'POST' && c.url.pathname === '/api/artefacts')!.body as Record<string, unknown>
    expect(body.objectTypeId).toBe('t9')
    expect(body.exhibitions).toEqual(['Arte Ottomana', 'Genova nel Medioevo'])
    expect(body.storageLocationId).toBe('l1')
  })

  it('picks the source from the parties and creates a holder inline as a person', async () => {
    const user = userEvent.setup()
    const { calls } = renderNew({
      ...languagesRoute,
      'GET /api/parties': () => [{ id: 'p1', name: "Sotheby's", kind: 'organisation', sourceCount: 4, holderCount: 0 }],
      'POST /api/parties': () => ({ id: 'p2', name: 'Anna Rossi', kind: 'person', sourceCount: 0, holderCount: 0 }),
      'POST /api/artefacts': () => ({ id: 'new1' }),
    })

    await user.type(await screen.findByRole('textbox', { name: 'Inventory number' }), 'X-1')
    await user.type(screen.getByRole('textbox', { name: 'Title' }), 'Coppa')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Method' }), 'purchase')
    await user.type(screen.getByRole('combobox', { name: 'Source' }), 'Sothe')
    await user.click(await screen.findByRole('option', { name: /Sotheby's/ }))
    await user.type(screen.getByRole('combobox', { name: 'Holder' }), 'Anna Rossi')
    await user.click(await screen.findByRole('option', { name: 'Add “Anna Rossi” as a person' }))
    await user.click(screen.getByRole('button', { name: 'Create artefact' }))

    expect(await screen.findByText('Saved record')).toBeInTheDocument()
    const added = calls.find((c) => c.method === 'POST' && c.url.pathname === '/api/parties')!.body as Record<string, unknown>
    expect(added).toEqual({ name: 'Anna Rossi', kind: 'person' })
    const body = calls.find((c) => c.method === 'POST' && c.url.pathname === '/api/artefacts')!.body as Record<string, unknown>
    expect((body.acquisition as Record<string, unknown>).sourceId).toBe('p1')
    expect(body.holderId).toBe('p2')
  })

  it('shows server errors on the new fields', async () => {
    const user = userEvent.setup()
    renderNew({
      ...languagesRoute,
      'POST /api/artefacts': () =>
        problem(400, { title: 'Validation failed', errors: { 'valuation.currency': ['"XYZ" is not a recognised ISO 4217 currency code.'] } }),
    })

    await user.type(await screen.findByRole('textbox', { name: 'Inventory number' }), 'X-1')
    await user.type(screen.getByRole('textbox', { name: 'Title' }), 'T')
    await user.type(screen.getByRole('textbox', { name: /Valuation/ }), '100')
    await user.click(screen.getByRole('button', { name: 'Create artefact' }))

    const valuation = (await screen.findByText('"XYZ" is not a recognised ISO 4217 currency code.')).closest('.field') as HTMLElement
    expect(within(valuation).getByRole('textbox', { name: /Valuation/ })).toBeInTheDocument()
  })
})

describe('object types page', () => {
  it('lists usage counts and explains why an object type in use cannot be deleted', async () => {
    const user = userEvent.setup()
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    mockFetch({
      ...languagesRoute,
      'GET /api/object-types': () => [{ id: 't1', name: 'Ceramics', nameLanguage: 'en', names: { en: 'Ceramics', it: 'Ceramiche' }, artefactCount: 70 }],
      'DELETE /api/object-types/t1': () =>
        problem(409, { title: 'Conflict', detail: '"Ceramics" is the object type of artefacts and cannot be deleted.', code: 'object_type_in_use' }),
    })
    renderWithProviders(<ObjectTypesPage />, { route: '/reference/object-types' })

    const rowEl = (await screen.findByText('Ceramics')).closest('tr') as HTMLElement
    expect(within(rowEl).getByText('70')).toBeInTheDocument()

    await user.click(within(rowEl).getByRole('button', { name: /Delete/ }))

    expect(await within(rowEl).findByRole('alert')).toHaveTextContent('"Ceramics" is the object type of artefacts and cannot be deleted.')
    expect(screen.getByText('Ceramics')).toBeInTheDocument()
  })
})
