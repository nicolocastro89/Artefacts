#!/usr/bin/env node
// Seeds the development stack with a sample collection through the public API, so every record
// passes the same validation, storage and image pipeline as data entered by a user.
//
// Usage (API running, e.g. `dotnet run --project backend/src/Artefacts.Api`):
//   node scripts/seed/seed.mjs [--api http://localhost:5032]
// The API URL can also come from ARTEFACTS_API_URL. Rerunning is safe: regions, object types, list entries and
// parties are matched by name, and artefacts whose inventory number already exists are skipped.
// To start again from empty, reset the stack's volumes: docker compose -f docker/docker-compose.yml down -v
import { catalogue, descriptionsIt, dynastyNamesEn, groupNamesEn, italianOnly, objectTypes, regionNamesIt } from './data-catalogue.mjs'
import { artefacts, regions } from './data.mjs'
import { encodePng, encodeTiff, renderImage } from './images.mjs'

const argApi = process.argv.indexOf('--api')
const API = (argApi > 0 ? process.argv[argApi + 1] : process.env.ARTEFACTS_API_URL ?? 'http://localhost:5032').replace(/\/$/, '')

class ApiFailure extends Error {
  constructor(what, status, body) {
    const details = body?.errors
      ? Object.entries(body.errors).map(([field, messages]) => `${field}: ${messages.join(' ')}`).join('; ')
      : (body?.detail ?? body?.title ?? '')
    super(`${what} failed (${status})${details ? `: ${details}` : ''}`)
  }
}

async function call(method, path, { json, form } = {}) {
  const response = await fetch(`${API}${path}`, {
    method,
    headers: json ? { 'Content-Type': 'application/json', Accept: 'application/json' } : { Accept: 'application/json' },
    body: json ? JSON.stringify(json) : form,
  })
  const text = await response.text()
  const body = text ? JSON.parse(text) : null
  if (!response.ok) throw new ApiFailure(`${method} ${path}`, response.status, body)
  return { status: response.status, body }
}

const dateInput = (d) => (d == null ? null : typeof d === 'string' ? { text: d, earliestYear: null, latestYear: null } : d)

async function seedRegions() {
  const ids = new Map()
  let created = 0
  for (const r of regions) {
    const parentId = r.parent ? ids.get(r.parent) : null
    if (r.parent && !parentId) throw new Error(`Region "${r.name}" lists parent "${r.parent}", which is not defined before it.`)
    const names = { en: r.name, ...(regionNamesIt[r.name] ? { it: regionNamesIt[r.name] } : {}) }
    const { status, body } = await call('POST', '/api/regions', { json: { names, parentId } })
    if (status === 201) created++
    ids.set(r.name, body.id)
  }
  console.log(`Regions: ${created} created, ${regions.length - created} already present.`)
  return ids
}

async function seedObjectTypes() {
  const ids = new Map()
  let created = 0
  for (const names of objectTypes) {
    const { status, body } = await call('POST', '/api/object-types', { json: { names } })
    if (status === 201) created++
    ids.set(names.en, body.id)
  }
  console.log(`Object types: ${created} created, ${objectTypes.length - created} already present.`)
  return ids
}

/** People among the seed's sources: bequests, gifts from named people, families and private collectors. */
const isPerson = (name) => /bequest of|gift of mr|family|private collector/i.test(name)

/**
 * The collection groups, dynasties, storage locations and parties the records use, created first (adding returns an
 * existing entry with the same name, so variants such as "Olanda" link to "OLANDA"). Returns a lookup by text.
 */
async function seedLists() {
  const records = artefacts.map((a) => ({ a, c: catalogue[a.inventoryNumber] ?? {} }))
  const lookups = { group: new Map(), dynasty: new Map(), location: new Map(), party: new Map() }
  const counts = { group: 0, dynasty: 0, location: 0, party: 0 }
  const add = async (kind, text, path, json) => {
    if (text == null || lookups[kind].has(text)) return
    const { status, body } = await call('POST', path, { json })
    if (status === 201) counts[kind]++
    lookups[kind].set(text, body.id)
  }
  for (const { a, c } of records) {
    const groupEn = c.group && groupNamesEn[c.group.trim().toUpperCase()]
    await add('group', c.group, '/api/lists/collection-groups', { names: { it: c.group, ...(groupEn ? { en: groupEn } : {}) } })
    const dynastyEn = c.dynasty && dynastyNamesEn[c.dynasty]
    await add('dynasty', c.dynasty, '/api/lists/dynasties', { names: { it: c.dynasty, ...(dynastyEn ? { en: dynastyEn } : {}) } })
    await add('location', c.location, '/api/lists/storage-locations', { name: c.location })
    await add('party', c.holder, '/api/parties', { name: c.holder, kind: 'organisation' })
    const source = a.acquisition?.source
    await add('party', source, '/api/parties', { name: source, kind: isPerson(source ?? '') ? 'person' : 'organisation' })
  }
  console.log(
    `Lists: ${counts.group} collection groups, ${counts.dynasty} dynasties, ${counts.location} storage locations and ${counts.party} parties created ` +
      `(the rest already present).`,
  )
  return lookups
}

async function exists(inventoryNumber) {
  // The artefact-list system query, as the list page runs it.
  const { body } = await call('POST', '/graphql', { json: { id: 'artefact-list', variables: { search: inventoryNumber, pageSize: 100 } } })
  return body.data.artefacts.items.some((i) => i.inventoryNumber === inventoryNumber)
}

/** Classification, origin and holding from data-catalogue.mjs, as request fields. */
function catalogueFields(a, regionIds, typeIds, lists) {
  const c = catalogue[a.inventoryNumber]
  if (!c) return {}
  const region = c.place?.region ? regionIds.get(c.place.region) : null
  if (c.place?.region && !region) throw new Error(`${a.inventoryNumber}: unknown region "${c.place.region}" for the place of production.`)
  const typeId = c.type ? typeIds.get(c.type) : null
  if (c.type && !typeId) throw new Error(`${a.inventoryNumber}: unknown object type "${c.type}".`)
  return {
    objectTypeId: typeId,
    collectionGroupId: c.group ? lists.group.get(c.group) : null,
    placeOfProduction: c.place ? { text: c.place.text ?? null, regionId: region } : null,
    dynastyId: c.dynasty ? lists.dynasty.get(c.dynasty) : null,
    exhibitions: c.exhibitions ?? [],
    catalogueEntries: c.entry ? { en: c.entry } : null,
    holderId: c.holder ? lists.party.get(c.holder) : null,
    storageLocationId: c.location ? lists.location.get(c.location) : null,
    valuation: c.valuation ?? null,
  }
}

function toRequest(a, regionIds, typeIds, lists) {
  const c = catalogue[a.inventoryNumber] ?? {}
  const { source, ...acquired } = a.acquisition ?? {}
  const acquisition = a.acquisition
    ? { ...acquired, sourceId: source ? lists.party.get(source) : null, ...(c.price ? { price: c.price } : {}), lot: c.lot ?? null, note: c.note ?? null }
    : null
  return {
    ...catalogueFields(a, regionIds, typeIds, lists),
    inventoryNumber: a.inventoryNumber,
    title: a.title,
    maker: a.maker,
    creationDate: dateInput(a.creationDate),
    medium: a.medium,
    dimensions: a.dimensions,
    descriptions: {
      ...(a.description && !italianOnly.includes(a.inventoryNumber) ? { en: a.description } : {}),
      ...(descriptionsIt[a.inventoryNumber] ? { it: descriptionsIt[a.inventoryNumber] } : {}),
    },
    acquisition,
    provenance: a.provenance.map((p) => {
      const regionId = regionIds.get(p.region)
      if (!regionId) throw new Error(`${a.inventoryNumber}: unknown region "${p.region}" in provenance.`)
      return { regionId, period: dateInput(p.period), note: p.note }
    }),
  }
}

async function uploadImages(a, id) {
  if (a.images.length === 0) return 0
  const form = new FormData()
  const names = a.images.map((spec, i) => {
    const rgb = renderImage(spec.kind, spec.width, spec.height, `${a.inventoryNumber}#${i}`, spec.colours)
    const [bytes, type, ext] = spec.format === 'tiff' ? [encodeTiff(spec.width, spec.height, rgb), 'image/tiff', 'tif'] : [encodePng(spec.width, spec.height, rgb), 'image/png', 'png']
    const name = `${a.inventoryNumber.toLowerCase()}-${String(i + 1).padStart(2, '0')}.${ext}`
    form.append('files', new Blob([bytes], { type }), name)
    return name
  })
  const { body: detail } = await call('POST', `/api/artefacts/${id}/images`, { form })

  const assetFor = (name) => {
    const image = detail.images.find((im) => im.originalFileName === name)
    if (!image) throw new Error(`${a.inventoryNumber}: uploaded "${name}" is missing from the response.`)
    return image.mediaAssetId
  }
  for (const [i, spec] of a.images.entries()) {
    if (spec.caption) await call('PATCH', `/api/artefacts/${id}/images/${assetFor(names[i])}`, { json: { caption: spec.caption } })
  }
  if (a.primary != null) await call('PUT', `/api/artefacts/${id}/images/${assetFor(names[a.primary])}/primary`)
  return a.images.length
}

async function main() {
  try {
    await call('GET', '/health')
  } catch (e) {
    console.error(`Cannot reach the API at ${API} (${e.cause?.code ?? e.message}).`)
    console.error('Start the stack and the API first, or pass --api <url>. See README.md.')
    process.exit(2)
  }
  console.log(`Seeding ${API}`)
  const started = Date.now()
  const regionIds = await seedRegions()
  const typeIds = await seedObjectTypes()
  const lists = await seedLists()

  const totals = { created: 0, skipped: 0, failed: 0, images: 0 }
  for (const a of artefacts) {
    const label = `${a.inventoryNumber.padEnd(13)} ${a.title.length > 48 ? `${a.title.slice(0, 47)}…` : a.title}`
    try {
      if (await exists(a.inventoryNumber)) {
        totals.skipped++
        console.log(`  skip    ${label} (already present)`)
        continue
      }
      const { body: created } = await call('POST', '/api/artefacts', { json: toRequest(a, regionIds, typeIds, lists) })
      const images = await uploadImages(a, created.id)
      totals.created++
      totals.images += images
      console.log(`  create  ${label}${images ? ` (+${images} image${images > 1 ? 's' : ''})` : ''}`)
    } catch (e) {
      totals.failed++
      console.error(`  FAILED  ${label}\n          ${e.message}`)
    }
  }

  const seconds = ((Date.now() - started) / 1000).toFixed(1)
  console.log(
    `Artefacts: ${totals.created} created (${totals.images} images), ${totals.skipped} skipped, ${totals.failed} failed, in ${seconds}s.`,
  )
  if (totals.failed > 0) process.exit(1)
}

await main()
