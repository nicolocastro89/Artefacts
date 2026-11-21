import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router'
import { vi } from 'vitest'
import { readerLanguageOf } from '../app/languages'
import { languagesRoute, mockFetch, problem } from '../test/fetchMock'
import { artefactDetailRoutes } from '../test/queryFixtures'
import { renderWithProviders } from '../test/render'
import { artefactDetailPage } from './definitions/artefactDetail'
import { artefactNewPage } from './definitions/artefactEdit'
import { LanguagesPage } from './LanguagesPage'
import { PageRoute } from './PageRoute'
import { RegionsPage } from './RegionsPage'

const languages = [
  { code: 'en', name: 'English', position: 1 },
  { code: 'it', name: 'Italian', position: 2 },
]

describe('reader language', () => {
  it('takes the first browser language that is a content language, matching regional codes', () => {
    expect(readerLanguageOf(languages, ['fr-FR', 'it-IT', 'en'])).toBe('it')
    expect(readerLanguageOf(languages, ['de'])).toBe('en')
  })
})

describe('regions with names per language', () => {
  afterEach(() => vi.restoreAllMocks())

  it('shows a column per language and adds a region with both names', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({
      ...languagesRoute,
      'GET /api/regions': () => [{ id: 'r1', name: 'Flanders', nameLanguage: 'en', names: { en: 'Flanders', it: 'Fiandre' }, parentId: null, parentName: null }],
      'POST /api/regions': () =>
        new Response(JSON.stringify({ id: 'r2', name: 'Turkey', nameLanguage: 'en', names: { en: 'Turkey', it: 'Turchia' }, parentId: null, parentName: null }), {
          status: 201,
          headers: { 'Content-Type': 'application/json' },
        }),
    })
    renderWithProviders(<RegionsPage />)

    const table = await screen.findByRole('table', { name: 'Regions' })
    expect(within(table).getByRole('columnheader', { name: 'English' })).toBeInTheDocument()
    expect(within(table).getByRole('columnheader', { name: 'Italian' })).toBeInTheDocument()
    expect(within(table).getByText('Fiandre')).toBeInTheDocument()

    await user.type(screen.getByRole('textbox', { name: 'New region (Italian)' }), 'Turchia')
    await user.type(screen.getByRole('textbox', { name: 'New region (English)' }), 'Turkey')
    await user.click(screen.getByRole('button', { name: 'Add region' }))

    await screen.findByText('Added “Turkey”.')
    expect(calls.find((c) => c.method === 'POST')?.body).toEqual({ names: { it: 'Turchia', en: 'Turkey' }, parentId: null })
  })

  it('renames a region in every language at once', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({
      ...languagesRoute,
      'GET /api/regions': () => [{ id: 'r1', name: 'Holland', nameLanguage: 'en', names: { en: 'Holland', it: 'Olanda' }, parentId: null, parentName: null }],
      'PUT /api/regions/r1': () => ({ id: 'r1', name: 'Netherlands', nameLanguage: 'en', names: { en: 'Netherlands', it: 'Paesi Bassi' }, parentId: null, parentName: null }),
    })
    renderWithProviders(<RegionsPage />)

    const row = (await screen.findByText('Holland', { selector: '.cell-title' })).closest('tr')!
    await user.click(within(row).getByRole('button', { name: 'Rename' }))
    const english = within(row).getByRole('textbox', { name: 'English name for Holland' })
    const italian = within(row).getByRole('textbox', { name: 'Italian name for Holland' })
    await user.clear(english)
    await user.type(english, 'Netherlands')
    await user.clear(italian)
    await user.type(italian, 'Paesi Bassi{Enter}')

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true))
    expect(calls.find((c) => c.method === 'PUT')?.body).toEqual({ names: { en: 'Netherlands', it: 'Paesi Bassi' } })
  })
})

describe('languages page', () => {
  afterEach(() => vi.restoreAllMocks())

  it('moves a language up in the fallback order and explains when one in use cannot be removed', async () => {
    const user = userEvent.setup()
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const { calls } = mockFetch({
      'GET /api/languages': () => languages,
      'PUT /api/languages/order': () => [languages[1], languages[0]],
      'DELETE /api/languages/it': () => problem(409, { title: 'Conflict', detail: 'Italian is used by names or descriptions and cannot be removed.', code: 'language_in_use' }),
    })
    renderWithProviders(<LanguagesPage />)

    await user.click(await screen.findByRole('button', { name: 'Move Italian up' }))
    await waitFor(() => expect(calls.find((c) => c.method === 'PUT')?.body).toEqual({ codes: ['it', 'en'] }))

    const row = screen.getByText('Italian', { selector: '.cell-title' }).closest('tr')!
    await user.click(within(row).getByRole('button', { name: /Remove/ }))
    expect(await within(row).findByRole('alert')).toHaveTextContent('Italian is used by names or descriptions and cannot be removed.')
  })
})

describe('description per language', () => {
  it('sends only the languages that have a description', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({ ...languagesRoute, 'POST /api/artefacts': () => ({ id: 'new1' }) })
    renderWithProviders(
      <Routes>
        <Route path="/artefacts/new" element={<PageRoute page={artefactNewPage} />} />
        <Route path="/artefacts/:id" element={<p>Saved record</p>} />
      </Routes>,
      { route: '/artefacts/new' },
    )

    await user.type(await screen.findByRole('textbox', { name: 'Inventory number' }), 'INV-1')
    await user.type(screen.getByRole('textbox', { name: 'Title' }), 'Dish')
    await user.type(await screen.findByRole('textbox', { name: 'Description (English)' }), 'Dish with a phoenix')
    expect(screen.getByRole('textbox', { name: 'Description (Italian)' })).toHaveValue('')
    await user.click(screen.getByRole('button', { name: 'Create artefact' }))

    await screen.findByText('Saved record')
    const body = calls.find((c) => c.method === 'POST' && c.url.pathname === '/api/artefacts')!.body as Record<string, unknown>
    expect(body.descriptions).toEqual({ en: 'Dish with a phoenix' })
  })

  it('marks a description shown in a fallback language', async () => {
    mockFetch({
      ...artefactDetailRoutes(
        {
        id: 'a1',
        inventoryNumber: 'COL-2006-015',
        title: 'Ewer in the Form of a Cockerel',
        maker: null,
        creationDate: null,
        medium: null,
        dimensions: null,
        description: 'Brocca in forma di gallo.',
        descriptionLanguage: 'it',
        acquisition: null,
        provenance: [],
        primaryImage: null,
        images: [],
        objectType: null,
        collectionGroup: null,
        placeOfProduction: null,
        dynasty: null,
        exhibitions: [],
        catalogueEntry: null,
        catalogueEntryLanguage: null,
        catalogueEntries: {},
        holder: null,
        storageLocation: null,
        valuation: null,
        },
        'en',
      ),
    })
    renderWithProviders(
      <Routes>
        <Route path="/artefacts/:id" element={<PageRoute page={artefactDetailPage} />} />
      </Routes>,
      { route: '/artefacts/a1' },
    )

    const text = await screen.findByText('Brocca in forma di gallo.')
    const block = text.closest('.text-block') as HTMLElement
    expect(within(block).getByText('In Italian')).toBeInTheDocument()
    expect(block).toHaveAttribute('lang', 'it')
  })
})

describe('catalogue entry per language', () => {
  it('sends only the languages that have a catalogue entry', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({ ...languagesRoute, 'POST /api/artefacts': () => ({ id: 'new1' }) })
    renderWithProviders(
      <Routes>
        <Route path="/artefacts/new" element={<PageRoute page={artefactNewPage} />} />
        <Route path="/artefacts/:id" element={<p>Saved record</p>} />
      </Routes>,
      { route: '/artefacts/new' },
    )

    await user.type(await screen.findByRole('textbox', { name: 'Inventory number' }), 'INV-1')
    await user.type(screen.getByRole('textbox', { name: 'Title' }), 'Dish')
    await user.type(await screen.findByRole('textbox', { name: 'Catalogue entry (Italian)' }), 'Piatto con fenice')
    expect(screen.getByRole('textbox', { name: 'Catalogue entry (English)' })).toHaveValue('')
    await user.click(screen.getByRole('button', { name: 'Create artefact' }))

    await screen.findByText('Saved record')
    const body = calls.find((c) => c.method === 'POST' && c.url.pathname === '/api/artefacts')!.body as Record<string, unknown>
    expect(body.catalogueEntries).toEqual({ it: 'Piatto con fenice' })
  })

  it('marks a catalogue entry shown in a fallback language', async () => {
    mockFetch({
      ...languagesRoute,
      ...artefactDetailRoutes(
        {
        id: 'a1',
        inventoryNumber: 'COL-2006-015',
        title: 'Ewer in the Form of a Cockerel',
        maker: null,
        creationDate: null,
        medium: null,
        dimensions: null,
        description: null,
        descriptionLanguage: null,
        acquisition: null,
        provenance: [],
        primaryImage: null,
        images: [],
        objectType: null,
        collectionGroup: null,
        placeOfProduction: null,
        dynasty: null,
        exhibitions: [],
        catalogueEntry: 'AN EWER IN THE FORM OF A COCKEREL',
        catalogueEntryLanguage: 'en',
        catalogueEntries: { en: 'AN EWER IN THE FORM OF A COCKEREL' },
        holder: null,
        storageLocation: null,
        valuation: null,
        },
        'it',
      ),
    })
    renderWithProviders(
      <Routes>
        <Route path="/artefacts/:id" element={<PageRoute page={artefactDetailPage} />} />
      </Routes>,
      { route: '/artefacts/a1' },
    )

    const text = await screen.findByText('AN EWER IN THE FORM OF A COCKEREL')
    const block = text.closest('.text-block') as HTMLElement
    expect(within(block).getByText('In English')).toBeInTheDocument()
    expect(block).toHaveAttribute('lang', 'en')
  })
})
