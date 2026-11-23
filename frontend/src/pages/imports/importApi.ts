import { useQuery } from '@tanstack/react-query'
import { api, type Schemas } from '../../api/client'
import { ApiError, unwrap, type Problem } from '../../api/problem'

export type ImportJob = Schemas['ImportJobDto']
export type ImportJobState = Schemas['ImportJobState']
export type ImportMapping = Schemas['ImportMapping']
export type ColumnRule = Schemas['ColumnRule']
export type LineRule = Schemas['LineRule']
export type ImportField = NonNullable<Schemas['ImportField']>
export type LineDetector = NonNullable<Schemas['LineDetector']>
export type ColumnSummary = Schemas['ColumnSummary']
export type ImportReview = Schemas['ImportReviewDto']
export type ReviewRow = Schemas['ReviewRowDto']
export type ImportRowStatus = Schemas['ImportRowStatus']
export type ImportPlace = Schemas['ImportPlaceDto']
export type PlaceDecision = Schemas['PlaceDecision']
export type ImportDate = Schemas['ImportDateDto']
export type ImportList = Schemas['ImportList']
export type ImportListGroup = Schemas['ImportListGroupDto']
export type ImportListValue = Schemas['ImportListValueDto']
export type ListDecision = Schemas['ListDecision']
export type ImportTemplate = Schemas['ImportTemplateDto']
export type ImportReport = Schemas['ImportReportDto']
export type MeasureKind = Schemas['MeasureKind']
export type DimensionsOptions = Schemas['DimensionsOptions']
export type DimensionResolution = Schemas['DimensionResolution']
export type ImportDimensions = Schemas['ImportDimensionsDto']

/** Measurements in the order the import shows them, with their labels. */
export const MEASURE_KINDS: { value: MeasureKind; label: string; short: string }[] = [
  { value: 'height', label: 'Height', short: 'H' },
  { value: 'width', label: 'Width', short: 'W' },
  { value: 'length', label: 'Length', short: 'L' },
  { value: 'diameter', label: 'Diameter', short: 'Ø' },
]

/** Common orders for numbers written without labels ("82 x 64"). */
export const UNLABELLED_ORDERS: MeasureKind[][] = [
  ['height', 'width'],
  ['height', 'width', 'length'],
  ['width', 'height'],
  ['length', 'width', 'height'],
  ['length', 'width'],
  ['height', 'diameter'],
]

export const orderLabel = (order: MeasureKind[]) => order.map((k) => MEASURE_KINDS.find((m) => m.value === k)!.label.toLowerCase()).join(' × ')

/** Fields in the order the mapping step offers them. */
export const FIELDS: { value: ImportField; label: string }[] = [
  { value: 'title', label: 'Title' },
  { value: 'itemNumber', label: 'Item number' },
  { value: 'groupNumber', label: 'Group number' },
  { value: 'catalogueNumber', label: 'Catalogue number' },
  { value: 'inventoryNumber', label: 'Inventory number' },
  { value: 'dimensions', label: 'Dimensions' },
  { value: 'medium', label: 'Medium' },
  { value: 'maker', label: 'Maker' },
  { value: 'description', label: 'Description' },
  { value: 'placeOfProduction', label: 'Place of production' },
  { value: 'creationDate', label: 'Date' },
  { value: 'dynasty', label: 'Dynasty' },
  { value: 'acquisitionSource', label: 'Acquired from' },
  { value: 'acquisitionDate', label: 'Acquisition date' },
  { value: 'acquisitionLot', label: 'Lot' },
  { value: 'acquisitionPrice', label: 'Price' },
  { value: 'acquisitionNote', label: 'Acquisition note' },
  { value: 'exhibitions', label: 'Exhibitions' },
  { value: 'catalogueEntry', label: 'Catalogue entry' },
  { value: 'holder', label: 'Holder' },
  { value: 'storageLocation', label: 'Storage location' },
  { value: 'valuation', label: 'Valuation' },
  { value: 'collectionGroup', label: 'Collection group' },
]

export const DETECTORS: { value: LineDetector; label: string; example: string }[] = [
  { value: 'itemTitle', label: 'Numbered title', example: '01. Coppa Celadon' },
  { value: 'groupNumber', label: 'Group number', example: '01.' },
  { value: 'dimensions', label: 'Dimensions', example: 'diam 17,6 x h 7' },
  { value: 'historicalDate', label: 'Historical date', example: 'fine XII inizio XIII' },
  { value: 'calendarDate', label: 'Calendar date', example: '12 06 1990' },
  { value: 'lot', label: 'Auction lot', example: 'lot 170' },
  { value: 'money', label: 'Price with currency', example: '£ 7.865' },
  { value: 'catalogueNumber', label: 'Catalogue number', example: 'W.389' },
  { value: 'locationCode', label: 'Location code', example: 'MI F3' },
]

export const fieldLabel = (field: string | null | undefined) => FIELDS.find((f) => f.value === field)?.label ?? field ?? ''
export const detectorLabel = (detector: string) => DETECTORS.find((d) => d.value === detector)?.label ?? detector

export const STATE_LABELS: Record<ImportJobState, string> = {
  reading: 'Reading',
  awaitingMapping: 'Map columns',
  readyForReview: 'Ready for review',
  importing: 'Importing',
  finished: 'Finished',
  failed: 'Failed',
  discarded: 'Discarded',
}

export const STATUS_LABELS: Record<ImportRowStatus, string> = {
  pending: 'Pending',
  ready: 'Ready',
  problem: 'Problem',
  imported: 'Imported',
  skipped: 'Already present',
}

/** States in which the worker is busy: the job is polled every 2 seconds. */
export const isRunning = (state: ImportJobState) => state === 'reading' || state === 'importing'

export const isEditable = (state: ImportJobState) => state === 'awaitingMapping' || state === 'readyForReview'

export function useImportJob(id: string) {
  return useQuery({
    queryKey: ['imports', id],
    queryFn: async () => unwrap(await api.GET('/api/imports/{id}', { params: { path: { id } } })),
    refetchInterval: (query) => (query.state.data && isRunning(query.state.data.state) ? 2000 : false),
  })
}

export function useImportJobs() {
  return useQuery({
    queryKey: ['imports'],
    queryFn: async () => unwrap(await api.GET('/api/imports')),
    refetchInterval: (query) => (query.state.data?.some((j) => isRunning(j.state)) ? 2000 : false),
  })
}

export function useImportTemplates() {
  return useQuery({
    queryKey: ['import-templates'],
    queryFn: async () => unwrap(await api.GET('/api/import-templates')),
  })
}

/** Only .xlsx workbooks can be imported (pictures are read from the workbook itself). */
export function checkWorkbookFile(file: File): string | null {
  if (!/\.xlsx$/i.test(file.name))
    return `“${file.name}” is not an .xlsx workbook. Save it from Excel as an Excel workbook (.xlsx); .xls and .csv files cannot be imported.`
  return null
}

/**
 * Uploads a workbook with progress (fetch reports no upload progress, so this uses XMLHttpRequest).
 * Rejects with {@link ApiError} for problem responses, like the other API helpers.
 */
export function uploadWorkbook(
  input: { file: File; objectTypeId: string; templateId?: string | null },
  onProgress?: (fraction: number) => void,
): Promise<ImportJob> {
  const form = new FormData()
  form.append('file', input.file)
  form.append('objectTypeId', input.objectTypeId)
  if (input.templateId) form.append('templateId', input.templateId)

  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest()
    xhr.open('POST', '/api/imports')
    xhr.upload.onprogress = (e) => {
      if (e.lengthComputable) onProgress?.(e.loaded / e.total)
    }
    xhr.onload = () => {
      let body: unknown = null
      try {
        body = xhr.responseText ? JSON.parse(xhr.responseText) : null
      } catch {
        body = null
      }
      if (xhr.status >= 200 && xhr.status < 300) resolve(body as ImportJob)
      else reject(new ApiError(xhr.status, (body ?? {}) as Problem))
    }
    xhr.onerror = () => reject(new Error('The upload was interrupted. Check the connection and try again.'))
    xhr.send(form)
  })
}

export function errorMessage(e: unknown): string {
  if (e instanceof ApiError) return Object.values(e.fieldErrors).flat()[0] ?? e.message
  return e instanceof Error ? e.message : 'Something went wrong.'
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`
  return `${(bytes / (1024 * 1024)).toFixed(bytes < 10 * 1024 * 1024 ? 1 : 0)} MB`
}
