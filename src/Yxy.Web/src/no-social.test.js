import { readdirSync, readFileSync, statSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test } from 'vitest'

function filesUnder(dir) {
  const found = []
  for (const name of readdirSync(dir)) {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) found.push(...filesUnder(path))
    else if (name.endsWith('.vue') || name.endsWith('.html') || name.endsWith('.js')) found.push(path)
  }
  return found
}

test('account screens have no social login', () => {
  const root = dirname(fileURLToPath(import.meta.url))
  const banned = ['微信', '支付宝', 'weixin', 'alipay', 'qq.com']
  const paths = [...filesUnder(root), join(root, '../index.html')]
  for (const path of paths) {
    if (path.endsWith('no-social.test.js')) continue
    const text = readFileSync(path, 'utf8').toLowerCase()
    for (const word of banned) expect(text, path).not.toContain(word.toLowerCase())
  }
})
