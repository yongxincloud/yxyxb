import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test } from 'vitest'

const root = dirname(fileURLToPath(import.meta.url))

test('vendor customer list shows profile fields and searches the api', () => {
  const page = readFileSync(join(root, 'views/VendorCustomersView.vue'), 'utf8')
  expect(page).toContain('/api/vendor/customers')
  expect(page).toContain('encodeURIComponent')
  expect(page).toContain('姓名、邮箱、手机、公司、抬头')
  expect(page).toContain('查询')
  expect(page).toContain('姓名')
  expect(page).toContain('昵称')
  expect(page).toContain('公司')
  expect(page).toContain('手机')
  expect(page).toContain('抬头')
  expect(page).toContain('税号')
  expect(page).toContain('收票邮箱')
  expect(page).toContain('执照文件名')
  expect(page).toContain('没有匹配的客户')
  expect(page).toContain('CUSTOM')
  expect(page).toContain('人数')
  expect(page).toContain('maxDevices')
  expect(page).toContain('—')
})
