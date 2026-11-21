import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { languagesRoute, mockFetch, problem } from '../test/fetchMock'
import { renderWithProviders } from '../test/render'
import { ListPage } from './ListPage'
import { PartiesPage } from './PartiesPage'

const rowOf = async (name: string) => (await screen.findByText(name, { selector: '.cell-title' })).closest('tr')!

describe('list pages', () => {
  beforeEach(() => vi.spyOn(window, 'confirm').mockReturnValue(true))
  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('shows a dynasty’s names per language and renames it in both', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({
      ...languagesRoute,
      'GET /api/lists/dynasties': () => [{ id: 'd1', kind: 'dynasty', name: 'Ilkahnid', nameLanguage: 'en', names: { it: 'Ilkahnide', en: 'Ilkahnid' }, artefactCount: 5 }],
      'PUT /api/lists/dynasties/d1': () => ({ id: 'd1', kind: 'dynasty', name: 'Ilkhanid', nameLanguage: 'en', names: { it: 'Ilkhanide', en: 'Ilkhanid' }, artefactCount: 5 }),
    })
    renderWithProviders(<ListPage list="dynasties" />, { route: '/reference/dynasties' })

    expect(await screen.findByRole('heading', { name: 'Dynasties' })).toBeInTheDocument()
    const row = await rowOf('Ilkahnide')
    expect(screen.getByRole('columnheader', { name: 'Italian' })).toBeInTheDocument()
    expect(within(row).getByText('Ilkahnid')).toBeInTheDocument()
    expect(within(row).getByText('5')).toBeInTheDocument()

    await user.click(within(row).getByRole('button', { name: /Rename/ }))
    const it = within(row).getByRole('textbox', { name: 'Italian name for Ilkahnid' })
    await user.clear(it)
    await user.type(it, 'Ilkhanide')
    const en = within(row).getByRole('textbox', { name: 'English name for Ilkahnid' })
    await user.clear(en)
    await user.type(en, 'Ilkhanid')
    await user.click(within(row).getByRole('button', { name: 'Save names' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true))
    expect(calls.find((c) => c.method === 'PUT')!.body).toEqual({ names: { it: 'Ilkhanide', en: 'Ilkhanid' } })
  })

  it('adds a storage location with a single name and reports an existing one', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({
      ...languagesRoute,
      'GET /api/lists/storage-locations': () => [{ id: 'l1', kind: 'storageLocation', name: 'MI F3', nameLanguage: null, names: null, artefactCount: 4 }],
      'POST /api/lists/storage-locations': () => ({ id: 'l1', kind: 'storageLocation', name: 'MI F3', nameLanguage: null, names: null, artefactCount: 4 }),
    })
    renderWithProviders(<ListPage list="storage-locations" />, { route: '/reference/storage-locations' })

    await rowOf('MI F3')
    expect(screen.getByRole('columnheader', { name: 'Name' })).toBeInTheDocument()
    expect(screen.queryByRole('columnheader', { name: 'Italian' })).toBeNull()
    await user.type(screen.getByRole('textbox', { name: 'New storage location' }), ' mi f3 ')
    await user.click(screen.getByRole('button', { name: /Add storage location/ }))

    expect(await screen.findByText('“MI F3” already exists.')).toBeInTheDocument()
    expect(calls.find((c) => c.method === 'POST')!.body).toEqual({ name: 'mi f3' })
  })

  it('explains that a collection group in use cannot be deleted and keeps it', async () => {
    const user = userEvent.setup()
    mockFetch({
      ...languagesRoute,
      'GET /api/lists/collection-groups': () => [{ id: 'g1', kind: 'collectionGroup', name: 'CINA', nameLanguage: 'it', names: { it: 'CINA' }, artefactCount: 12 }],
      'DELETE /api/lists/collection-groups/g1': () =>
        problem(409, { title: 'Conflict', detail: '"CINA" is used by artefacts and cannot be deleted.', code: 'entry_in_use' }),
    })
    renderWithProviders(<ListPage list="collection-groups" />, { route: '/reference/collection-groups' })

    const row = await rowOf('CINA')
    await user.click(within(row).getByRole('button', { name: /Delete/ }))

    expect(await within(row).findByRole('alert')).toHaveTextContent('"CINA" is used by artefacts and cannot be deleted.')
    expect(screen.getByText('CINA', { selector: '.cell-title' })).toBeInTheDocument()
  })
})

describe('parties page', () => {
  beforeEach(() => vi.spyOn(window, 'confirm').mockReturnValue(true))
  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  const parties = [
    { id: 'p1', name: 'Christie’s', kind: 'organisation', sourceCount: 3, holderCount: 1 },
    { id: 'p2', name: 'Anna Rossi', kind: 'person', sourceCount: 0, holderCount: 2 },
  ]

  it('lists source and holder counts, filters by kind, edits the kind and explains a party in use', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({
      'GET /api/parties': () => parties,
      'PUT /api/parties/p2': () => ({ ...parties[1], kind: 'organisation' }),
      'DELETE /api/parties/p1': () =>
        problem(409, { title: 'Conflict', detail: '"Christie’s" is the acquisition source or holder of artefacts and cannot be deleted.', code: 'party_in_use' }),
    })
    renderWithProviders(<PartiesPage />, { route: '/reference/parties' })

    const christies = await rowOf('Christie’s')
    expect(within(christies).getByText('3')).toBeInTheDocument()
    expect(within(christies).getByText('1')).toBeInTheDocument()

    await user.selectOptions(screen.getByRole('combobox', { name: 'Filter by kind' }), 'People')
    expect(screen.queryByText('Christie’s', { selector: '.cell-title' })).toBeNull()
    const anna = await rowOf('Anna Rossi')
    await user.click(within(anna).getByRole('button', { name: /Edit/ }))
    await user.selectOptions(within(anna).getByRole('combobox', { name: 'Kind of Anna Rossi' }), 'Organisation')
    await user.click(within(anna).getByRole('button', { name: 'Save party' }))
    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true))
    expect(calls.find((c) => c.method === 'PUT')!.body).toEqual({ name: 'Anna Rossi', kind: 'organisation' })

    await user.selectOptions(screen.getByRole('combobox', { name: 'Filter by kind' }), 'All kinds')
    const row = await rowOf('Christie’s')
    await user.click(within(row).getByRole('button', { name: /Delete/ }))
    expect(await within(row).findByRole('alert')).toHaveTextContent('cannot be deleted')
  })
})
