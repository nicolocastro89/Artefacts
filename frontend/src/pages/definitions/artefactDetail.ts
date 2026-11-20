import type { PageDefinition, PageNode } from '../../widgets/core/page'

/** The record body: shared by the detail page and its print preview. */
export const artefactRecordBody: PageNode[] = [
  {
    type: 'layout',
    id: 'head',
    layout: 'row',
    children: [
      { type: 'widget', id: 'title', widgetId: 'heading', bindings: { text: 'title', subtitle: 'maker' } },
      { type: 'widget', id: 'tag', widgetId: 'inventory-tag', bindings: { text: 'inventoryNumber' } },
    ],
  },
  {
    type: 'layout',
    id: 'body',
    layout: 'split',
    children: [
      { type: 'widget', id: 'gallery', widgetId: 'image-gallery', bindings: { images: 'images[]' }, config: { emptyText: 'No images yet. Add them from Edit.' } },
      {
        type: 'layout',
        id: 'facts',
        layout: 'stack',
        children: [
          {
            type: 'layout',
            id: 'catalogue',
            layout: 'section',
            title: 'Catalogue',
            children: [
              {
                type: 'widget',
                id: 'catalogue-fields',
                widgetId: 'field-list',
                bindings: {
                  inventoryNumber: 'inventoryNumber',
                  maker: 'maker',
                  creationDate: 'creationDate',
                  medium: 'medium',
                  dimensions: 'dimensions',
                },
                config: {
                  labels: { inventoryNumber: 'Inventory no.', maker: 'Maker', creationDate: 'Date', medium: 'Medium', dimensions: 'Dimensions' },
                },
              },
              { type: 'widget', id: 'description', widgetId: 'text-block', bindings: { text: 'description' } },
            ],
          },
          {
            type: 'layout',
            id: 'acquisition',
            layout: 'section',
            title: 'Acquisition',
            children: [
              {
                type: 'widget',
                id: 'acquisition-fields',
                widgetId: 'field-list',
                bindings: {
                  method: 'acquisition.method',
                  date: 'acquisition.date',
                  source: 'acquisition.source',
                  price: 'acquisition.price',
                },
                config: { labels: { method: 'Method', date: 'Date', source: 'Source', price: 'Price' } },
              },
            ],
          },
          {
            type: 'layout',
            id: 'provenance',
            layout: 'section',
            title: 'Provenance',
            children: [
              {
                type: 'repeat',
                id: 'provenance-steps',
                itemsPath: 'provenance[]',
                layout: 'steps',
                emptyText: 'No provenance recorded.',
                children: [
                  { type: 'widget', id: 'step', widgetId: 'list-row', bindings: { primary: 'regionName', secondary: 'period', note: 'note' } },
                ],
              },
            ],
          },
        ],
      },
    ],
  },
]

export const artefactDetailPage: PageDefinition = {
  id: 'artefact-detail',
  title: 'Artefact',
  root: {
    type: 'container',
    id: 'artefact',
    dataSource: { kind: 'endpoint', operationId: 'getArtefact', params: { id: { from: 'route', name: 'id' } } },
    children: [
      {
        type: 'layout',
        id: 'page',
        layout: 'stack',
        children: [
          {
            type: 'widget',
            id: 'actions',
            widgetId: 'record-actions',
            bindings: { id: 'id' },
            config: {
              actions: [
                { label: 'All artefacts', href: '/artefacts', icon: 'back' },
                { label: 'Print', href: '/artefacts/{id}/print', icon: 'print' },
                { label: 'Edit', href: '/artefacts/{id}/edit', icon: 'edit', primary: true },
              ],
            },
          },
          ...artefactRecordBody,
        ],
      },
    ],
  },
}
