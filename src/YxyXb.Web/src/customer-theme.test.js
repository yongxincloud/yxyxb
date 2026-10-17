import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test } from 'vitest'

const root = dirname(fileURLToPath(import.meta.url))

function read(name) {
  return readFileSync(join(root, name), 'utf8')
}

test('customer theme overrides ink and red without replacing vendor blue', () => {
  const css = read('styles/tokens.css')
  expect(css).toContain('--brand-primary: #3171FF')
  expect(css).toContain('body.theme-customer')
  expect(css).toContain('--brand-primary: #B10505')
  expect(css).toContain('#DC0606')
  expect(css).toContain('#B00404')
  expect(css).toContain('#1C1214')
  expect(css).toContain('#7A1F1A')
  expect(css).toContain('#F6F3F1')
})

test('logo appears on home, login, register, and portal/vendor shells', () => {
  expect(read('views/HomeView.vue')).toContain('src="/images/logo.png"')
  expect(read('views/HomeView.vue')).toContain('height: 88px')
  expect(read('views/HomeView.vue')).toContain('product1.png')
  expect(read('views/LoginView.vue')).toContain('src="/images/logo.png"')
  expect(read('views/LoginView.vue')).toContain('#DC0606')
  expect(read('views/PortalLayout.vue')).toContain('src="/images/logo.png"')
  expect(read('views/PortalLayout.vue')).toContain('height: 72px')
  expect(read('views/PortalLayout.vue')).toContain('#F8E8E8')
  expect(read('views/RegisterView.vue')).toContain('src="/images/logo.png"')
  expect(read('views/VendorLayout.vue')).toContain('src="/images/logo.png"')
  expect(read('views/VendorLayout.vue')).toContain('#F8E8E8')
  expect(read('views/VendorLayout.vue')).not.toContain('#eef3ff')
  expect(read('views/VendorLoginView.vue')).toContain('src="/images/logo.png"')
})

test('subscription page lists what the license covers', () => {
  const page = read('views/PortalView.vue')
  expect(page).toContain('请先登录')
  expect(page).toContain('文件备份')
  expect(page).toContain('库备份（SQL Server）')
  expect(page).toContain('离线介质')
  expect(page).toContain('云、UNC、FTP、邮件')
  expect(page).toContain('没有有效授权时，文件备份、库备份、云、UNC、FTP、邮件会停用。离线介质仍可使用。操作在客户端里做。')
})
