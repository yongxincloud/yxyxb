<script setup>
import { onMounted, ref } from 'vue'
import { get, post } from '../api.js'

const rows = ref([])
const promo = ref('')
const error = ref('')
const invoiceFile = ref('')
const message = ref('')

onMounted(async () => {
  const code = await get('/api/portal/promo')
  if (code.ok) promo.value = code.url
  const data = await get('/api/portal/commissions')
  if (!data.ok) {
    error.value = data.error || '还不是服务商'
    return
  }
  rows.value = data.rows || []
})

async function settle() {
  message.value = ''
  const data = await post('/api/portal/settlements', { invoiceFile: invoiceFile.value })
  message.value = data.ok ? '已提交结算 ' + data.amount + ' 元' : (data.error || '结算失败')
}
</script>

<template>
  <section class="card-yxy">
    <h1>我的佣金</h1>
    <p v-if="promo">推广链接：{{ promo }}</p>
    <p class="err" v-if="error">{{ error }}</p>
    <p v-for="row in rows" :key="row.id">订单 {{ row.orderId }} 佣金 {{ row.amount }} 元</p>
    <input class="field-yxy" v-model="invoiceFile" placeholder="佣金发票文件名，如 invoice.pdf" />
    <button class="btn-yxy" type="button" @click="settle">发起结算</button>
    <p v-if="message">{{ message }}</p>
    <router-link to="/home">返回</router-link>
  </section>
</template>
