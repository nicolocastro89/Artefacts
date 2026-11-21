import type { PageDefinition, PageNode } from '../../widgets/core/page'
import { acquisitionFields, acquisitionNote, catalogueEntrySection, classificationSection, exhibitionsSection, holdingSection } from './recordSections'

/** The record body: shared by the detail page and its print preview. */
export const artefactRecordBody: PageNode[] = [
  {
    type: 'layout',
    id: 'head',
    layout: 'row',
    children: [
      { type: 'widget', id: 'title', widgetId: 'heading', bindings: { text: 'artefact.title', subtitle: 'artefact.maker' } },
      { type: 'widget', id: 'tag', widgetId: 'inventory-tag', bindings: { text: 'artefact.inventoryNumber' } },
    ],
  },
  {
    type: 'layout',
    id: 'body',
    layout: 'split',
    children: [
      { type: 'widget', id: 'gallery', widgetId: 'image-gallery', bindings: { images: 'artefact.images[]' }, config: { emptyText: 'No images yet. Add them from Edit.' } },
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
                  inventoryNumber: 'artefact.inventoryNumber',
                  maker: 'artefact.maker',
                  creationDate: 'artefact.creationDate',
                  medium: 'artefact.medium',
                  dimensions: 'artefact.dimensions',
                },
                config: {
                  labels: { inventoryNumber: 'Inventory no.', maker: 'Maker', creationDate: 'Date', medium: 'Medium', dimensions: 'Dimensions' },
                },
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
                type: 'repeat',
                id: 'provenance-steps',
                itemsPath: 'artefact.provenance[]',
                layout: 'steps',
                emptyText: 'No provenance recorded.',
                children: [
                  { type: 'widget', id: 'step', widgetId: 'list-row', bindings: { primary: 'region.name', secondary: 'period', note: 'note' } },
                ],
              },
            ],
          },
          exhibitionsSection,
          catalogueEntrySection,
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
    dataSource: { kind: 'query', queryId: 'artefact-detail', variables: { id: { from: 'route', name: 'id' } }, require: 'artefact' },
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
            bindings: { id: 'artefact.id' },
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
