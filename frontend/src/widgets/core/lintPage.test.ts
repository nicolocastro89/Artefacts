import artefactDetailFile from '../../../../backend/src/Artefacts.Infrastructure/Pages/Definitions/artefact-detail.json'
import artefactListFile from '../../../../backend/src/Artefacts.Infrastructure/Pages/Definitions/artefact-list.json'
import { artefactDetailDescription, artefactListDescription } from '../../test/queryFixtures'
import '../builtin'
import { lintPage, queryIdsOf } from './lintPage'
import type { PageNode } from './page'

const descriptions = { 'artefact-list': artefactListDescription, 'artefact-detail': artefactDetailDescription }

const page = (children: PageNode[], queryId = 'artefact-list'): PageNode => ({
  type: 'container',
  id: 'c',
  dataSource: { kind: 'query', queryId },
  children,
})

describe('lintPage', () => {
  it('finds nothing wrong with the shipped system pages', () => {
    expect(lintPage(artefactListFile.root as PageNode, descriptions)).toEqual([])
    expect(lintPage(artefactDetailFile.root as PageNode, descriptions)).toEqual([])
  })

  it('names a widget that is not registered, with its path', () => {
    const root = page([{ type: 'widget', id: 'w', widgetId: 'carousel', bindings: {} }])

    expect(lintPage(root, descriptions)).toEqual([{ path: 'root.children[0].widgetId', message: 'Unknown widget "carousel".' }])
  })

  it('names a binding path the stored query does not return', () => {
    const root = page([{ type: 'widget', id: 'w', widgetId: 'heading', bindings: { text: 'artefacts.valuation' } }])

    const [problem] = lintPage(root, descriptions)

    expect(problem?.path).toBe('root.children[0].bindings.text')
    expect(problem?.message).toContain('"artefacts.valuation" is not a field')
  })

  it('names a slot bound to a value of the wrong type', () => {
    const root = page([{ type: 'widget', id: 'w', widgetId: 'image', bindings: { src: 'artefacts.total' } }])

    const [problem] = lintPage(root, descriptions)

    expect(problem?.path).toBe('root.children[0].bindings.src')
    expect(problem?.message).toMatch(/expects image but .* is number/)
  })

  it('names a required slot with no binding', () => {
    const root = page([{ type: 'widget', id: 'w', widgetId: 'image', bindings: {} }])

    expect(lintPage(root, descriptions).map((p) => p.path)).toEqual(['root.children[0].bindings.src'])
  })

  it('checks the bindings inside a repeat against the item path', () => {
    const ok = page([{ type: 'repeat', id: 'r', itemsPath: 'artefacts.items[]', children: [{ type: 'widget', id: 'w', widgetId: 'heading', bindings: { text: 'title' } }] }])
    const wrong = page([{ type: 'repeat', id: 'r', itemsPath: 'artefacts.items[]', children: [{ type: 'widget', id: 'w', widgetId: 'heading', bindings: { text: 'nonsense' } }] }])
    const notAList = page([{ type: 'repeat', id: 'r', itemsPath: 'artefacts.total', children: [] }])

    expect(lintPage(ok, descriptions)).toEqual([])
    expect(lintPage(wrong, descriptions).map((p) => p.path)).toEqual(['root.children[0].children[0].bindings.text'])
    expect(lintPage(notAList, descriptions).map((p) => p.path)).toEqual(['root.children[0].itemsPath'])
  })

  it('reports a stored query that does not exist, and skips typing while a description is not known yet', () => {
    const root = page([{ type: 'widget', id: 'w', widgetId: 'heading', bindings: { text: 'anything' } }], 'gone')

    expect(lintPage(root, { gone: null }).map((p) => p.path)).toEqual(['root.dataSource.queryId'])
    expect(lintPage(root, {})).toEqual([])
  })

  it('lists each stored query a page reads once', () => {
    const root = page([page([], 'artefact-detail'), page([], 'artefact-list')])

    expect(queryIdsOf(root).sort()).toEqual(['artefact-detail', 'artefact-list'])
  })
})
