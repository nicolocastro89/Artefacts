import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { parse, print } from 'graphql'
import rulesFile from '../test/builderRules.json'
import { toDocument } from './toDocument'
import { toModel } from './fromDocument'
import type { QueryModel } from './model'
import type { BuilderRules } from './rules'
import { declareVariable, pruneVariables, removeVariable, variableUses } from './variables'

const rules = rulesFile as unknown as BuilderRules

// The tests run from the frontend folder; the system queries are the backend's embedded files.
const system = (id: string) => readFileSync(join(process.cwd(), '..', 'backend/src/Artefacts.Infrastructure/Graph/Queries', `${id}.graphql`), 'utf8')

function model(document: string): QueryModel {
  const result = toModel(document, rules)
  if (!result.ok) throw new Error(`Not representable: ${result.reasons.join(' ')}`)
  return result.model
}

const reasonsOf = (document: string) => {
  const result = toModel(document, rules)
  if (result.ok) throw new Error('Expected the document to be refused.')
  return result.reasons
}

/** The same document in normal form: what printing a parsed document gives. */
const normal = (document: string) => print(parse(document))

describe('round trip', () => {
  it.each(['artefact-list', 'artefact-detail'])('shows the system query %s in blocks and prints it back unchanged', (id) => {
    const document = system(id)

    const read = model(document)

    expect(normal(toDocument(read))).toBe(normal(document))
    expect(model(toDocument(read))).toEqual({ ...read, comments: [] })
  })

  it('reads the structure of the list query', () => {
    const read = model(system('artefact-list'))

    const [root] = read.roots
    expect(root?.name).toBe('artefacts')
    expect(Object.keys(root!.args)).toEqual(['search', 'sort', 'direction', 'group', 'page', 'pageSize'])
    expect(root?.args.search).toEqual({ kind: 'variable', name: 'search' })
    expect(root?.selection.map((s) => s.name)).toEqual(['total', 'page', 'pageSize', 'groups', 'items'])
    expect(read.variables.find((v) => v.name === 'page')?.type).toBe('Int')
  })

  it('shows nested objects and lists of the detail query, and its second root', () => {
    const read = model(system('artefact-detail'))

    expect(read.roots.map((r) => r.name)).toEqual(['language', 'artefact'])
    const artefact = read.roots[1]!
    const acquisition = artefact.selection.find((s) => s.name === 'acquisition')!
    expect(acquisition.children.find((c) => c.name === 'price')?.children.map((c) => c.name)).toEqual(['amount', 'currency'])
    expect(artefact.selection.find((s) => s.name === 'provenance')?.children.map((c) => c.name)).toEqual(['region', 'period', 'note'])
    expect(artefact.args.id).toEqual({ kind: 'variable', name: 'id' })
  })

  it('keeps the comments of a document so a rewrite can warn about them', () => {
    expect(model(system('artefact-list')).comments).toEqual([' name: Artefact list'])
  })

  it('reads filters in and/or groups with literals, lists and variables, and orders, and prints them back', () => {
    const document = `query Gifts($search: String, $method: String) {
      artefacts(
        search: $search
        pageSize: 10
        where: { and: [
          { acquisitionMethod: { eq: $method } }
          { or: [ { imageCount: { gt: 0 } }, { title: { in: ["Bowl", "Plate"] } } ] }
        ] }
        order: [{ title: ASC }, { inventoryNumber: DESC }]
      ) { total items { title } }
    }`

    const read = model(document)
    const root = read.roots[0]!

    expect(root.where).toEqual({
      type: 'group',
      op: 'and',
      items: [
        { type: 'condition', field: 'acquisitionMethod', operator: 'eq', value: { kind: 'variable', name: 'method' } },
        {
          type: 'group',
          op: 'or',
          items: [
            { type: 'condition', field: 'imageCount', operator: 'gt', value: { kind: 'literal', value: 0 } },
            { type: 'condition', field: 'title', operator: 'in', value: { kind: 'list', items: ['Bowl', 'Plate'] } },
          ],
        },
      ],
    })
    expect(root.order).toEqual([
      { field: 'title', direction: 'ASC' },
      { field: 'inventoryNumber', direction: 'DESC' },
    ])
    expect(normal(toDocument(read))).toBe(normal(document))
  })

  it('prints a single condition as the field alone, as the spec shows', () => {
    const read = model('{ artefacts { total } }')
    read.roots[0]!.where = {
      type: 'group',
      op: 'and',
      items: [
        { type: 'condition', field: 'acquisitionMethod', operator: 'eq', value: { kind: 'literal', value: 'Gift' } },
        { type: 'condition', field: 'imageCount', operator: 'gt', value: { kind: 'literal', value: 0 } },
      ],
    }

    expect(toDocument(read)).toContain('where: { and: [{ acquisitionMethod: { eq: "Gift" } }, { imageCount: { gt: 0 } }] }')

    read.roots[0]!.where!.items.pop()
    expect(toDocument(read)).toContain('where: { acquisitionMethod: { eq: "Gift" } }')
  })

  it('reads several operators on one field and several fields in one object as an and', () => {
    const read = model('{ artefacts { total } }')
    const document = '{ artefacts(where: { imageCount: { gt: 1, lt: 5 }, title: { startsWith: "A" } }) { total } }'

    const items = model(document).roots[0]!.where!.items

    expect(items.map((i) => (i.type === 'condition' ? `${i.field} ${i.operator}` : 'group'))).toEqual(['imageCount gt', 'imageCount lt', 'title startsWith'])
    expect(read.roots[0]?.where).toBeNull()
  })

  it('prints numbers, decimals, booleans and text as the matching literals', () => {
    const read = model('{ artefacts { total } }')
    read.roots[0]!.where = {
      type: 'group',
      op: 'and',
      items: [
        { type: 'condition', field: 'valuationAmount', operator: 'gte', value: { kind: 'literal', value: 12.5 } },
        { type: 'condition', field: 'imageCount', operator: 'eq', value: { kind: 'literal', value: 3 } },
        { type: 'condition', field: 'title', operator: 'eq', value: { kind: 'literal', value: 'a "quoted" title' } },
      ],
    }

    const reread = model(toDocument(read))

    expect(reread.roots[0]?.where).toEqual(read.roots[0]?.where)
  })
})

describe('documents the blocks cannot show', () => {
  it.each([
    ['an alias', '{ artefacts { items { name: title } } }', /alias "name"/],
    ['a fragment definition', 'fragment F on Artefact { title } { artefacts { items { ...F } } }', /fragment "F"/],
    ['an inline fragment', '{ artefacts { items { ... on Artefact { title } } } }', /inline fragment/],
    ['a directive', '{ artefacts { items { title @include(if: true) } } }', /directive/],
    ['a mutation', 'mutation { artefacts { total } }', /mutation/],
    ['two operations', 'query A { language } query B { language }', /more than one operation/],
    ['a field the schema does not have', '{ artefacts { items { nonsense } } }', /no field "nonsense"/],
    ['an argument the root does not have', '{ artefacts(colour: "red") { total } }', /no argument "colour"/],
    ['a root that does not exist', '{ nothing { id } }', /not a field the query can start from/],
    ['arguments on a nested field', '{ artefacts { items(first: 1) { title } } }', /arguments/],
    ['a filter passed as a variable', 'query($w: ArtefactFilterInput) { artefacts(where: $w) { total } }', /where.*variable/],
    ['a field that is not filterable', '{ artefacts(where: { images: { eq: "x" } }) { total } }', /cannot filter on "images"/],
    ['an operator the field does not have', '{ artefacts(where: { imageCount: { contains: 1 } }) { total } }', /no operator "contains"/],
    ['a syntax error', '{ artefacts { ', /syntax error/],
    ['an object without a selection', '{ artefacts { items } }', /needs a selection/],
    ['a variable that is not declared', '{ artefacts(search: $s) { total } }', /\$s is used but not declared/],
  ])('refuses %s and says why', (_name, document, reason) => {
    expect(reasonsOf(document).join(' ')).toMatch(reason)
  })

  it('lists every reason, not only the first', () => {
    expect(reasonsOf('{ artefacts { items { name: title @include(if: true) } } }').length).toBeGreaterThanOrEqual(2)
  })
})

describe('variables', () => {
  const empty = () => model('{ artefacts { total } }')

  it('takes the type of the place: an argument with a default is nullable, a required one is not', () => {
    const page = declareVariable(empty(), rules, 'p', { kind: 'argument', root: 'artefacts', argument: 'page' })
    const id = declareVariable(empty(), rules, 'i', { kind: 'argument', root: 'artefact', argument: 'id' })
    const list = declareVariable(empty(), rules, 'm', { kind: 'condition', root: 'artefacts', field: 'acquisitionMethod', operator: 'in' })
    const text = declareVariable(empty(), rules, 't', { kind: 'condition', root: 'artefacts', field: 'acquisitionMethod', operator: 'eq' })

    expect(page.ok && page.model.variables[0]?.type).toBe('Int')
    expect(id.ok && id.model.variables[0]?.type).toBe('UUID!')
    expect(list.ok && list.model.variables[0]?.type).toBe('[String]')
    expect(text.ok && text.model.variables[0]?.type).toBe('String')
  })

  it('refuses a variable used in two places that take different types', () => {
    const first = declareVariable(empty(), rules, 'x', { kind: 'argument', root: 'artefacts', argument: 'page' })
    if (!first.ok) throw new Error('unexpected')

    const second = declareVariable(first.model, rules, 'x', { kind: 'argument', root: 'artefacts', argument: 'search' })
    const again = declareVariable(first.model, rules, 'x', { kind: 'argument', root: 'artefacts', argument: 'pageSize' })

    expect(second).toEqual({ ok: false, reason: '$x is declared as Int, but this place takes String.' })
    expect(again.ok).toBe(true)
  })

  it('refuses to remove a variable that is in use, naming where, and removes one that is not', () => {
    const read = model('query($s: String, $m: String, $unused: Int) { artefacts(search: $s, where: { acquisitionMethod: { eq: $m } }) { total } }')

    expect(variableUses(read, 's')).toEqual(['artefacts: search'])
    expect(removeVariable(read, 'm')).toEqual({ ok: false, reason: '$m is used by artefacts: acquisitionMethod eq.' })
    const removed = removeVariable(read, 'unused')
    expect(removed.ok && removed.model.variables.map((v) => v.name)).toEqual(['s', 'm'])
  })

  it('drops variables nothing uses after a block is removed', () => {
    const read = model('query($s: String) { artefacts(search: $s) { total } }')
    delete read.roots[0]!.args.search

    expect(pruneVariables(read).variables).toEqual([])
  })
})
