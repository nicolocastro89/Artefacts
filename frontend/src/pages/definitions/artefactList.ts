import type { PageDefinition } from '../../widgets/core/page'

/** The artefact list: toolbar, table or grid, the pull-out rack, and paging. All state lives in the URL. */
export const artefactListPage: PageDefinition = {
  id: 'artefact-list',
  title: 'Artefacts',
  root: {
    type: 'container',
    id: 'artefacts',
    dataSource: {
      kind: 'endpoint',
      operationId: 'listArtefacts',
      params: {
        search: { from: 'search', name: 'search' },
        sort: { from: 'search', name: 'sort' },
        direction: { from: 'search', name: 'direction' },
        page: { from: 'search', name: 'page' },
        pageSize: 25,
      },
    },
    children: [
      {
        type: 'layout',
        id: 'frame',
        layout: 'stack',
        children: [
          {
            type: 'widget',
            id: 'toolbar',
            widgetId: 'list-toolbar',
            bindings: { total: 'total' },
            config: {
              title: 'Artefacts',
              noun: 'artefact',
              searchLabel: 'Search artefacts',
              searchPlaceholder: 'Search inventory number, title or maker',
              newHref: '/artefacts/new',
              newLabel: 'New artefact',
            },
          },
          {
            type: 'layout',
            id: 'collection',
            layout: 'rack',
            children: [
              {
                type: 'layout',
                id: 'views',
                layout: 'stack',
                children: [
                  {
                    type: 'widget',
                    id: 'table',
                    widgetId: 'record-table',
                    bindings: { rows: 'items[]' },
                    config: {
                      caption: 'Artefacts',
                      rowHref: '/artefacts/{id}',
                      emptyText: 'No artefacts match.',
                      columns: [
                        { path: 'primaryImage', label: 'Image', kind: 'thumb' },
                        { path: 'inventoryNumber', label: 'Inventory no.', kind: 'tag', sort: 'inventoryNumber' },
                        { path: 'title', label: 'Title', kind: 'title', sort: 'title' },
                        { path: 'maker', label: 'Maker', optional: true },
                        { path: 'creationDate', label: 'Date', sort: 'creationDate' },
                        { path: 'medium', label: 'Medium', optional: true },
                      ],
                    },
                  },
                  {
                    type: 'widget',
                    id: 'grid',
                    widgetId: 'record-grid',
                    bindings: { rows: 'items[]' },
                    config: { rowHref: '/artefacts/{id}', metaPaths: ['maker', 'creationDate'], emptyText: 'No artefacts match.' },
                  },
                  { type: 'widget', id: 'pager', widgetId: 'pager', bindings: { total: 'total', page: 'page', pageSize: 'pageSize' } },
                ],
              },
              {
                type: 'widget',
                id: 'rack',
                widgetId: 'rack-panel',
                bindings: { rows: 'items[]' },
                config: {
                  openHref: '/artefacts/{id}',
                  editHref: '/artefacts/{id}/edit',
                  facts: [
                    { path: 'maker', label: 'Maker' },
                    { path: 'creationDate', label: 'Date' },
                    { path: 'medium', label: 'Medium' },
                    { path: 'imageCount', label: 'Images' },
                  ],
                },
              },
            ],
          },
        ],
      },
    ],
  },
}
