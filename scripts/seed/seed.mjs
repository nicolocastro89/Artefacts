#!/usr/bin/env node
// Seeds the development stack with a sample collection through the public API, so every record
// passes the same validation, storage and image pipeline as data entered by a user.
//
// Usage (API running, e.g. `dotnet run --project backend/src/Artefacts.Api`):
//   node scripts/seed/seed.mjs [--api http://localhost:5032]
// The API URL can also come from ARTEFACTS_API_URL. Rerunning is safe: regions are matched by name
// and artefacts whose inventory number already exists are skipped. To start again from empty, reset
// the stack's volumes: docker compose -f docker/docker-compose.yml down -v
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
    const { status, body } = await call('POST', '/api/regions', { json: { name: r.name, parentId } })
    if (status === 201) created++
    ids.set(r.name, body.id)
  }
  console.log(`Regions: ${created} created, ${regions.length - created} already present.`)
  return ids
}

async function exists(inventoryNumber) {
  const query = new URLSearchParams({ search: inventoryNumber, pageSize: '100' })
  const { body } = await call('GET', `/api/artefacts?${query}`)
  return body.items.some((i) => i.inventoryNumber === inventoryNumber)
}

function toRequest(a, regionIds) {
  return {
    inventoryNumber: a.inventoryNumber,
    title: a.title,
    maker: a.maker,
    creationDate: dateInput(a.creationDate),
    medium: a.medium,
    dimensions: a.dimensions,
    description: a.description,
    acquisition: a.acquisition,
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

  const totals = { created: 0, skipped: 0, failed: 0, images: 0 }
  for (const a of artefacts) {
    const label = `${a.inventoryNumber.padEnd(13)} ${a.title.length > 48 ? `${a.title.slice(0, 47)}…` : a.title}`
    try {
      if (await exists(a.inventoryNumber)) {
        totals.skipped++
        console.log(`  skip    ${label} (already present)`)
        continue
      }
      const { body: created } = await call('POST', '/api/artefacts', { json: toRequest(a, regionIds) })
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
  console.log(`Artefacts: ${totals.created} created (${totals.images} images), ${totals.skipped} skipped, ${totals.failed} failed, in ${seconds}s.`)
  if (totals.failed > 0) process.exit(1)
}

await main()
