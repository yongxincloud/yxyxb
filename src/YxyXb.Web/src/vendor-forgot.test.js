import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test } from 'vitest'

const root = dirname(fileURLToPath(import.meta.url))

test('vendor login supports code login and email password reset', () => {
  const login = readFileSync(join(root, 'views/VendorLoginView.vue'), 'utf8')
  const forgot = readFileSync(join(root, 'views/VendorForgotView.vue'), 'utf8')
  const router = readFileSync(join(root, 'router.js'), 'utf8')
  expect(login).toContain('/vendor/forgot')
  expect(login).toContain('/api/vendor/login-code')
  expect(login).toContain("scene: 'login'")
  expect(forgot).toContain('/api/vendor/send-code')
  expect(forgot).toContain("scene: 'reset'")
  expect(forgot).toContain('/api/vendor/reset')
  expect(router).toContain('VendorForgotView')
  expect(router).toContain("/vendor/forgot")
})
