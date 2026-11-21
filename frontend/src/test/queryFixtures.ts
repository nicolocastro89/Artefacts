import type { QueryDescription } from '../widgets/core/dataSources'

/** What `GET /api/queries/artefact-list/description` returns (the result paths of the system query and its variables). */
export const artefactListDescription: QueryDescription = {
  fields: {
    'artefacts.groups[]': 'list',
    'artefacts.groups[].count': 'number',
    'artefacts.groups[].key': 'text',
    'artefacts.groups[].label': 'text',
    'artefacts.items[]': 'list',
    'artefacts.items[].collectionGroup.id': 'reference',
    'artefacts.items[].collectionGroup.name': 'text',
    'artefacts.items[].creationDate': 'historical-date',
    'artefacts.items[].dynasty.id': 'reference',
    'artefacts.items[].dynasty.name': 'text',
    'artefacts.items[].groupKey': 'text',
    'artefacts.items[].groupLabel': 'text',
    'artefacts.items[].id': 'reference',
    'artefacts.items[].imageCount': 'number',
    'artefacts.items[].inventoryNumber': 'text',
    'artefacts.items[].maker': 'text',
    'artefacts.items[].medium': 'text',
    'artefacts.items[].objectType.id': 'reference',
    'artefacts.items[].objectType.name': 'text',
    'artefacts.items[].placeOfProduction.text': 'text',
    'artefacts.items[].primaryImage.urls': 'image',
    'artefacts.items[].title': 'text',
    'artefacts.page': 'number',
    'artefacts.pageSize': 'number',
    'artefacts.total': 'number',
  },
  variables: {
    search: { type: 'text', required: false },
    sort: { type: 'text', required: false },
    direction: { type: 'text', required: false },
    group: { type: 'text', required: false },
    page: { type: 'number', required: false },
    pageSize: { type: 'number', required: false },
  },
}

/** What `GET /api/queries/artefact-detail/description` returns, as the API produced it. */
export const artefactDetailDescription: QueryDescription = {
  fields: {
    "artefact.acquisition.date": "date",
    "artefact.acquisition.lot": "text",
    "artefact.acquisition.method": "text",
    "artefact.acquisition.note": "text",
    "artefact.acquisition.price": "money",
    "artefact.acquisition.source.id": "reference",
    "artefact.acquisition.source.kind": "text",
    "artefact.acquisition.source.name": "text",
    "artefact.catalogueEntry": "text",
    "artefact.catalogueEntryLanguage": "text",
    "artefact.collectionGroup.id": "reference",
    "artefact.collectionGroup.name": "text",
    "artefact.creationDate": "historical-date",
    "artefact.description": "text",
    "artefact.descriptionLanguage": "text",
    "artefact.dimensions": "text",
    "artefact.dynasty.id": "reference",
    "artefact.dynasty.name": "text",
    "artefact.exhibitions[]": "text",
    "artefact.holder.id": "reference",
    "artefact.holder.kind": "text",
    "artefact.holder.name": "text",
    "artefact.id": "reference",
    "artefact.images[]": "list",
    "artefact.images[].caption": "text",
    "artefact.images[].format": "text",
    "artefact.images[].isPrimary": "boolean",
    "artefact.images[].mediaAssetId": "reference",
    "artefact.images[].originalFileName": "text",
    "artefact.images[].sizeBytes": "number",
    "artefact.images[].uploadedAt": "date",
    "artefact.images[].urls": "image",
    "artefact.inventoryNumber": "text",
    "artefact.maker": "text",
    "artefact.medium": "text",
    "artefact.objectType.id": "reference",
    "artefact.objectType.name": "text",
    "artefact.placeOfProduction.region.id": "reference",
    "artefact.placeOfProduction.region.name": "text",
    "artefact.placeOfProduction.text": "text",
    "artefact.primaryImage.caption": "text",
    "artefact.primaryImage.urls": "image",
    "artefact.provenance[]": "list",
    "artefact.provenance[].note": "text",
    "artefact.provenance[].period": "historical-date",
    "artefact.provenance[].region.id": "reference",
    "artefact.provenance[].region.name": "text",
    "artefact.storageLocation.id": "reference",
    "artefact.storageLocation.name": "text",
    "artefact.title": "text",
    "artefact.valuation": "money",
    "language": "text",
  },
  variables: {
    "id": { type: "reference", required: true },
  },
}

type Variables = Record<string, unknown>

/**
 * Routes for a page that reads the `artefact-list` query: its description, and the GraphQL endpoint answering with
 * what `respond` returns for the variables sent (the `data` of the response).
 */
export function artefactListRoutes(respond: (variables: Variables) => unknown) {
  return {
    'GET /api/queries/artefact-list/description': () => artefactListDescription,
    'POST /graphql': (_url: URL, init?: RequestInit) => ({ data: respond((JSON.parse(String(init?.body)) as { variables: Variables }).variables) }),
  }
}

/** The variables the page sent to `/graphql`, from the calls the fetch mock recorded. */
export function sentVariables(calls: { method: string; url: URL; body?: unknown }[]): Variables[] {
  return calls.filter((c) => c.method === 'POST' && c.url.pathname === '/graphql').map((c) => (c.body as { variables: Variables }).variables)
}

/**
 * Routes for a page that reads the `artefact-detail` query: its description, and the GraphQL endpoint answering with
 * `{ language, artefact }` (an `artefact` of null for an unknown identifier).
 */
export function artefactDetailRoutes(artefact: unknown, language = 'en') {
  return {
    'GET /api/queries/artefact-detail/description': () => artefactDetailDescription,
    'POST /graphql': () => ({ data: { language, artefact } }),
  }
}
