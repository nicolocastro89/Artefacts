import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes, useLocation } from 'react-router'
import { mockFetch } from '../test/fetchMock'
import { artefactListRoutes, sentVariables } from '../test/queryFixtures'
import { renderWithProviders } from '../test/render'
import { PageRoute } from './PageRoute'

const urls = (n: string) => ({ thumb: `/img/${n}/thumb`, web: `/img/${n}/web`, print: `/img/${n}/print` })

const items = [
  { id: 'a1', inventoryNumber: 'COL-1998-014', title: 'Still Life with Lemons', maker: 'Workshop of Heda', creationDate: { display: 'c. 1650', earliestYear: 1640, latestYear: 1660, precision: 'approximate' }, medium: 'Oil on panel', primaryImage: { urls: urls('a1') }, imageCount: 2 },
  { id: 'a2', inventoryNumber: 'COL-2011-007', title: 'Study of Hands', maker: null, creationDate: null, medium: null, primaryImage: null, imageCount: 0 },
]

let lastLocation = ''
function LocationSpy() {
  const location = useLocation()
  lastLocation = location.pathname + location.search
  return null
}

function renderList(route = '/artefacts') {
  return renderWithProviders(
    <>
      <Routes>
        <Route path="/artefacts" element={<PageRoute slug="artefact-list" />} />
        <Route path="/artefacts/:id" element={<p>Detail</p>} />
      </Routes>
      <LocationSpy />
    </>,
    { route },
  )
}

describe('artefact list page', () => {
  let calls: ReturnType<typeof mockFetch>['calls']

  beforeEach(() => {
    ;({ calls } = mockFetch(
      artefactListRoutes((variables) => ({
        artefacts: { items, total: 52, page: Number(variables.page ?? 1), pageSize: 25, groups: [] },
      })),
    ))
  })

  it('shows artefacts in a table with thumbnails, tags and dates', async () => {
    renderList()

    const table = await screen.findByRole('table', { name: 'Artefacts' })
    expect(within(table).getByText('Still Life with Lemons')).toBeInTheDocument()
    expect(within(table).getByText('COL-1998-014')).toHaveClass('tag')
    expect(within(table).getByText('c. 1650')).toBeInTheDocument()
    expect(screen.getByText(/52 artefacts/)).toBeInTheDocument()
    expect(document.querySelector('.widget-config-error')).toBeNull()
  })

  it('shows a neutral placeholder when an artefact has no image', async () => {
    renderList()

    const row = (await screen.findByText('Study of Hands')).closest('tr')!
    expect(within(row).getByRole('img', { name: 'No image' })).toBeInTheDocument()
  })

  it('switches to the grid and records it in the URL', async () => {
    const user = userEvent.setup()
    renderList()
    await screen.findByRole('table')

    await user.click(screen.getByRole('button', { name: 'Grid' }))

    expect(screen.queryByRole('table')).toBeNull()
    const grid = screen.getByRole('list', { name: 'Records' })
    expect(within(grid).getByText('Still Life with Lemons')).toBeInTheDocument()
    // Card thumbnails are decorative (alt=""): the title beside them names the artefact.
    expect(grid.querySelector('img')).toHaveAttribute('src', '/img/a1/thumb')
    expect(lastLocation).toContain('view=grid')
  })

  it('restores search, sort and page from a shared URL', async () => {
    renderList('/artefacts?search=lemons&sort=creationDate&direction=desc&page=2&view=grid')

    await screen.findByRole('list', { name: 'Records' })
    expect(sentVariables(calls).at(-1)).toEqual({ search: 'lemons', sort: 'CREATION_DATE', direction: 'DESC', page: 2, pageSize: 25 })
    expect(screen.getByRole('searchbox', { name: 'Search artefacts' })).toHaveValue('lemons')
    expect(screen.getByText('Page 2 of 3')).toBeInTheDocument()
  })

  it('sorts by a column header', async () => {
    const user = userEvent.setup()
    renderList()
    await screen.findByRole('table')

    await user.click(screen.getByRole('button', { name: /Date/ }))

    await waitFor(() => expect(lastLocation).toContain('sort=creationDate'))
    await waitFor(() => expect(sentVariables(calls).at(-1)?.sort).toBe('CREATION_DATE'))
  })

  it('pulls the selected artefact out into the rack, and slides it back with Escape', async () => {
    const user = userEvent.setup()
    renderList()
    await user.click(await screen.findByText('Workshop of Heda'))

    const rack = screen.getByRole('complementary', { name: 'Preview: Still Life with Lemons' })
    expect(within(rack).getByRole('img', { name: 'Still Life with Lemons' })).toHaveAttribute('src', '/img/a1/web')
    expect(within(rack).getByRole('link', { name: 'Open record' })).toHaveAttribute('href', '/artefacts/a1')
    expect(lastLocation).toContain('selected=a1')

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('complementary')).toBeNull()
  })
})
