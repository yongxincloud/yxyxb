import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

const root = dirname(fileURLToPath(import.meta.url))

describe('edition plans UI', () => {
  it('home binds plans and drops unlimited-device copy', () => {
    const home = readFileSync(join(root, 'views/HomeView.vue'), 'utf8')
    expect(home).toContain('plans')
    expect(home).toContain('maxDevices')
    expect(home).toContain('在售商品')
    expect(home).toContain('商品名称')
    expect(home).toContain('个人版')
    expect(home).toContain('标准版')
    expect(home).toContain('专业版')
    expect(home).toContain('listPrice: 36')
    expect(home).toContain('listPrice: 98')
    expect(home).toContain('listPrice: 198')
    expect(home).toContain('服务期限 365 天')
    expect(home).not.toContain('不限台数')
    expect(home).not.toContain('价格加载中')
  })

  it('orders can select productNo from plans', () => {
    const orders = readFileSync(join(root, 'views/OrdersView.vue'), 'utf8')
    expect(orders).toContain('plans')
    expect(orders).toContain('productNo')
    expect(orders).not.toContain("'STD'")
  })
})
