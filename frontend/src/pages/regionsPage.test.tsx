import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { languagesRoute, mockFetch, problem } from '../test/fetchMock'
import { renderWithProviders } from '../test/render'
import { RegionsPage } from './RegionsPage'

/** The table row for a region (names also appear as options in the parent selects). */
const rowOf = async (name: string) => (await screen.findByText(name, { selector: '.cell-title' })).closest('tr')!
const nameCell = (name: string) => screen.queryByText(name, { selector: '.cell-title' })

const region = (id: string, name: string, parentId: string | null = null, parentName: string | null = null) => ({
  id,
  name,
  nameLanguage: 'en',
  names: { en: name },
  parentId,
  parentName,
})

describe('regions page', () => {
  beforeEach(() => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('explains that a region in use cannot be deleted and keeps it in the list', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({
      ...languagesRoute,
      'GET /api/regions': () => [region('r1', 'Flanders'), region('r2', 'Paris')],
      'DELETE /api/regions/r1': () =>
        problem(409, { title: 'The request conflicts with the current state.', detail: '"Flanders" is used in artefact provenance and cannot be deleted.', code: 'region_in_use' }),
    })
    renderWithProviders(<RegionsPage />)

    const row = await rowOf('Flanders')
    await user.click(within(row).getByRole('button', { name: 'Delete' }))

    expect(await within(row).findByRole('alert')).toHaveTextContent('"Flanders" is used in artefact provenance and cannot be deleted.')
    expect(nameCell('Flanders')).toBeInTheDocument()
    expect(calls.some((c) => c.method === 'DELETE' && c.url.pathname === '/api/regions/r1')).toBe(true)
  })

  it('removes a region that is not in use after the list is refreshed', async () => {
    const user = userEvent.setup()
    let regions = [region('r1', 'Flanders'), region('r2', 'Paris')]
    mockFetch({
      ...languagesRoute,
      'GET /api/regions': () => regions,
      'DELETE /api/regions/r2': () => {
        regions = regions.filter((r) => r.id !== 'r2')
        return new Response(null, { status: 204 })
      },
    })
    renderWithProviders(<RegionsPage />)

    const row = await rowOf('Paris')
    await user.click(within(row).getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(nameCell('Paris')).toBeNull())
    expect(nameCell('Flanders')).toBeInTheDocument()
  })

  it('tells the user when an added region already exists', async () => {
    const user = userEvent.setup()
    mockFetch({
      ...languagesRoute,
      'GET /api/regions': () => [region('r1', 'Flanders')],
      // The API answers 200 (not 201) with the existing region on a normalised-name match.
      'POST /api/regions': () => region('r1', 'Flanders'),
    })
    renderWithProviders(<RegionsPage />)

    await user.type(await screen.findByRole('textbox', { name: 'New region (English)' }), ' flanders ')
    await user.click(screen.getByRole('button', { name: 'Add region' }))

    expect(await screen.findByText('“Flanders” already exists.')).toBeInTheDocument()
  })
})
