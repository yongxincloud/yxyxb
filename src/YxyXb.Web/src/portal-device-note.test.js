import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test } from 'vitest'

const root = dirname(fileURLToPath(import.meta.url))
const view = readFileSync(join(root, 'views/PortalDevicesView.vue'), 'utf8')

test('portal device page saves a note about who uses the computer', () => {
  expect(view).toContain("post('/api/portal/devices/note'")
  expect(view).toContain('使用人')
  expect(view).toContain('v-model="row.note"')
  expect(view).toContain('maxlength="40"')
})
