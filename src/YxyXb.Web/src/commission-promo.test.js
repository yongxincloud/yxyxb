import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test } from 'vitest'

const root = dirname(fileURLToPath(import.meta.url))
const view = readFileSync(join(root, 'views/CommissionView.vue'), 'utf8')

test('commission page shows a full promo url, copy button, and qr code', () => {
  expect(view).toContain('toAbsolutePromoUrl')
  expect(view).toContain('window.location.origin')
  expect(view).toContain('QRCode.toDataURL')
  expect(view).toContain('clipboard.writeText')
  expect(view).toContain('复制')
  expect(view).toContain('推广链接二维码')
})
