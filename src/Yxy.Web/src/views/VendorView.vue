<script setup>
import { ref } from 'vue'

const email = ref('')
const days = ref(30)
const reason = ref('赠送')
const price = ref('')
const message = ref('')
const logs = ref([])

async function call(path, body) {
  const res = await fetch(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-Vendor-Key': 'yxy-vendor' },
    body: JSON.stringify(body)
  })
  return res.json()
}

async function grant() {
  const data = await call('/api/vendor/grants', { email: email.value, days: Number(days.value), reason: reason.value })
  message.value = data.ok ? '已发放至 ' + data.authEndTime : (data.error || '发放失败')
  await loadLogs()
}

async function changePrice() {
  const data = await call('/api/vendor/products/STD/price', { price: Number(price.value) })
  message.value = data.ok ? '价格已更新' : (data.error || '改价失败')
  await loadLogs()
}

async function loadLogs() {
  const res = await fetch('/api/vendor/logs', { headers: { 'X-Vendor-Key': 'yxy-vendor' } })
  const data = await res.json()
  logs.value = data.rows || []
}

loadLogs()
</script>

<template>
  <section class="card-yxy">
    <h1>厂商后台</h1>
    <input class="field-yxy" v-model="email" placeholder="已注册邮箱" />
    <input class="field-yxy" v-model="days" type="number" placeholder="天数" />
    <input class="field-yxy" v-model="reason" placeholder="事由" />
    <button class="btn-yxy" type="button" @click="grant">发放授权</button>
    <input class="field-yxy" v-model="price" placeholder="标准版新价格" />
    <button class="btn-yxy" type="button" @click="changePrice">修改价格</button>
    <p v-if="message">{{ message }}</p>
    <p v-for="(row, index) in logs" :key="index">{{ row.module }} {{ row.action }} {{ row.beforeValue }} → {{ row.afterValue }}</p>
  </section>
</template>
