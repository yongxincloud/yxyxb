import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test } from 'vitest'

const root = dirname(fileURLToPath(import.meta.url))
const page = readFileSync(join(root, 'views/VendorDevicesView.vue'), 'utf8')
const layout = readFileSync(join(root, 'views/VendorLayout.vue'), 'utf8')
const router = readFileSync(join(root, 'router.js'), 'utf8')

test('vendor device page searches accounts and shows the note', () => {
  expect(page).toContain('/api/vendor/devices')
  expect(page).toContain('encodeURIComponent')
  expect(page).toContain('备注')
  expect(page).toContain('row.note')
  expect(page).toContain('没有匹配的设备')
  expect(page).toContain('pageSize=')
  expect(page).toContain('上一页')
  expect(page).toContain('下一页')
  expect(layout).toContain("'/vendor/devices'")
  expect(layout).toContain('设备查询')
  expect(router).toContain("path: 'devices'")
})
