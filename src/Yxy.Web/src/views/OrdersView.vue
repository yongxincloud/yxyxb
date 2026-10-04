<script setup>
import { onMounted, ref } from 'vue'
import { get, post } from '../api.js'

const price = ref('')
const trial1 = ref('')
const trial2 = ref('')
const orders = ref([])
const error = ref('')
const refunding = ref(false)

async function load() {
  const catalog = await get('/api/orders/catalog')
  if (!catalog.ok) {
    error.value = '请先登录'
    return
  }
  price.value = catalog.price
  trial1.value = catalog.trialPrice1
  trial2.value = catalog.trialPrice2
  const list = await get('/api/orders')
  orders.value = list.orders || []
}

async function refund(orderNo) {
  if (refunding.value) return
  refunding.value = true
  error.value = '正在向支付宝退款…'
  try {
    const data = await post('/api/orders/' + orderNo + '/refund', {})
    error.value = data.ok ? '已退款' : (data.error || '退款失败')
    await load()
  } catch {
    error.value = '退款失败，请再点一次'
  } finally {
    refunding.value = false
  }
}

async function remove(orderNo) {
  error.value = ''
  const data = await post('/api/orders/' + orderNo + '/delete', {})
  error.value = data.ok ? '已删除' : (data.error || '删除失败')
  await load()
}

async function buy(orderType) {
  error.value = ''
  const created = await post('/api/orders', { productNo: 'STD', orderType, channel: 'alipay' })
  if (!created.ok) {
    error.value = created.error || '下单失败'
    return
  }
  const pay = await post('/api/orders/' + created.orderNo + '/alipay', {})
  if (!pay.ok || !pay.payUrl) {
    error.value = pay.error || '支付失败'
    return
  }
  window.location.href = pay.payUrl
}

onMounted(load)
</script>

<template>
  <section class="card-yxy">
    <h1>续费 / 订单</h1>
    <p class="lead">标准版 {{ price }} 元 / 年。试用第 1 次 {{ trial1 }} 元，第 2 次 {{ trial2 }} 元。续费按当时价格，在原到期日上顺延。</p>
    <p class="lead">要开票由厂商开具，也可以不开票。一个账号不限台数。</p>
    <div class="pay">
      <button class="btn-yxy" type="button" @click="buy(1)">购买一年</button>
      <button class="btn-ghost" type="button" @click="buy(2)">购买试用</button>
    </div>
    <p class="err" v-if="error">{{ error }}</p>
    <div class="list">
      <article v-for="order in orders" :key="order.orderNo">
        <div>
          <strong>{{ order.orderNo }}</strong>
          <span>{{ order.amount }} 元</span>
        </div>
        <span class="tag">{{ order.payStatus === 1 ? '已支付' : order.payStatus === 2 ? '已退款' : order.payStatus === 3 ? '已关闭' : '未支付' }}</span>
        <button v-if="order.payStatus === 1" class="btn-ghost" type="button" :disabled="refunding" @click="refund(order.orderNo)">{{ refunding ? '退款中' : '退款' }}</button>
        <button v-else class="btn-ghost" type="button" @click="remove(order.orderNo)">删除</button>
      </article>
      <p v-if="orders.length === 0">还没有订单。</p>
    </div>
  </section>
</template>

<style scoped>
.lead { margin: 0; line-height: 1.7; }
.pay { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; }
.btn-ghost {
  height: 43px;
  padding: 0 18px;
  border-radius: var(--radius-pill);
  border: 1px solid var(--c-border);
  background: #fff;
  color: var(--brand-primary);
  cursor: pointer;
}
.list { display: grid; gap: 8px; }
article {
  display: grid;
  grid-template-columns: 1fr auto auto;
  gap: 12px;
  align-items: center;
  padding: 12px 0;
  border-top: 1px solid var(--c-border);
}
article strong { display: block; color: var(--c-title); }
.tag { color: var(--c-text-muted); }
</style>
