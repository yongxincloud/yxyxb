import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test } from 'vitest'

test('account screens have no social login', () => {
  const root = dirname(fileURLToPath(import.meta.url))
  const banned = ['微信', '支付宝', 'weixin', 'alipay', 'qq.com']
  const accountDir = join(root, 'views')
  const paths = ['LoginView.vue', 'RegisterView.vue', 'ForgotView.vue'].map((name) => join(accountDir, name))
  paths.push(join(root, '../index.html'))
  for (const path of paths) {
    if (path.endsWith('no-social.test.js')) continue
    const text = readFileSync(path, 'utf8').toLowerCase()
    for (const word of banned) expect(text, path).not.toContain(word.toLowerCase())
  }
})
