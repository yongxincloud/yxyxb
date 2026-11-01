import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test } from 'vitest'

const root = dirname(fileURLToPath(import.meta.url))

test('vendor can enable, disable, and change level of approved providers', () => {
  const page = readFileSync(join(root, 'views/VendorProvidersView.vue'), 'utf8')
  expect(page).toContain('/api/vendor/providers/')
  expect(page).toContain('/status')
  expect(page).toContain('/level')
  expect(page).toContain('启用')
  expect(page).toContain('停用')
  expect(page).toContain('保存')
  expect(page).toContain('row.status === 2')
  expect(page).toContain('已解约的不能再改')
})
