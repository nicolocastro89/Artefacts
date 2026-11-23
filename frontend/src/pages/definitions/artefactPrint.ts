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
    dataSource: { kind: 'query', queryId: 'artefact-detail', variables: { id: { from: 'route', name: 'id' } }, require: 'artefact' },
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
              { type: 'widget', id: 'title', widgetId: 'heading', bindings: { text: 'artefact.title', subtitle: 'artefact.maker' } },
              { type: 'widget', id: 'tag', widgetId: 'inventory-tag', bindings: { text: 'artefact.inventoryNumber' } },
            ],
          },
          { type: 'widget', id: 'primary', widgetId: 'image', bindings: { src: 'artefact.primaryImage.urls', caption: 'artefact.primaryImage.caption', alt: 'artefact.title' }, config: { size: 'print', aspect: 'auto' } },
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
                  inventoryNumber: 'artefact.inventoryNumber',
                  maker: 'artefact.maker',
                  creationDate: 'artefact.creationDate',
                  medium: 'artefact.medium',
                  dimensions: 'artefact.dimensions',
                  height: 'artefact.height',
                  width: 'artefact.width',
                  length: 'artefact.length',
                  diameter: 'artefact.diameter',
                },
                config: { labels: { inventoryNumber: 'Inventory no.', maker: 'Maker', creationDate: 'Date', medium: 'Medium', dimensions: 'Dimensions' }, appendMeasurements: 'dimensions' },
              },
              { type: 'widget', id: 'description', widgetId: 'translated-text', bindings: { text: 'artefact.description', language: 'artefact.descriptionLanguage', requested: 'language' } },
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
                bindings: { rows: 'artefact.provenance[]' },
                config: {
                  emptyText: 'No provenance recorded.',
                  columns: [
                    { path: 'region.name', label: 'Region' },
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
            children: [{ type: 'widget', id: 'gallery', widgetId: 'image-gallery', bindings: { images: 'artefact.images[]' } }],
          },
        ],
      },
    ],
  },
}
