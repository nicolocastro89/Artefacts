import type { OperationId } from '../../api/fieldTypes.generated'
import type { QueryDescription } from './dataSources'
import { fieldTypesFor } from './dataSources'
import type { PageNode } from './page'
import { qualify, typeAt } from './paths'
import { getWidget } from './registry'
import type { FieldType } from './types'
import { validateBindings } from './validation'

export interface PageProblem {
  /** Where in the definition, in the form the server uses for its own errors: `root.children[0].bindings.src`. */
  path: string
  message: string
}

/** What the lint knows of each stored query: its description, `null` when the query does not exist, absent while unknown. */
export type QueryDescriptions = Readonly<Record<string, QueryDescription | null | undefined>>

interface Scope {
  fieldTypes?: Readonly<Record<string, FieldType>>
  prefix: string
}

/** The stored queries a page reads, so a caller can fetch their descriptions before linting. */
export function queryIdsOf(root: PageNode): string[] {
  const ids = new Set<string>()
  const walk = (node: PageNode) => {
    if (node.type === 'container' && node.dataSource.kind === 'query') ids.add(node.dataSource.queryId)
    if (node.type !== 'widget') node.children.forEach(walk)
  }
  walk(root)
  return [...ids]
}

/**
 * Finds what the renderer would report for a page without rendering it: widgets that are not registered, binding slots
 * that are missing or mistyped, and paths the data source does not return. The server checks only the structure of a
 * definition, so this is where such problems are found while the page is edited.
 */
export function lintPage(root: PageNode, descriptions: QueryDescriptions): PageProblem[] {
  const problems: PageProblem[] = []

  const walk = (node: PageNode, path: string, scope: Scope | null) => {
    switch (node.type) {
      case 'container': {
        const source = node.dataSource
        let next: Scope
        if (source.kind === 'query') {
          const description = descriptions[source.queryId]
          if (description === null) problems.push({ path: `${path}.dataSource.queryId`, message: `The stored query "${source.queryId}" does not exist.` })
          next = { fieldTypes: description?.fields, prefix: '' }
        } else if (source.kind === 'endpoint') next = { fieldTypes: fieldTypesFor(source.operationId as OperationId), prefix: '' }
        else next = { fieldTypes: fieldTypesFor(source.typedAs), prefix: '' }
        node.children.forEach((child, i) => walk(child, `${path}.children[${i}]`, next))
        break
      }
      case 'layout':
        node.children.forEach((child, i) => walk(child, `${path}.children[${i}]`, scope))
        break
      case 'repeat': {
        if (!scope) {
          problems.push({ path, message: 'A repeat must be inside a container that provides data.' })
          break
        }
        if (scope.fieldTypes && typeAt(scope.fieldTypes, scope.prefix, node.itemsPath) !== 'list')
          problems.push({ path: `${path}.itemsPath`, message: `"${node.itemsPath}" is not a list in this data source.` })
        const next = { fieldTypes: scope.fieldTypes, prefix: qualify(scope.prefix, node.itemsPath) }
        node.children.forEach((child, i) => walk(child, `${path}.children[${i}]`, next))
        break
      }
      case 'widget': {
        const widget = getWidget(node.widgetId)
        if (!widget) {
          problems.push({ path: `${path}.widgetId`, message: `Unknown widget "${node.widgetId}".` })
          break
        }
        if (!scope) {
          problems.push({ path, message: 'A widget must be inside a container that provides data.' })
          break
        }
        for (const problem of validateBindings(widget, node.bindings, scope.fieldTypes, scope.prefix))
          problems.push({ path: problem.slot ? `${path}.bindings.${problem.slot}` : path, message: problem.message })
        break
      }
    }
  }

  walk(root, 'root', null)
  return problems
}
