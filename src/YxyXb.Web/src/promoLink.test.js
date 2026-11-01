import { expect, test } from 'vitest'
import { toAbsolutePromoUrl } from './promoLink.js'

test('joins a relative promo path onto the current origin', () => {
  expect(toAbsolutePromoUrl('/register?provider_no=P1', 'https://backup.yongxincloud.cn'))
    .toBe('https://backup.yongxincloud.cn/register?provider_no=P1')
})

test('keeps an already-absolute promo url', () => {
  expect(toAbsolutePromoUrl('https://backup.yongxincloud.cn/register?provider_no=P1', 'http://localhost:5173'))
    .toBe('https://backup.yongxincloud.cn/register?provider_no=P1')
})
