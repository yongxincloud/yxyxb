import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test } from 'vitest'

const root = dirname(fileURLToPath(import.meta.url))
const html = readFileSync(join(root, '../index.html'), 'utf8')
const router = readFileSync(join(root, 'router.js'), 'utf8')
const robots = readFileSync(join(root, '../public/robots.txt'), 'utf8')

test('marketing pages expose titles, descriptions, and a sitemap', () => {
  expect(html).toContain('用心云MES系统')
  expect(html).toContain('常州用友')
  expect(html).toContain('勒索病毒的防范')
  expect(html).toContain('name="keywords"')
  expect(html).toContain('rel="canonical"')
  expect(html).toContain('href="/about"')
  expect(router).toContain("title: '关于我们｜用心云MES系统、电子看板与用心云循备'")
  expect(router).toContain("title: '联系我们｜用心云·循备 400-051-9608'")
  expect(robots).toContain('Sitemap: https://backup.yongxincloud.cn/sitemap.xml')
  expect(robots).toContain('Disallow: /vendor')
})
