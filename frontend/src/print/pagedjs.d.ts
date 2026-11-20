declare module 'pagedjs' {
  export interface Flow {
    total: number
  }

  export class Previewer {
    constructor(options?: Record<string, unknown>)
    preview(content?: Node | DocumentFragment, stylesheets?: (string | Record<string, string>)[], renderTo?: HTMLElement): Promise<Flow>
    /** Lays out pages; each page owns a ResizeObserver until destroyed. */
    chunker: { pages: { destroy(): void }[]; destroy(): void }
    /** Owns the stylesheets (including the @page rules) injected into <head>. */
    polisher: { destroy(): void }
  }

  export class Handler {
    constructor(chunker: unknown, polisher: unknown, caller: unknown)
    chunker: { source?: ParentNode } & Record<string, unknown>
    afterPageLayout?(pageElement: HTMLElement, page: unknown, breakToken: unknown, chunker: unknown): void
  }

  export function registerHandlers(...handlers: (new (chunker: unknown, polisher: unknown, caller: unknown) => Handler)[]): void
}
