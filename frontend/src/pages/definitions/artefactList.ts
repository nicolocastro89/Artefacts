import type { GroupOption } from '../../widgets/builtin/collection'
import type { PageDefinition } from '../../widgets/core/page'

/** Ways to group the list; the values are the API's `group` parameter. */
const groupOptions: GroupOption[] = [
  { value: 'objectType', label: 'Object type', emptyLabel: 'No object type' },
  { value: 'collectionGroup', label: 'Collection group', emptyLabel: 'No collection group' },
  { value: 'origin', label: 'Origin (region)', emptyLabel: 'No linked region' },
  { value: 'century', label: 'Century', emptyLabel: 'Undated' },
  { value: 'dynasty', label: 'Dynasty or period', emptyLabel: 'No dynasty or period' },
]

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
        group: { from: 'search', name: 'group' },
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
              searchPlaceholder: 'Search number, title, maker, place, group, location',
              newHref: '/artefacts/new',
              newLabel: 'New artefact',
              groupOptions,
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
                    bindings: { rows: 'items[]', groups: 'groups[]' },
                    config: {
                      groupOptions,
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
                    bindings: { rows: 'items[]', groups: 'groups[]' },
                    config: { rowHref: '/artefacts/{id}', metaPaths: ['maker', 'creationDate'], emptyText: 'No artefacts match.', groupOptions },
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
                    { path: 'objectType', label: 'Type' },
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
