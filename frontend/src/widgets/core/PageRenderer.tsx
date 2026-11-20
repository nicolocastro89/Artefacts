import { useQuery } from '@tanstack/react-query'
import { createContext, useContext, type ReactNode } from 'react'
import { ApiError } from '../../api/problem'
import { usePrintMode } from '../../print/PrintContext'
import { fetchOperation, fieldTypesFor, resolveParams, type ParamContext } from './dataSources'
import type { ContainerNode, LayoutNode, PageDefinition, PageNode, RepeatNode, WidgetNode } from './page'
import { qualify, resolvePath, typeAt } from './paths'
import { getWidget } from './registry'
import type { BoundValue, FieldType } from './types'
import { validateBindings } from './validation'
import { WidgetConfigError } from './WidgetConfigError'

/** The data a node binds against: the value, its field-type map, and the path prefix of this scope within it. */
interface Scope {
  data: unknown
  fieldTypes?: Readonly<Record<string, FieldType>>
  prefix: string
}

const ScopeContext = createContext<Scope | null>(null)
const ParamsContext = createContext<ParamContext>({ route: {}, search: new URLSearchParams() })

export interface PageRendererProps {
  page: PageDefinition
  route?: Record<string, string | undefined>
  search?: URLSearchParams
}

/** Renders a serialisable page definition. Built-in and (future) user-defined pages use this same renderer. */
export function PageRenderer({ page, route = {}, search = new URLSearchParams() }: PageRendererProps) {
  return (
    <ParamsContext.Provider value={{ route, search }}>
      <div className="page" data-page-id={page.id}>
        <RenderNode node={page.root} />
      </div>
    </ParamsContext.Provider>
  )
}

function RenderNode({ node }: { node: PageNode }): ReactNode {
  switch (node.type) {
    case 'container':
      return <RenderContainer node={node} />
    case 'layout':
      return <RenderLayout node={node} />
    case 'repeat':
      return <RenderRepeat node={node} />
    case 'widget':
      return <RenderWidget node={node} />
  }
}

function RenderChildren({ nodes }: { nodes: PageNode[] }) {
  return nodes.map((child) => <RenderNode key={child.id} node={child} />)
}

function RenderContainer({ node }: { node: ContainerNode }) {
  const source = node.dataSource
  if (source.kind === 'static') {
    return (
      <ScopeContext.Provider value={{ data: source.data, fieldTypes: fieldTypesFor(source.typedAs), prefix: '' }}>
        <RenderChildren nodes={node.children} />
      </ScopeContext.Provider>
    )
  }
  if (source.kind === 'query') {
    return <div className="data-state data-state--unavailable">Custom queries are not available yet.</div>
  }
  return <EndpointContainer node={node} source={source} />
}

function EndpointContainer({ node, source }: { node: ContainerNode; source: Extract<ContainerNode['dataSource'], { kind: 'endpoint' }> }) {
  const ctx = useContext(ParamsContext)
  const params = resolveParams(source.params, ctx)
  const query = useQuery({
    queryKey: ['operation', source.operationId, params],
    queryFn: ({ signal }) => fetchOperation(source.operationId, params, signal),
    placeholderData: (previous) => previous,
  })

  if (query.isPending) return <div className="data-state data-state--loading" aria-busy="true">Loading…</div>
  if (query.isError) {
    const error = query.error
    const notFound = error instanceof ApiError && error.status === 404
    return (
      <div className="data-state data-state--error" role="alert">
        {notFound ? 'This record does not exist (it may have been deleted).' : `Could not load data: ${error.message}`}
      </div>
    )
  }
  return (
    <ScopeContext.Provider value={{ data: query.data, fieldTypes: fieldTypesFor(source.operationId), prefix: '' }}>
      <RenderChildren nodes={node.children} />
    </ScopeContext.Provider>
  )
}

function RenderLayout({ node }: { node: LayoutNode }) {
  const children = <RenderChildren nodes={node.children} />
  if (node.layout === 'section') {
    return (
      <section className="layout layout--section" data-node-id={node.id}>
        {node.title && <h2 className="layout__title">{node.title}</h2>}
        {children}
      </section>
    )
  }
  return (
    <div className={`layout layout--${node.layout}`} data-node-id={node.id}>
      {children}
    </div>
  )
}

function useScope(): Scope {
  const scope = useContext(ScopeContext)
  if (!scope) throw new Error('A widget or repeat node must be inside a container that provides data.')
  return scope
}

function RenderRepeat({ node }: { node: RepeatNode }) {
  const scope = useScope()
  const declared = typeAt(scope.fieldTypes, scope.prefix, node.itemsPath)
  if (scope.fieldTypes && declared !== 'list') {
    return (
      <WidgetConfigError
        widgetId={`repeat:${node.id}`}
        problems={[{ slot: 'items', message: `"${node.itemsPath}" is not a list in this data source.` }]}
      />
    )
  }
  const items = resolvePath(scope.data, node.itemsPath)
  const list = Array.isArray(items) ? items : []
  if (list.length === 0) return node.emptyText ? <p className="repeat-empty">{node.emptyText}</p> : null

  const itemPrefix = qualify(scope.prefix, node.itemsPath)
  return (
    <div className={`repeat layout layout--${node.layout ?? 'stack'}`} data-node-id={node.id}>
      {list.map((item, index) => (
        <ScopeContext.Provider key={keyOf(item, index)} value={{ data: item, fieldTypes: scope.fieldTypes, prefix: itemPrefix }}>
          <div className="repeat__item">
            <RenderChildren nodes={node.children} />
          </div>
        </ScopeContext.Provider>
      ))}
    </div>
  )
}

function keyOf(item: unknown, index: number): string | number {
  if (item && typeof item === 'object') {
    const o = item as Record<string, unknown>
    const id = o.id ?? o.mediaAssetId ?? o.regionId
    if (typeof id === 'string' || typeof id === 'number') return `${id}:${index}`
  }
  return index
}

function RenderWidget({ node }: { node: WidgetNode }) {
  const scope = useScope()
  const print = usePrintMode()
  const widget = getWidget(node.widgetId)
  if (!widget) {
    return <WidgetConfigError widgetId={node.widgetId} problems={[{ slot: '', message: `Unknown widget "${node.widgetId}".` }]} />
  }
  if (print && widget.print.hideInPrint) return null

  const problems = validateBindings(widget, node.bindings, scope.fieldTypes, scope.prefix)
  if (problems.length > 0) return <WidgetConfigError widgetId={widget.id} problems={problems} />

  const values: Record<string, BoundValue> = {}
  for (const [slot, path] of Object.entries(node.bindings)) {
    values[slot] = {
      value: resolvePath(scope.data, path),
      type: typeAt(scope.fieldTypes, scope.prefix, path),
      key: qualify(scope.prefix, path),
    }
  }
  const config = { ...widget.config, ...node.config }
  const Component = widget.component

  return (
    <div
      className={`widget widget--${widget.id}${widget.print.keepTogether ? ' keep-together' : ''}`}
      data-widget-id={widget.id}
      data-node-id={node.id}
    >
      <Component values={values} config={config} fieldTypes={scope.fieldTypes} />
    </div>
  )
}
