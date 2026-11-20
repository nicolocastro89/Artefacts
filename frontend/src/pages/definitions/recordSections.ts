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
    method: 'acquisition.method',
    date: 'acquisition.date',
    source: 'acquisition.source',
    lot: 'acquisition.lot',
    price: 'acquisition.price',
  },
  config: { labels: { method: 'Method', date: 'Date', source: 'Source', lot: 'Lot', price: 'Price' } },
}

export const acquisitionNote: WidgetNode = {
  type: 'widget',
  id: 'acquisition-note',
  widgetId: 'text-block',
  bindings: { text: 'acquisition.note' },
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
        objectType: 'objectType.name',
        collectionGroup: 'collectionGroup',
        place: 'placeOfProduction.text',
        region: 'placeOfProduction.region.name',
        dynasty: 'dynasty',
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
      bindings: { holder: 'holder', storageLocation: 'storageLocation', valuation: 'valuation' },
      config: { labels: { holder: 'Holder', storageLocation: 'Storage location', valuation: 'Valuation' } },
    },
  ],
}

export const exhibitionsSection: LayoutNode = {
  type: 'layout',
  id: 'exhibitions',
  layout: 'section',
  title: 'Exhibitions',
  children: [{ type: 'widget', id: 'exhibition-list', widgetId: 'text-list', bindings: { items: 'exhibitions[]' } }],
}

export const catalogueEntrySection: LayoutNode = {
  type: 'layout',
  id: 'catalogue-entry',
  layout: 'section',
  title: 'Catalogue entry (English)',
  children: [{ type: 'widget', id: 'catalogue-entry-text', widgetId: 'text-block', bindings: { text: 'catalogueEntryEn' } }],
}
