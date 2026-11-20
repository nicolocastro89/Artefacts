import { screen } from '@testing-library/react'
import { renderWithProviders } from '../test/render'
import type { PageDefinition } from '../widgets/core/page'
import { PageRenderer } from '../widgets/core/PageRenderer'
import { PrintContext } from './PrintContext'

const record = {
  id: 'a1',
  title: 'Still Life',
  primaryImage: { urls: { thumb: '/t', web: '/w', print: '/p' }, caption: null },
}

const page: PageDefinition = {
  id: 'p',
  title: 'P',
  root: {
    type: 'container',
    id: 'c',
    dataSource: { kind: 'static', data: record, typedAs: 'getArtefact' },
    children: [
      { type: 'widget', id: 'actions', widgetId: 'record-actions', bindings: { id: 'id' }, config: { actions: [{ label: 'Edit', href: '/artefacts/{id}/edit' }] } },
      { type: 'widget', id: 'img', widgetId: 'image', bindings: { src: 'primaryImage.urls', alt: 'title' } },
    ],
  },
}

describe('print mode', () => {
  it('hides interactive controls and uses print-size images', () => {
    renderWithProviders(
      <PrintContext.Provider value>
        <PageRenderer page={page} />
      </PrintContext.Provider>,
    )
    expect(screen.queryByRole('link', { name: 'Edit' })).toBeNull()
    expect(screen.getByRole('img', { name: 'Still Life' })).toHaveAttribute('src', '/p')
  })

  it('loads print images eagerly, because pagination waits for every image', () => {
    // A lazy image on a page that is not on screen never loads, which stalled Paged.js mid-layout.
    renderWithProviders(
      <PrintContext.Provider value>
        <PageRenderer page={page} />
      </PrintContext.Provider>,
    )
    expect(screen.getByRole('img', { name: 'Still Life' })).toHaveAttribute('loading', 'eager')
  })

  it('shows controls and web-size images on screen', () => {
    renderWithProviders(<PageRenderer page={page} />)
    expect(screen.getByRole('link', { name: 'Edit' })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Still Life' })).toHaveAttribute('src', '/w')
    expect(screen.getByRole('img', { name: 'Still Life' })).toHaveAttribute('loading', 'lazy')
  })
})
