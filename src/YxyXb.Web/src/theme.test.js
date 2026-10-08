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

test('vendor paths use the same ink and red theme', () => {
  expect(isCustomerPath('/vendor')).toBe(true)
  expect(isCustomerPath('/vendor/login')).toBe(true)
  expect(isCustomerPath('/vendor/customers')).toBe(true)
})
