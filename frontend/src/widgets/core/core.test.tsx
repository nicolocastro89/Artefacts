import { screen } from '@testing-library/react'
import { renderWithProviders } from '../../test/render'
import type { PageDefinition } from './page'
import { PageRenderer } from './PageRenderer'
import { resolvePath, typeAt } from './paths'
import { defineWidget, getWidget, listWidgets, unregisterWidget } from './registry'

const urls = (name: string) => ({ thumb: `/t/${name}`, web: `/w/${name}`, print: `/p/${name}` })

const detail = {
  id: 'a1',
  inventoryNumber: 'COL-1',
  title: 'Still Life with Lemons',
  maker: 'Workshop of Heda',
  creationDate: { display: 'c. 1650', earliestYear: 1640, latestYear: 1660, precision: 'approximate' },
  medium: null,
  dimensions: null,
  description: null,
  acquisition: null,
  provenance: [
    { regionId: 'r1', regionName: 'Flanders', period: { display: 'c. 1650', earliestYear: 1640, latestYear: 1660, precision: 'approximate' }, note: null },
    { regionId: 'r2', regionName: 'Paris', period: { display: '1780s', earliestYear: 1780, latestYear: 1789, precision: 'decade' }, note: 'Sold' },
  ],
  primaryImage: { mediaAssetId: 'm1', originalFileName: 'a.jpg', format: 'jpeg', sizeBytes: 1, uploadedAt: '2026-01-01T00:00:00Z', caption: 'Recto', isPrimary: true, urls: urls('m1') },
  images: [],
}

const list = {
  items: [{ id: 'a1', inventoryNumber: 'COL-1', title: 'Still Life', maker: null, creationDate: null, medium: null, primaryImage: urls('row'), imageCount: 1 }],
  total: 1,
  page: 1,
  pageSize: 25,
}

describe('widget registry', () => {
  it('registers a widget and lists its contract', () => {
    defineWidget({
      id: 'test-widget',
      label: 'Test',
      slots: { value: { type: 'text', required: true } },
      config: { emphasis: false },
      print: { keepTogether: true },
      component: () => null,
    })

    const entry = listWidgets().find((w) => w.id === 'test-widget')
    expect(entry).toBeDefined()
    expect(entry!.slots.value).toEqual({ type: 'text', required: true })
    expect(entry!.config).toEqual({ emphasis: false })
    expect(entry!.print.keepTogether).toBe(true)
    unregisterWidget('test-widget')
    expect(getWidget('test-widget')).toBeUndefined()
  })

  it('includes the built-in widgets', () => {
    const ids = listWidgets().map((w) => w.id)
    expect(ids).toEqual(expect.arrayContaining(['image', 'image-gallery', 'field-list', 'table', 'heading']))
  })
})

describe('binding paths', () => {
  it('resolves nested, list and item-relative paths', () => {
    expect(resolvePath(detail, 'creationDate.display')).toBe('c. 1650')
    expect(resolvePath(detail, 'provenance[]')).toHaveLength(2)
    expect(resolvePath(detail.provenance[1], 'period.display')).toBe('1780s')
    expect(resolvePath(detail, 'missing.path')).toBeUndefined()
    expect(resolvePath(detail, '')).toBe(detail)
  })

  it('looks up types relative to a list scope', () => {
    const types = { 'provenance[]': 'list', 'provenance[].period': 'historical-date' } as const
    expect(typeAt(types, 'provenance[]', 'period')).toBe('historical-date')
    expect(typeAt(types, '', 'provenance[]')).toBe('list')
  })
})

function page(root: PageDefinition['root']): PageDefinition {
  return { id: 'test', title: 'Test', root }
}

describe('PageRenderer', () => {
  it('renders the same image widget from a detail response and from a list row', () => {
    const def = page({
      type: 'layout',
      id: 'l',
      layout: 'stack',
      children: [
        {
          type: 'container',
          id: 'detail',
          dataSource: { kind: 'static', data: detail, typedAs: 'getArtefact' },
          children: [{ type: 'widget', id: 'img1', widgetId: 'image', bindings: { src: 'primaryImage.urls', alt: 'title' } }],
        },
        {
          type: 'container',
          id: 'list',
          dataSource: { kind: 'static', data: list, typedAs: 'listArtefacts' },
          children: [
            {
              type: 'repeat',
              id: 'rows',
              itemsPath: 'items[]',
              children: [{ type: 'widget', id: 'img2', widgetId: 'image', bindings: { src: 'primaryImage', alt: 'title' } }],
            },
          ],
        },
      ],
    })

    renderWithProviders(<PageRenderer page={def} />)

    const images = screen.getAllByRole('img')
    expect(images.map((i) => i.getAttribute('src'))).toEqual(['/w/m1', '/w/row'])
  })

  it('reports a type mismatch instead of rendering', () => {
    const def = page({
      type: 'container',
      id: 'c',
      dataSource: { kind: 'static', data: detail, typedAs: 'getArtefact' },
      children: [{ type: 'widget', id: 'bad', widgetId: 'image', bindings: { src: 'title' } }],
    })

    renderWithProviders(<PageRenderer page={def} />)

    const alert = screen.getByRole('alert')
    expect(alert).toHaveTextContent('Slot "src" expects image but "title" is text.')
    expect(screen.queryByRole('img')).toBeNull()
  })

  it('names a missing required slot', () => {
    const def = page({
      type: 'container',
      id: 'c',
      dataSource: { kind: 'static', data: detail, typedAs: 'getArtefact' },
      children: [{ type: 'widget', id: 'bad', widgetId: 'image', bindings: { alt: 'title' } }],
    })

    renderWithProviders(<PageRenderer page={def} />)

    expect(screen.getByRole('alert')).toHaveTextContent('Required slot "src" has no binding.')
  })

  it('resolves repeater children against each item', () => {
    const def = page({
      type: 'container',
      id: 'c',
      dataSource: { kind: 'static', data: detail, typedAs: 'getArtefact' },
      children: [
        {
          type: 'repeat',
          id: 'prov',
          itemsPath: 'provenance[]',
          children: [
            {
              type: 'widget',
              id: 'f',
              widgetId: 'field-list',
              bindings: { region: 'regionName', period: 'period' },
              config: { labels: { region: 'Region', period: 'Period' }, showRanges: false },
            },
          ],
        },
      ],
    })

    const { container } = renderWithProviders(<PageRenderer page={def} />)

    const items = container.querySelectorAll('.repeat__item')
    expect(items).toHaveLength(2)
    expect(items[0]).toHaveTextContent('Flanders')
    expect(items[0]).toHaveTextContent('c. 1650')
    expect(items[1]).toHaveTextContent('Paris')
    expect(items[1]).toHaveTextContent('1780s')
  })

  it('renders identically after a JSON round trip of the page definition', () => {
    const def = page({
      type: 'container',
      id: 'c',
      dataSource: { kind: 'static', data: detail, typedAs: 'getArtefact' },
      children: [
        { type: 'widget', id: 'h', widgetId: 'heading', bindings: { text: 'title', subtitle: 'maker' } },
        { type: 'widget', id: 'i', widgetId: 'image', bindings: { src: 'primaryImage.urls', caption: 'primaryImage.caption' } },
        { type: 'repeat', id: 'p', itemsPath: 'provenance[]', children: [{ type: 'widget', id: 'r', widgetId: 'field-list', bindings: { region: 'regionName' } }] },
      ],
    })

    const first = renderWithProviders(<PageRenderer page={def} />).container.innerHTML
    const restored = JSON.parse(JSON.stringify(def)) as PageDefinition
    const second = renderWithProviders(<PageRenderer page={restored} />).container.innerHTML

    expect(second).toBe(first)
  })
})
