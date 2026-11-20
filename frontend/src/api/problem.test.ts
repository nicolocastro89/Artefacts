import { ApiError, mapFieldErrors } from './problem'

describe('mapFieldErrors', () => {
  it('attaches errors to their fields, including nested paths, and collects the rest', () => {
    const { byField, unmatched } = mapFieldErrors(
      {
        inventoryNumber: ['Already used.'],
        'acquisition.price.currency': ['Unknown currency.'],
        'provenance[1].regionId': ['The region does not exist.'],
        file: ['Unsupported.'],
      },
      ['inventoryNumber', 'acquisition', 'provenance'],
    )

    expect(byField.inventoryNumber).toEqual(['Already used.'])
    expect(byField.acquisition).toEqual(['Unknown currency.'])
    expect(byField.provenance).toEqual(['The region does not exist.'])
    expect(unmatched).toEqual(['file: Unsupported.'])
  })

  it('prefers an exact field match over a parent', () => {
    const { byField } = mapFieldErrors({ 'acquisition.price.currency': ['Bad'] }, ['acquisition', 'acquisition.price.currency'])
    expect(byField['acquisition.price.currency']).toEqual(['Bad'])
    expect(byField.acquisition).toBeUndefined()
  })

  it('exposes validation errors from an ApiError', () => {
    const error = new ApiError(400, { title: 'Invalid', errors: { title: ['Is required.'] } })
    expect(error.fieldErrors).toEqual({ title: ['Is required.'] })
    expect(error.message).toBe('Invalid')
  })
})
