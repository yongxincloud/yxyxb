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
    expect(home).not.toContain('不限台数')
  })

  it('orders can select productNo from plans', () => {
    const orders = readFileSync(join(root, 'views/OrdersView.vue'), 'utf8')
    expect(orders).toContain('plans')
    expect(orders).toContain('productNo')
    expect(orders).not.toContain("'STD'")
  })
})
