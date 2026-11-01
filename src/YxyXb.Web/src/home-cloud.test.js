import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test } from 'vitest'

const root = dirname(fileURLToPath(import.meta.url))
const home = readFileSync(join(root, 'views/HomeView.vue'), 'utf8')

test('home describes all supported cloud storage providers', () => {
  expect(home).toContain('id="cloud"')
  expect(home).toContain('href="#cloud"')
  expect(home).toContain('适配的云存储')
  expect(home).toContain('七牛云')
  expect(home).toContain('阿里云')
  expect(home).toContain('腾讯云')
  expect(home).toContain('天翼云')
  expect(home).toContain('百度网盘')
})
