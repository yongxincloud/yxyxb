import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test } from 'vitest'

test('tokens include brand primary', () => {
  const dir = dirname(fileURLToPath(import.meta.url))
  const css = readFileSync(join(dir, 'styles/tokens.css'), 'utf8')
  expect(css).toContain('#3171FF')
  expect(css).toContain('--brand-primary')
})
