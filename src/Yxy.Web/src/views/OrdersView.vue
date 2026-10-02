<script setup>
import { onMounted, ref } from 'vue'
import { get, post } from '../api.js'

const price = ref('')
const trial1 = ref('')
const trial2 = ref('')
const orders = ref([])
const error = ref('')
const channel = ref('wechat')

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

async function buy(orderType) {
  error.value = ''
  const created = await post('/api/orders', { productNo: 'STD', orderType, channel: channel.value })
  if (!created.ok) {
    error.value = created.error || '下单失败'
    return
  }
  const paid = await post('/api/orders/' + created.orderNo + '/mock-pay', {})
  if (!paid.ok) {
    error.value = paid.error || '支付失败'
    return
  }
  await load()
}

onMounted(load)
</script>

<template>
  <section class="card-yxy">
    <h1>订单</h1>
    <p>标准版 {{ price }} 元 / 年。试用第 1 次 {{ trial1 }} 元，第 2 次 {{ trial2 }} 元。</p>
    <p>要开票由厂商开具，也可以不开票。一个账号不限台数。</p>
    <label><input type="radio" value="wechat" v-model="channel" /> 微信</label>
    <label><input type="radio" value="alipay" v-model="channel" /> 支付宝</label>
    <button class="btn-yxy" type="button" @click="buy(1)">购买一年</button>
    <button class="btn-yxy" type="button" @click="buy(2)">购买试用</button>
    <p class="err" v-if="error">{{ error }}</p>
    <p v-for="order in orders" :key="order.orderNo">
      {{ order.orderNo }} {{ order.amount }} 元
      {{ order.payStatus === 1 ? '已支付' : order.payStatus === 3 ? '已关闭' : '未支付' }}
    </p>
    <router-link to="/home">返回</router-link>
  </section>
</template>
