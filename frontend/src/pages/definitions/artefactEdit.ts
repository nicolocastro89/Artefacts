import type { PageDefinition, WidgetNode } from '../../widgets/core/page'

const formBindings: WidgetNode['bindings'] = {
  id: 'id',
  inventoryNumber: 'inventoryNumber',
  title: 'title',
  maker: 'maker',
  creationDate: 'creationDate',
  medium: 'medium',
  dimensions: 'dimensions',
  description: 'description',
  acquisitionMethod: 'acquisition.method',
  acquisitionDate: 'acquisition.date',
  acquisitionSource: 'acquisition.source',
  acquisitionPrice: 'acquisition.price',
  provenance: 'provenance[]',
}

/** Editing an existing artefact: images first (they are what staff check), then the catalogue form. */
export const artefactEditPage: PageDefinition = {
  id: 'artefact-edit',
  title: 'Edit artefact',
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
            config: { actions: [{ label: 'Back to record', href: '/artefacts/{id}', icon: 'back' }] },
          },
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
            id: 'images',
            layout: 'section',
            title: 'Images',
            children: [{ type: 'widget', id: 'image-manager', widgetId: 'image-manager', bindings: { id: 'id', images: 'images[]' } }],
          },
          { type: 'widget', id: 'form', widgetId: 'artefact-form', bindings: formBindings },
        ],
      },
    ],
  },
}

/** Creating an artefact: an empty record (typed as an artefact so bindings stay checked). Images are added after creation. */
export const artefactNewPage: PageDefinition = {
  id: 'artefact-new',
  title: 'New artefact',
  root: {
    type: 'container',
    id: 'artefact',
    dataSource: { kind: 'static', data: {}, typedAs: 'getArtefact' },
    children: [
      {
        type: 'layout',
        id: 'page',
        layout: 'stack',
        children: [
          {
            type: 'layout',
            id: 'head',
            layout: 'section',
            title: 'New artefact',
            children: [],
          },
          { type: 'widget', id: 'form', widgetId: 'artefact-form', bindings: formBindings, config: { cancelHref: '/artefacts' } },
        ],
      },
    ],
  },
}
