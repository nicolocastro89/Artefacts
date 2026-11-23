import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { languagesRoute, mockFetch, problem } from '../test/fetchMock'
import { renderWithProviders } from '../test/render'
import { UnitsPage } from './UnitsPage'

const cm = { id: 'u-cm', name: 'Centimetre', nameLanguage: 'en', names: { en: 'Centimetre', it: 'centimetro' }, abbreviation: 'cm', factor: 0.01, artefactCount: 12 }
const foot = { id: 'u-ft', name: 'Foot', nameLanguage: 'en', names: { en: 'Foot', it: 'Piede' }, abbreviation: 'ft', factor: 0.3048, artefactCount: 0 }

const rowOf = async (name: string) => (await screen.findByText(name, { selector: '.cell-title' })).closest('tr')!

describe('units page', () => {
  beforeEach(() => vi.spyOn(window, 'confirm').mockReturnValue(true))
  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('lists units with abbreviation, factor and usage, and adds “Foot”', async () => {
    const user = userEvent.setup()
    let units = [cm]
    const { calls } = mockFetch({
      ...languagesRoute,
      'GET /api/units': () => units,
      'POST /api/units': () => {
        units = [cm, foot]
        return foot
      },
    })
    renderWithProviders(<UnitsPage />, { route: '/reference/units' })

    const row = await rowOf('centimetro')
    expect(within(row).getByText('cm')).toBeInTheDocument()
    expect(within(row).getByText('0.01')).toBeInTheDocument()
    expect(within(row).getByText('12')).toBeInTheDocument()

    await user.type(screen.getByRole('textbox', { name: /New unit \(English\)/ }), 'Foot')
    await user.type(screen.getByRole('textbox', { name: /New unit \(Italian\)/ }), 'Piede')
    await user.type(screen.getByRole('textbox', { name: 'Abbreviation' }), 'ft')
    await user.type(screen.getByRole('textbox', { name: 'Metres per unit' }), '0,3048')
    await user.click(screen.getByRole('button', { name: /Add unit/ }))

    expect(await screen.findByText('Added “Foot” (ft).')).toBeInTheDocument()
    expect(calls.find((c) => c.method === 'POST')!.body).toEqual({ names: { en: 'Foot', it: 'Piede' }, abbreviation: 'ft', factor: 0.3048 })
    expect(await rowOf('Piede')).toBeInTheDocument()
  })

  it('warns that a new factor changes the measurements in use, and saves it', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({
      ...languagesRoute,
      'GET /api/units': () => [cm],
      'PUT /api/units/u-cm': () => ({ ...cm, factor: 0.011 }),
    })
    renderWithProviders(<UnitsPage />, { route: '/reference/units' })

    const row = await rowOf('centimetro')
    await user.click(within(row).getByRole('button', { name: /Edit/ }))
    const factor = within(row).getByRole('textbox', { name: 'Metres per Centimetre' })
    await user.clear(factor)
    await user.type(factor, '0.011')

    expect(within(row).getByText('Changes the value in metres of 12 artefacts.')).toBeInTheDocument()
    await user.click(within(row).getByRole('button', { name: 'Save unit' }))
    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true))
    expect(calls.find((c) => c.method === 'PUT')!.body).toEqual({ names: { en: 'Centimetre', it: 'centimetro' }, abbreviation: 'cm', factor: 0.011 })
  })

  it('explains that a unit in use cannot be deleted', async () => {
    const user = userEvent.setup()
    mockFetch({
      ...languagesRoute,
      'GET /api/units': () => [cm],
      'DELETE /api/units/u-cm': () => problem(409, { title: 'Conflict', detail: '"cm" is used by the measurements of 12 artefacts and cannot be deleted.', code: 'unit_in_use' }),
    })
    renderWithProviders(<UnitsPage />, { route: '/reference/units' })

    const row = await rowOf('centimetro')
    await user.click(within(row).getByRole('button', { name: /Delete/ }))

    expect(await within(row).findByRole('alert')).toHaveTextContent('cannot be deleted')
  })
})
