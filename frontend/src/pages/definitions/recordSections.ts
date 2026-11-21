import type { LayoutNode, WidgetNode } from '../../widgets/core/page'

/*
 * Record sections shared by the detail page and the print record. Each is a titled section; a section whose
 * fields are all empty is omitted (see `.layout--section` in primitives.css).
 */

export const acquisitionFields: WidgetNode = {
  type: 'widget',
  id: 'acquisition-fields',
  widgetId: 'field-list',
  bindings: {
    method: 'artefact.acquisition.method',
    date: 'artefact.acquisition.date',
    source: 'artefact.acquisition.source.name',
    lot: 'artefact.acquisition.lot',
    price: 'artefact.acquisition.price',
  },
  config: { labels: { method: 'Method', date: 'Date', source: 'Source', lot: 'Lot', price: 'Price' } },
}

export const acquisitionNote: WidgetNode = {
  type: 'widget',
  id: 'acquisition-note',
  widgetId: 'text-block',
  bindings: { text: 'artefact.acquisition.note' },
}

export const classificationSection: LayoutNode = {
  type: 'layout',
  id: 'classification',
  layout: 'section',
  title: 'Classification and origin',
  children: [
    {
      type: 'widget',
      id: 'classification-fields',
      widgetId: 'field-list',
      bindings: {
        objectType: 'artefact.objectType.name',
        collectionGroup: 'artefact.collectionGroup.name',
        place: 'artefact.placeOfProduction.text',
        region: 'artefact.placeOfProduction.region.name',
        dynasty: 'artefact.dynasty.name',
      },
      config: {
        labels: {
          objectType: 'Object type',
          collectionGroup: 'Group',
          place: 'Place of production',
          region: 'Region',
          dynasty: 'Dynasty or period',
        },
      },
    },
  ],
}

export const holdingSection: LayoutNode = {
  type: 'layout',
  id: 'holding',
  layout: 'section',
  title: 'Holding',
  children: [
    {
      type: 'widget',
      id: 'holding-fields',
      widgetId: 'field-list',
      bindings: { holder: 'artefact.holder.name', storageLocation: 'artefact.storageLocation.name', valuation: 'artefact.valuation' },
      config: { labels: { holder: 'Holder', storageLocation: 'Storage location', valuation: 'Valuation' } },
    },
  ],
}

export const exhibitionsSection: LayoutNode = {
  type: 'layout',
  id: 'exhibitions',
  layout: 'section',
  title: 'Exhibitions',
  children: [{ type: 'widget', id: 'exhibition-list', widgetId: 'text-list', bindings: { items: 'artefact.exhibitions[]' } }],
}

export const catalogueEntrySection: LayoutNode = {
  type: 'layout',
  id: 'catalogue-entry',
  layout: 'section',
  title: 'Catalogue entry',
  children: [
    { type: 'widget', id: 'catalogue-entry-text', widgetId: 'translated-text', bindings: { text: 'artefact.catalogueEntry', language: 'artefact.catalogueEntryLanguage', requested: 'language' } },
  ],
}
