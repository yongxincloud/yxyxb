import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test } from 'vitest'

const root = dirname(fileURLToPath(import.meta.url))
const layout = readFileSync(join(root, 'views/PortalLayout.vue'), 'utf8')
const vendor = readFileSync(join(root, 'views/VendorLayout.vue'), 'utf8')

test('portal hides partner menus until isProvider and logout goes home', () => {
  expect(layout).toContain('isProvider')
  expect(layout).toContain("me.isProvider")
  expect(layout).toContain("'/portal/commission'")
  expect(layout).toContain("'/portal/customers'")
  expect(layout).toContain("router.replace('/')")
  expect(layout).toMatch(/if \(isProvider\.value\)[\s\S]*我的佣金/)
  expect(layout).toMatch(/else[\s\S]*申请服务商/)
})

test('vendor logout returns to marketing home', () => {
  expect(vendor).toContain("await post('/api/vendor/logout')")
  expect(vendor).toMatch(/async function logout\(\)[\s\S]*router\.replace\('\/'\)/)
})
