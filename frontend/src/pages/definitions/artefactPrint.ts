import type { PageDefinition } from '../../widgets/core/page'
import { acquisitionFields, acquisitionNote, catalogueEntrySection, classificationSection, exhibitionsSection, holdingSection } from './recordSections'

/**
 * The printed artefact record. Same widgets as the detail page, arranged for paper:
 * images on their own keep-together plates, provenance as a table that can continue across pages.
 */
export const artefactPrintPage: PageDefinition = {
  id: 'artefact-print',
  title: 'Print artefact',
  root: {
    type: 'container',
    id: 'artefact',
    dataSource: { kind: 'endpoint', operationId: 'getArtefact', params: { id: { from: 'route', name: 'id' } } },
    children: [
      {
        type: 'layout',
        id: 'doc',
        layout: 'stack',
        children: [
          {
            type: 'layout',
            id: 'head',
            layout: 'row',
            children: [
              { type: 'widget', id: 'title', widgetId: 'heading', bindings: { text: 'title', subtitle: 'maker' } },
              { type: 'widget', id: 'tag', widgetId: 'inventory-tag', bindings: { text: 'inventoryNumber' } },
            ],
          },
          { type: 'widget', id: 'primary', widgetId: 'image', bindings: { src: 'primaryImage.urls', caption: 'primaryImage.caption', alt: 'title' }, config: { size: 'print', aspect: 'auto' } },
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
                bindings: { inventoryNumber: 'inventoryNumber', maker: 'maker', creationDate: 'creationDate', medium: 'medium', dimensions: 'dimensions' },
                config: { labels: { inventoryNumber: 'Inventory no.', maker: 'Maker', creationDate: 'Date', medium: 'Medium', dimensions: 'Dimensions' } },
              },
              { type: 'widget', id: 'description', widgetId: 'text-block', bindings: { text: 'description' } },
            ],
          },
          classificationSection,
          holdingSection,
          {
            type: 'layout',
            id: 'acquisition',
            layout: 'section',
            title: 'Acquisition',
            children: [
              acquisitionFields,
              acquisitionNote,
            ],
          },
          {
            type: 'layout',
            id: 'provenance',
            layout: 'section',
            title: 'Provenance',
            children: [
              {
                type: 'widget',
                id: 'provenance-table',
                widgetId: 'table',
                bindings: { rows: 'provenance[]' },
                config: {
                  emptyText: 'No provenance recorded.',
                  columns: [
                    { path: 'regionName', label: 'Region' },
                    { path: 'period', label: 'Period' },
                    { path: 'note', label: 'Note' },
                  ],
                },
              },
            ],
          },
          exhibitionsSection,
          catalogueEntrySection,
          {
            type: 'layout',
            id: 'images',
            layout: 'section',
            title: 'Images',
            children: [{ type: 'widget', id: 'gallery', widgetId: 'image-gallery', bindings: { images: 'images[]' } }],
          },
        ],
      },
    ],
  },
}
