import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router'
import { mockFetch } from '../test/fetchMock'
import { artefactDetailRoutes } from '../test/queryFixtures'
import { renderWithProviders } from '../test/render'
import { PageRoute } from './PageRoute'

const urls = (n: string) => ({ thumb: `/img/${n}/thumb`, web: `/img/${n}/web`, print: `/img/${n}/print` })
const image = (n: string, isPrimary: boolean, caption: string | null) => ({
  mediaAssetId: n,
  originalFileName: `${n}.tif`,
  format: 'TIFF',
  sizeBytes: 1000,
  uploadedAt: '2026-11-19T10:00:00Z',
  caption,
  isPrimary,
  urls: urls(n),
})

const artefact = {
  id: 'a1',
  inventoryNumber: 'COL-1998-014',
  title: 'Still Life with Lemons',
  maker: 'Workshop of Heda',
  creationDate: { display: 'c. 1650', earliestYear: 1640, latestYear: 1660, precision: 'APPROXIMATE' },
  medium: 'Oil on panel',
  dimensions: '52 × 41 cm',
  description: null,
  acquisition: { method: 'PURCHASE', date: '1998-03-02', source: { id: 'p1', name: 'Private collection', kind: 'PERSON' }, price: { amount: 12000, currency: 'EUR' } },
  provenance: [
    { region: { id: 'r1', name: 'Flanders' }, period: { display: 'c. 1650', earliestYear: 1640, latestYear: 1660, precision: 'APPROXIMATE' }, note: 'Painted here' },
    { region: { id: 'r2', name: 'Paris' }, period: { display: '1780s', earliestYear: 1780, latestYear: 1789, precision: 'DECADE' }, note: null },
  ],
  primaryImage: image('m1', true, 'Recto'),
  images: [image('m1', true, 'Recto'), image('m2', false, 'Verso')],
}

function renderDetail() {
  mockFetch(artefactDetailRoutes(artefact))
  return renderWithProviders(
    <Routes>
      <Route path="/artefacts/:id" element={<PageRoute slug="artefact-detail" />} />
    </Routes>,
    { route: '/artefacts/a1' },
  )
}

describe('artefact detail page', () => {
  it('lists provenance entries in order with region name and period text', async () => {
    const { container } = renderDetail()

    await screen.findByRole('heading', { name: 'Still Life with Lemons' })
    const steps = container.querySelector('[data-node-id="provenance-steps"]') as HTMLElement
    const rows = [...steps.querySelectorAll('.list-row')].map((r) => r.textContent)
    expect(rows).toHaveLength(2)
    expect(rows[0]).toMatch(/^Flanders.*c\. 1650.*1640–1660.*Painted here/)
    expect(rows[1]).toMatch(/^Paris.*1780s.*1780–1789/)
  })

  it('shows the catalogue fields and the acquisition', async () => {
    renderDetail()

    await screen.findByRole('heading', { name: 'Still Life with Lemons' })
    expect(screen.getByText('Oil on panel')).toBeInTheDocument()
    expect(screen.getAllByText('c. 1650').length).toBeGreaterThan(0)
    expect(screen.getByText('Private collection')).toBeInTheDocument()
    expect(screen.getByText('Purchase')).toBeInTheDocument()
    expect(document.querySelector('.widget-config-error')).toBeNull()
  })

  it('shows the primary image at web size and switches the viewer when another image is picked', async () => {
    const user = userEvent.setup()
    renderDetail()

    expect(await screen.findByRole('img', { name: 'Recto' })).toHaveAttribute('src', '/img/m1/web')

    await user.click(within(screen.getByRole('list', { name: 'Images' })).getByRole('button', { name: 'Show image 2: Verso' }))

    expect(screen.getByRole('img', { name: 'Verso' })).toHaveAttribute('src', '/img/m2/web')
    expect(screen.getByRole('button', { name: 'Show image 2: Verso' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('opens on the primary image even when it was not uploaded first', async () => {
    mockFetch(artefactDetailRoutes({ ...artefact, primaryImage: image('m2', true, 'Verso'), images: [image('m1', false, 'Recto'), image('m2', true, 'Verso')] }))
    renderWithProviders(
      <Routes>
        <Route path="/artefacts/:id" element={<PageRoute slug="artefact-detail" />} />
      </Routes>,
      { route: '/artefacts/a1' },
    )

    expect(await screen.findByRole('img', { name: 'Verso' })).toHaveAttribute('src', '/img/m2/web')
    expect(screen.getByRole('button', { name: 'Show image 2: Verso' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('shows the not-found state for an unknown artefact', async () => {
    mockFetch(artefactDetailRoutes(null))
    renderWithProviders(
      <Routes>
        <Route path="/artefacts/:id" element={<PageRoute slug="artefact-detail" />} />
      </Routes>,
      { route: '/artefacts/gone' },
    )

    expect(await screen.findByRole('alert')).toHaveTextContent('This record does not exist')
    expect(screen.queryByRole('heading', { level: 1 })).toBeNull()
  })
})
