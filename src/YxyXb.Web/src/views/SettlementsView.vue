<script setup>
import { onMounted, ref } from 'vue'
import { get } from '../api.js'

const rows = ref([])
const names = { 1: '待审批', 2: '已批准', 3: '已付款', 4: '已驳回' }
onMounted(async () => {
  const data = await get('/api/portal/settlements')
  rows.value = data.rows || []
})
</script>

<template>
  <section class="card-yxy">
    <h1>结算单</h1>
    <p v-for="row in rows" :key="row.settlementNo">{{ row.settlementNo }} {{ row.amount }} 元 {{ names[row.status] || row.status }}</p>
    <router-link to="/home">返回</router-link>
  </section>
</template>
