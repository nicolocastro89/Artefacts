import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { importDetectorsRoute, mockFetch, problem } from '../../test/fetchMock'
import { renderWithProviders } from '../../test/render'
import { ParsingPage } from './ParsingPage'

const userDetector = {
  id: 'user:abc',
  label: 'Insurance',
  example: 'ass. Lloyd',
  isSystem: false,
  definition: { kind: 'regex', pattern: '^ass\\.\\s*(?<value>.+)$', ignoreCase: true, parse: 'none', example: 'ass. Lloyd' },
}

const months = {
  id: 'months',
  label: 'Month names',
  entries: [
    { word: 'marzo', value: '3', isUser: false, builtInValue: null },
    { word: 'mai', value: '5', isUser: true, builtInValue: null },
  ],
}

describe('Parsing page', () => {
  it('lists built-in detectors read-only and user detectors with actions', async () => {
    mockFetch({ 'GET /api/imports/detectors': () => [...importDetectorsRoute['GET /api/imports/detectors']().map((d) => ({ ...d, isSystem: true })), userDetector] })
    renderWithProviders(<ParsingPage />)

    expect(await screen.findByText('Insurance')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Edit Insurance' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Edit Money' })).not.toBeInTheDocument()
    expect(screen.getAllByText('Built-in').length).toBeGreaterThan(5)
  })

  it('creates a detector and tries it against a sample line', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({
      'GET /api/imports/detectors': () => [],
      'POST /api/imports/detectors': () => new Response(JSON.stringify(userDetector), { status: 201, headers: { 'Content-Type': 'application/json' } }),
      'POST /api/imports/detectors/try': () => ({
        lines: [{ line: 'ass. Lloyd', matched: true, text: 'Lloyd', parsed: null, remainder: null, itemNumber: null, guessed: false }],
      }),
    })
    renderWithProviders(<ParsingPage />)

    await user.click(await screen.findByRole('button', { name: /new detector/i }))
    await user.type(screen.getByLabelText('Name'), 'Insurance')
    await user.type(screen.getByLabelText('Pattern'), '^ass', { skipClick: false })
    await user.type(screen.getByLabelText('Sample lines'), 'ass. Lloyd')

    expect(await screen.findByText('Lloyd', {}, { timeout: 3000 })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Save detector' }))
    await waitFor(() => expect(calls.some((c) => c.method === 'POST' && c.url.pathname === '/api/imports/detectors')).toBe(true))
    const body = calls.find((c) => c.method === 'POST' && c.url.pathname === '/api/imports/detectors')!.body as { name: string; definition: { kind: string } }
    expect(body.name).toBe('Insurance')
    expect(body.definition.kind).toBe('regex')
  })

  it('shows a pattern problem next to the pattern', async () => {
    const user = userEvent.setup()
    mockFetch({
      'GET /api/imports/detectors': () => [],
      'POST /api/imports/detectors': () => problem(400, { title: 'Invalid', status: 400, errors: { 'definition.pattern': ['Not a valid pattern: unclosed'] } }),
    })
    renderWithProviders(<ParsingPage />)

    await user.click(await screen.findByRole('button', { name: /new detector/i }))
    await user.type(screen.getByLabelText('Name'), 'Broken')
    await user.click(screen.getByRole('button', { name: 'Save detector' }))

    expect(await screen.findByText('Not a valid pattern: unclosed')).toBeInTheDocument()
  })

  it('saves the entries a user adds to a lookup table', async () => {
    const user = userEvent.setup()
    const { calls } = mockFetch({
      'GET /api/imports/lookup-tables': () => [months],
      'PUT /api/imports/lookup-tables/months': () => months,
    })
    renderWithProviders(<ParsingPage />)

    await user.click(await screen.findByRole('tab', { name: 'Lookup tables' }))
    expect(await screen.findByLabelText('Word 1')).toHaveValue('mai')
    await user.click(screen.getByRole('button', { name: /add entry/i }))
    await user.type(screen.getByLabelText('Word 2'), 'sett')
    await user.type(screen.getByLabelText('Value of sett'), '9')
    await user.click(screen.getByRole('button', { name: /save month names/i }))

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true))
    expect(calls.find((c) => c.method === 'PUT')!.body).toEqual({
      entries: [
        { word: 'mai', value: '5' },
        { word: 'sett', value: '9' },
      ],
    })
  })
})
