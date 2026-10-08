import { test, expect } from 'vitest'
import { PRODUCT_DISPLAY_NAME, PRODUCT_PATH_NAME, PRODUCT_SHORT_NAME } from './brand.js'

test('brand display uses middle dot', () => {
  expect(PRODUCT_DISPLAY_NAME).toBe('用心云·循备')
  expect(PRODUCT_DISPLAY_NAME).toContain('\u00B7')
  expect(PRODUCT_PATH_NAME).toBe('用心云循备')
  expect(PRODUCT_SHORT_NAME).toBe('循备')
})
