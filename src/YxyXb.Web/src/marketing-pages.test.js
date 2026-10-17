import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test } from 'vitest'

const root = dirname(fileURLToPath(import.meta.url))
const home = readFileSync(join(root, 'views/HomeView.vue'), 'utf8')
const about = readFileSync(join(root, 'views/AboutView.vue'), 'utf8')
const contact = readFileSync(join(root, 'views/ContactView.vue'), 'utf8')
const router = readFileSync(join(root, 'router.js'), 'utf8')

test('home links to real about and contact pages', () => {
  expect(home).toContain('to="/about"')
  expect(home).toContain('to="/contact"')
  expect(home).not.toContain('href="#about"')
  expect(router).toContain("path: '/about'")
  expect(router).toContain("path: '/contact'")
  expect(about).toContain('用心云 MES')
  expect(about).toContain('20 多年')
  expect(about).toContain('用友软件在常州的服务经销机构')
  expect(about).toContain('通江中路 600 号')
  expect(contact).toContain('400-051-9608')
  expect(contact).toContain('market@yongxinyun.com')
  expect(contact).not.toContain('window.location')
})
