import { expect, test } from 'vitest'
import { isCustomerPath } from './theme.js'

test('customer paths use the customer theme', () => {
  expect(isCustomerPath('/')).toBe(true)
  expect(isCustomerPath('/login')).toBe(true)
  expect(isCustomerPath('/register')).toBe(true)
  expect(isCustomerPath('/home')).toBe(true)
  expect(isCustomerPath('/forgot')).toBe(true)
  expect(isCustomerPath('/terms')).toBe(true)
})

test('vendor paths keep the vendor theme', () => {
  expect(isCustomerPath('/vendor')).toBe(false)
  expect(isCustomerPath('/vendor/login')).toBe(false)
  expect(isCustomerPath('/vendor/customers')).toBe(false)
})
