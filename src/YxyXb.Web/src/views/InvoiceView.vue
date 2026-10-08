<script setup>
import { onMounted, ref } from 'vue'
import { get, post } from '../api.js'

const orders = ref([])
const selected = ref([])
const ready = ref(false)
const message = ref('')

async function load() {
  const profile = await get('/api/account/profile')
  ready.value = !!(profile.ok && profile.invoiceTitle && profile.invoiceTaxNo && profile.invoiceEmail && profile.invoiceLicense)
  const data = await get('/api/portal/invoice-orders')
  orders.value = data.ok ? (data.orders || []) : []
  selected.value = orders.value.map((order) => order.orderNo)
}

async function submit() {
  message.value = ''
  if (!ready.value) {
    message.value = '请先在账号信息里填写开票信息'
    return
  }
  if (selected.value.length === 0) {
    message.value = '请选择有金额且未开票的支付记录'
    return
  }
  const data = await post('/api/portal/invoice-apply', { orderNos: selected.value })
  message.value = data.ok ? '已提交，由厂商开具。' : (data.error || '提交失败')
  if (data.ok) await load()
}

onMounted(load)
</script>

<template>
  <form class="card-yxy" @submit.prevent="submit">
    <h1>开票</h1>
    <p>选择有金额、已支付且未开票的记录。抬头、税号、邮箱和营业执照使用账号信息里保存的内容。</p>
    <p v-if="!ready">开票信息还没填完整。<router-link to="/portal/account">去账号信息填写</router-link></p>
    <fieldset class="orders">
      <legend>可开票的支付记录</legend>
      <label v-for="order in orders" :key="order.orderNo">
        <input type="checkbox" :value="order.orderNo" v-model="selected" />
        {{ order.orderNo }}　{{ order.amount }} 元
      </label>
      <p v-if="orders.length === 0">没有可开票的支付记录。</p>
    </fieldset>
    <p class="err" v-if="message">{{ message }}</p>
    <button class="btn-yxy" type="submit">提交开票</button>
  </form>
</template>

<style scoped>
.orders { border: 1px solid var(--c-border); border-radius: 12px; display: grid; gap: 8px; padding: 12px; }
.orders label { display: flex; gap: 8px; align-items: center; }
form { display: grid; gap: 12px; }
</style>
