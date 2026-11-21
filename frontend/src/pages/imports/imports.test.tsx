import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router'
import { vi } from 'vitest'
import { languagesRoute, mockFetch } from '../../test/fetchMock'
import { renderWithProviders } from '../../test/render'
import type { ImportJob, ImportMapping, ImportReview } from './importApi'
import { ImportsPage } from './ImportsPage'
import { ImportWizard } from './ImportWizard'

const mapping: ImportMapping = {
  columns: [
    { column: 1, heading: 'Gruppo e numero', lines: [{ target: 'groupNumber', detector: 'groupNumber', continues: false }], rest: 'holder' },
    {
      column: 2,
      heading: 'Nome File',
      lines: [
        { target: 'title', detector: 'itemTitle', continues: true },
        { target: 'dimensions', detector: 'dimensions', continues: false },
      ],
      rest: 'description',
    },
    { column: 6, heading: 'Formato Immagine' },
  ],
  inventoryNumber: { pattern: 'CER-{group:2}-{item:2}', prefer: 'catalogueNumber' },
  language: 'it',
  acquisitionMethod: 'purchase',
}

function job(overrides: Partial<ImportJob> = {}): ImportJob {
  return {
    id: 'j1',
    fileName: 'ceramiche.xlsx',
    fileSize: 39_000_000,
    state: 'awaitingMapping',
    objectType: { id: 't1', name: 'Ceramics' },
    template: null,
    sheetName: 'CERAMICHE',
    sheetNames: ['CERAMICHE', 'Foglio1'],
    groupHeadings: 6,
    totalRows: 70,
    processedRows: 0,
    importedRows: 0,
    skippedRows: 0,
    problemRows: 0,
    error: null,
    warnings: [],
    mapping,
    decisions: { places: [], dates: [], lists: [] },
    createdAt: '2026-11-20T10:00:00Z',
    updatedAt: '2026-11-20T10:00:00Z',
    committedAt: null,
    finishedAt: null,
    ...overrides,
  }
}

const review: ImportReview = {
  counts: { total: 3, ready: 1, skipped: 1, problems: 1, imported: 0 },
  rows: [
    {
      sheetRow: 3,
      status: 'ready',
      inventoryNumber: 'CER-01-01',
      title: 'Coppa Celadon',
      groupLabel: 'CINA',
      fields: [
        { field: 'title', value: 'Coppa Celadon' },
        { field: 'dimensions', value: 'diam 17,6 x h 7' },
      ],
      pictures: [{ index: 1, name: 'Immagine 1.jpg', supported: true, thumbUrl: 'http://imgproxy.test/x/thumb.jpg' }],
      problems: [],
      warnings: [],
      artefactId: null,
    },
    {
      sheetRow: 5,
      status: 'problem',
      inventoryNumber: 'CER-01-02',
      title: 'Piatto bis',
      groupLabel: 'CINA',
      fields: [],
      pictures: [],
      problems: [{ field: 'inventoryNumber', text: 'CER-01-02', reason: 'Row 4 has the same inventory number.' }],
      warnings: [],
      artefactId: null,
    },
  ],
  total: 2,
  page: 1,
  pageSize: 50,
}

function renderWizard(route: string) {
  return renderWithProviders(
    <Routes>
      <Route path="/imports" element={<p>All imports</p>} />
      <Route path="/imports/:id" element={<ImportWizard />} />
    </Routes>,
    { route },
  )
}

describe('imports list and upload', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('lists imports with their state, counts and a link to each', async () => {
    mockFetch({
      'GET /api/imports': () => [
        job({ id: 'j1', state: 'finished', importedRows: 65, problemRows: 5 }),
        job({ id: 'j2', fileName: 'tessuti.xlsx', state: 'importing', processedRows: 12, totalRows: 223, importedRows: 12 }),
      ],
      'GET /api/object-types': () => [],
      'GET /api/import-templates': () => [],
    })
    renderWithProviders(<ImportsPage />)

    const table = await screen.findByRole('table', { name: 'Imports, newest first' })
    const finished = within(table).getByRole('link', { name: 'ceramiche.xlsx' }).closest('tr')!
    expect(within(finished).getByText('Finished')).toBeInTheDocument()
    expect(within(finished).getByText('65')).toBeInTheDocument()
    expect(within(table).getByRole('link', { name: 'tessuti.xlsx' })).toHaveAttribute('href', '/imports/j2')
    expect(within(table).getByText('12 of 223 rows')).toBeInTheDocument()
  })

  it('rejects a .csv before uploading, naming the file', async () => {
    const user = userEvent.setup({ applyAccept: false })
    const { calls } = mockFetch({
      'GET /api/imports': () => [],
      'GET /api/object-types': () => [{ id: 't1', name: 'Ceramics', nameLanguage: 'en', names: { en: 'Ceramics' }, artefactCount: 0 }],
      'GET /api/import-templates': () => [],
    })
    renderWithProviders(<ImportsPage />)

    await user.upload(screen.getByLabelText('Workbook'), new File(['a;b'], 'inventario.csv', { type: 'text/csv' }))

    expect(screen.getByRole('alert')).toHaveTextContent('“inventario.csv” is not an .xlsx workbook')
    await user.click(screen.getByRole('button', { name: /Upload and read/ }))
    expect(calls.some((c) => c.method === 'POST')).toBe(false)
  })

  it('uploads the workbook with its object type and template, then opens the import', async () => {
    const user = userEvent.setup()
    mockFetch({
      'GET /api/imports': () => [],
      'GET /api/object-types': () => [{ id: 't1', name: 'Ceramics', nameLanguage: 'en', names: { en: 'Ceramics' }, artefactCount: 0 }],
      'GET /api/import-templates': () => [{ id: 'tpl1', name: 'Collection workbook 2024', mapping, decisions: { places: [], dates: [], lists: [] }, updatedAt: '' }],
    })
    const sent: FormData[] = []
    class FakeXhr {
      upload = { onprogress: null as ((e: ProgressEvent) => void) | null }
      status = 0
      responseText = ''
      onload: (() => void) | null = null
      onerror: (() => void) | null = null
      open() {}
      send(body: FormData) {
        sent.push(body)
        this.upload.onprogress?.({ lengthComputable: true, loaded: 5, total: 10 } as ProgressEvent)
        setTimeout(() => {
          this.status = 201
          this.responseText = JSON.stringify(job({ id: 'new1', state: 'reading' }))
          this.onload?.()
        })
      }
    }
    vi.stubGlobal('XMLHttpRequest', FakeXhr)
    renderWithProviders(
      <Routes>
        <Route path="/" element={<ImportsPage />} />
        <Route path="/imports/:id" element={<p>Opened import</p>} />
      </Routes>,
    )

    await user.upload(screen.getByLabelText('Workbook'), new File(['PK'], 'ceramiche.xlsx'))
    await user.selectOptions(await screen.findByRole('combobox', { name: 'Object type' }), await screen.findByRole('option', { name: 'Ceramics' }))
    await user.selectOptions(screen.getByRole('combobox', { name: 'Template' }), await screen.findByRole('option', { name: 'Collection workbook 2024' }))
    await user.click(screen.getByRole('button', { name: /Upload and read/ }))

    await screen.findByText('Opened import')
    expect(sent[0]!.get('objectTypeId')).toBe('t1')
    expect(sent[0]!.get('templateId')).toBe('tpl1')
    expect((sent[0]!.get('file') as File).name).toBe('ceramiche.xlsx')
  })
})

describe('mapping step', () => {
  afterEach(() => vi.unstubAllGlobals())

  const routes = (current = job()) => ({
    ...languagesRoute,
    'GET /api/imports/j1': () => current,
    'GET /api/imports/j1/columns': () => ({
      columns: [
        { column: 1, heading: 'Gruppo e numero', samples: ['01.\nGE'], filledRows: 70, hitRates: { groupNumber: 0.5 } },
        { column: 2, heading: 'Nome File', samples: ['01. Coppa Celadon\ndiam 17,6 x h 7'], filledRows: 70, hitRates: { itemTitle: 0.5, dimensions: 0.5 } },
        { column: 6, heading: 'Formato Immagine', samples: ['png'], filledRows: 70, hitRates: {} },
      ],
      fields: [],
      detectors: [],
    }),
    'POST /api/imports/j1/preview': () => review,
    'PUT /api/imports/j1/mapping': (_: URL, init?: RequestInit) => ({ job: { ...current, state: 'readyForReview', mapping: JSON.parse(String(init?.body ?? '{}')) }, warnings: [] }),
    'GET /api/import-templates': () => [],
  })

  it('edits a line rule and saves the mapping', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch(routes())
    renderWizard('/imports/j1')

    expect(await screen.findByRole('heading', { name: 'Nome File' })).toBeInTheDocument()
    const rules = screen.getByRole('list', { name: 'Line rules of Nome File' })
    await user.selectOptions(within(rules).getByRole('combobox', { name: 'Rule 2: go to' }), 'Medium')
    await user.click(screen.getByRole('button', { name: 'Add rule to Nome File' }))
    await user.click(screen.getByRole('button', { name: 'Save mapping' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true))
    const body = calls.find((c) => c.method === 'PUT')!.body as ImportMapping
    const title = body.columns.find((c) => c.heading === 'Nome File')!
    expect(title.lines![1]).toEqual({ target: 'medium', detector: 'dimensions', continues: false })
    expect(title.lines).toHaveLength(3)
    expect(title.rest).toBe('description')
  })

  it('warns when a split column has no field for remaining lines', async () => {
    const user = userEvent.setup()
    mockFetch(routes())
    renderWizard('/imports/j1')

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Remaining lines of Nome File go to' }), 'Nowhere: not imported')

    expect(screen.getByText('“Nome File” has no field for remaining lines: lines that match no rule will not be imported.')).toBeInTheDocument()
  })

  it('shows the preview rows with their inventory numbers and problems', async () => {
    mockFetch(routes())
    renderWizard('/imports/j1')

    const preview = await screen.findByRole('region', { name: 'Preview of the first rows' })
    expect(await within(preview).findByText('CER-01-01')).toBeInTheDocument()
    expect(within(preview).getByText('diam 17,6 x h 7')).toBeInTheDocument()
    expect(within(preview).getByText('Row 4 has the same inventory number.')).toBeInTheDocument()
  })
})

describe('places and dates steps', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('creates a region for a place under a parent, keeps another as text, and accepts an exact match', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({
      ...languagesRoute,
      'GET /api/imports/j1': () => job({ state: 'readyForReview' }),
      'GET /api/regions': () => [{ id: 'r-anatolia', name: 'Anatolia', nameLanguage: 'en', names: { en: 'Anatolia' }, parentId: null, parentName: null }],
      'GET /api/imports/j1/places': () => [
        {
          text: 'Turchia',
          rows: 48,
          decision: { text: 'Turchia', action: 'link', regionId: 'r-turkey' },
          decided: false,
          suggestions: [{ regionId: 'r-turkey', regionName: 'Turkey', matchedName: 'Turchia', score: 1, exact: true, alias: false }],
        },
        { text: 'Anatolia occidentale', rows: 24, decision: null, decided: false, suggestions: [] },
        { text: 'Anatolia o Asia Centrale?', rows: 3, decision: null, decided: false, suggestions: [] },
      ],
      'PUT /api/imports/j1/places': () => [],
    })
    renderWizard('/imports/j1?step=places')

    const turchia = await screen.findByRole('combobox', { name: 'Region for “Turchia”' })
    expect(turchia).toHaveDisplayValue('Turkey — same name')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Region for “Anatolia occidentale”' }), 'Create a new region…')
    expect(screen.getByRole('textbox', { name: 'New region for “Anatolia occidentale” (Italian)' })).toHaveValue('Anatolia occidentale')
    await user.type(screen.getByRole('textbox', { name: 'New region for “Anatolia occidentale” (English)' }), 'Western Anatolia')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Inside region' }), 'Anatolia')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Region for “Anatolia o Asia Centrale?”' }), 'Keep as text, without a region')
    await user.click(screen.getByRole('button', { name: /Save and continue to lists/ }))

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true))
    expect(calls.find((c) => c.method === 'PUT')!.body).toEqual({
      places: [
        { text: 'Turchia', action: 'link', regionId: 'r-turkey' },
        { text: 'Anatolia occidentale', action: 'create', names: { it: 'Anatolia occidentale', en: 'Western Anatolia' }, parentId: 'r-anatolia' },
        { text: 'Anatolia o Asia Centrale?', action: 'keep' },
      ],
    })
  })

  it('gives a year range to a date the rules do not understand', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({
      'GET /api/imports/j1': () => job({ state: 'readyForReview' }),
      'GET /api/imports/j1/dates': () => [
        { text: "19 Dhu'l hijja 797 (4 Ottobre 1395)", sheetRows: [37], resolution: null },
        { text: 'datato 1491', sheetRows: [12, 14], resolution: { text: 'datato 1491', earliestYear: 1491, latestYear: 1491 } },
      ],
      'PUT /api/imports/j1/dates': () => [],
    })
    renderWizard('/imports/j1?step=dates')

    await user.type(await screen.findByRole('textbox', { name: "Earliest year for “19 Dhu'l hijja 797 (4 Ottobre 1395)”" }), '1395')
    await user.type(screen.getByRole('textbox', { name: "Latest year for “19 Dhu'l hijja 797 (4 Ottobre 1395)”" }), '1395')
    expect(screen.getByText('Rows 12, 14')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /Save and continue to review/ }))

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true))
    expect(calls.find((c) => c.method === 'PUT')!.body).toEqual({
      dates: [
        { text: "19 Dhu'l hijja 797 (4 Ottobre 1395)", earliestYear: 1395, latestYear: 1395 },
        { text: 'datato 1491', earliestYear: 1491, latestYear: 1491 },
      ],
    })
  })
})

describe('review, import and report', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('shows counts and rows, filters by status, and starts the import', async () => {
    const user = userEvent.setup()
    let current = job({ state: 'readyForReview' })
    const { calls } = mockFetch({
      'GET /api/imports': () => [],
      'GET /api/imports/j1': () => current,
      'GET /api/imports/j1/review': () => review,
      'POST /api/imports/j1/commit': () => (current = job({ state: 'importing', processedRows: 0 })),
    })
    renderWizard('/imports/j1?step=review')

    expect(await screen.findByText('ready to import', { exact: false })).toHaveTextContent('1 ready to import · 1 already in the catalogue · 1 with problems')
    const table = screen.getByRole('table', { name: 'Rows as they will be imported' })
    expect(within(table).getByRole('img', { name: 'Immagine 1.jpg' })).toHaveAttribute('src', 'http://imgproxy.test/x/thumb.jpg')
    expect(within(table).getByText('Row 4 has the same inventory number.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /Problems/ }))
    await waitFor(() => expect(calls.some((c) => c.url.pathname.endsWith('/review') && c.url.searchParams.get('status') === 'problem')).toBe(true))

    await user.click(screen.getByRole('button', { name: 'Import 1 row' }))
    await waitFor(() => expect(calls.some((c) => c.method === 'POST' && c.url.pathname === '/api/imports/j1/commit')).toBe(true))
    expect(await screen.findByRole('progressbar', { name: 'Rows imported' })).toBeInTheDocument()
  })

  it('reports a finished import with links to the artefacts and a spreadsheet download', async () => {
    mockFetch({
      'GET /api/imports/j1': () => job({ state: 'finished', processedRows: 3, importedRows: 1, skippedRows: 1, problemRows: 1 }),
      'GET /api/imports/j1/report': () => ({
        job: job({ state: 'finished' }),
        rows: [
          { sheetRow: 3, status: 'imported', inventoryNumber: 'CER-01-01', artefactId: 'a1', problems: [], warnings: ['Picture “Immagine 2.wdp” is not in a format the catalogue accepts'] },
          { sheetRow: 4, status: 'skipped', inventoryNumber: 'CER-01-02', artefactId: null, problems: [], warnings: [] },
          { sheetRow: 6, status: 'problem', inventoryNumber: 'CER-01-04', artefactId: null, problems: [{ field: 'creationDate', text: 'datato 1491', reason: 'The date is not understood.' }], warnings: [] },
        ],
      }),
    })
    renderWizard('/imports/j1')

    const report = await screen.findByRole('table', { name: 'Import report' })
    expect(within(report).getByRole('link', { name: 'CER-01-01' })).toHaveAttribute('href', '/artefacts/a1')
    expect(within(report).getByText('The date is not understood.')).toBeInTheDocument()
    expect(within(report).getByText('Already present')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Download report/ })).toHaveAttribute('href', '/api/imports/j1/report.xlsx')
  })

  it('discards an import that has not been committed', async () => {
    const user = userEvent.setup()
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const { calls } = mockFetch({
      ...languagesRoute,
      'GET /api/imports': () => [],
      'GET /api/imports/j1': () => job({ state: 'readyForReview' }),
      'GET /api/imports/j1/review': () => review,
      'POST /api/imports/j1/discard': () => job({ state: 'discarded' }),
    })
    renderWizard('/imports/j1?step=review')

    await user.click(await screen.findByRole('button', { name: 'Discard import' }))

    await screen.findByText('All imports')
    expect(calls.some((c) => c.method === 'POST' && c.url.pathname === '/api/imports/j1/discard')).toBe(true)
  })
})

describe('lists step', () => {
  afterEach(() => vi.unstubAllGlobals())

  const group = (list: string, values: Record<string, unknown>[]) => ({ list, values, undecided: values.filter((v) => !v.decision).length })

  const entryRoutes = {
    'GET /api/lists/dynasties': () => [{ id: 'd-saf', kind: 'dynasty', name: 'Safavide', nameLanguage: 'it', names: { it: 'Safavide' }, artefactCount: 3 }],
    'GET /api/lists/collection-groups': () => [{ id: 'g-cina', kind: 'collectionGroup', name: 'CINA', nameLanguage: 'it', names: { it: 'CINA' }, artefactCount: 9 }],
    'GET /api/lists/storage-locations': () => [],
    'GET /api/parties': () => [{ id: 'p-drouot', name: 'Drouot', kind: 'organisation', sourceCount: 2, holderCount: 0 }],
  }

  it('links a variant, picks a source with no similar name, creates a party, and keeps an exact match', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({
      ...languagesRoute,
      ...entryRoutes,
      'GET /api/imports/j1': () => job({ state: 'readyForReview' }),
      'GET /api/imports/j1/lists': () => [
        group('collectionGroup', [
          {
            text: 'CINA',
            rows: 12,
            fields: ['collectionGroup'],
            decision: { list: 'collectionGroup', text: 'CINA', action: 'link', entryId: 'g-cina' },
            decided: false,
            suggestions: [{ entryId: 'g-cina', name: 'CINA', matchedName: 'CINA', score: 1, exact: true, alias: false }],
          },
        ]),
        group('dynasty', [
          {
            text: 'Sfavide',
            rows: 4,
            fields: ['dynasty'],
            decision: null,
            decided: false,
            suggestions: [{ entryId: 'd-saf', name: 'Safavide', matchedName: 'Safavide', score: 0.86, exact: false, alias: false }],
          },
        ]),
        group('storageLocation', []),
        group('party', [
          { text: 'Paris, Drouot-Richelieu Jean Dupont, Paris', rows: 1, fields: ['acquisitionSource'], decision: null, decided: false, suggestions: [] },
          { text: 'Rossi Antiquariato srl', rows: 2, fields: ['acquisitionSource', 'holder'], decision: null, decided: false, suggestions: [] },
        ]),
      ],
      'PUT /api/imports/j1/lists': () => [],
    })
    renderWizard('/imports/j1?step=lists')

    expect(await screen.findByRole('combobox', { name: 'Collection group for “CINA”' })).toHaveDisplayValue('CINA — same name')
    expect(screen.getByText('3 undecided: their rows will not be imported')).toBeInTheDocument()
    expect(screen.getByText(/as source and holder/)).toBeInTheDocument()

    await user.selectOptions(screen.getByRole('combobox', { name: 'Dynasty for “Sfavide”' }), 'Safavide — 86% similar')

    await user.selectOptions(screen.getByRole('combobox', { name: 'Party for “Paris, Drouot-Richelieu Jean Dupont, Paris”' }), 'Pick from the list…')
    await user.type(screen.getByRole('combobox', { name: 'Any party' }), 'Drou')
    await user.click(await screen.findByRole('option', { name: /Drouot/ }))

    await user.selectOptions(screen.getByRole('combobox', { name: 'Party for “Rossi Antiquariato srl”' }), 'Create a new party…')
    const name = screen.getByRole('textbox', { name: 'New party' })
    expect(name).toHaveValue('Rossi Antiquariato srl')
    await user.clear(name)
    await user.type(name, 'Rossi Antiquariato')
    expect(screen.getByRole('combobox', { name: 'Kind' })).toHaveDisplayValue('Organisation')

    expect(screen.getByText('Every value has a decision.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /Save and continue to dates/ }))

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true))
    expect(calls.find((c) => c.method === 'PUT')!.body).toEqual({
      lists: [
        { list: 'collectionGroup', text: 'CINA', action: 'link', entryId: 'g-cina' },
        { list: 'dynasty', text: 'Sfavide', action: 'link', entryId: 'd-saf' },
        { list: 'party', text: 'Paris, Drouot-Richelieu Jean Dupont, Paris', action: 'link', entryId: 'p-drouot' },
        { list: 'party', text: 'Rossi Antiquariato srl', action: 'create', name: 'Rossi Antiquariato', partyKind: 'organisation' },
      ],
    })
  })

  it('creates a dynasty with names prefilled in the workbook language and filters the undecided', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({
      ...languagesRoute,
      ...entryRoutes,
      'GET /api/imports/j1': () => job({ state: 'readyForReview' }),
      'GET /api/imports/j1/lists': () => [
        group('collectionGroup', []),
        group('dynasty', [
          { text: 'Ilkhanide?', rows: 2, fields: ['dynasty'], decision: null, decided: false, suggestions: [] },
          {
            text: 'Ottomana',
            rows: 9,
            fields: ['dynasty'],
            decision: { list: 'dynasty', text: 'Ottomana', action: 'link', entryId: 'd-ott' },
            decided: true,
            suggestions: [{ entryId: 'd-ott', name: 'Ottomana', matchedName: 'Ottomana', score: 1, exact: true, alias: false }],
          },
        ]),
        group('storageLocation', []),
        group('party', []),
      ],
      'PUT /api/imports/j1/lists': () => [],
    })
    renderWizard('/imports/j1?step=lists')

    await user.click(await screen.findByRole('checkbox', { name: 'Show only undecided' }))
    expect(screen.queryByRole('combobox', { name: 'Dynasty for “Ottomana”' })).toBeNull()
    await user.selectOptions(screen.getByRole('combobox', { name: 'Dynasty for “Ilkhanide?”' }), 'Create a new dynasty…')
    expect(screen.getByRole('textbox', { name: 'New dynasty (Italian)' })).toHaveValue('Ilkhanide')
    await user.type(screen.getByRole('textbox', { name: 'New dynasty (English)' }), 'Ilkhanid')
    await user.click(screen.getByRole('button', { name: /Save and continue to dates/ }))

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true))
    expect(calls.find((c) => c.method === 'PUT')!.body).toEqual({
      lists: [
        { list: 'dynasty', text: 'Ilkhanide?', action: 'create', names: { it: 'Ilkhanide', en: 'Ilkhanid' } },
        { list: 'dynasty', text: 'Ottomana', action: 'link', entryId: 'd-ott' },
      ],
    })
  })

  it('saves the mapping and decisions as a template from the review step', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({
      'GET /api/imports/j1': () => job({ state: 'readyForReview' }),
      'GET /api/imports/j1/review': () => review,
      'POST /api/import-templates': () => ({ id: 'tpl9', name: 'Collection workbook 2024', mapping, decisions: { places: [], dates: [], lists: [] }, updatedAt: '' }),
    })
    renderWizard('/imports/j1?step=review')

    await user.type(await screen.findByRole('textbox', { name: 'Template name' }), 'Collection workbook 2024')
    await user.click(screen.getByRole('button', { name: 'Save as template' }))

    expect(await screen.findByText('Saved as template “Collection workbook 2024”.')).toBeInTheDocument()
    expect(calls.find((c) => c.method === 'POST' && c.url.pathname === '/api/import-templates')!.body).toEqual({
      name: 'Collection workbook 2024',
      fromJobId: 'j1',
    })
  })
})
