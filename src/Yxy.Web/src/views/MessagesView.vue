<script setup>
import { onMounted, ref } from 'vue'
import { get } from '../api.js'

const rows = ref([])
onMounted(async () => {
  const data = await get('/api/portal/messages')
  rows.value = data.rows || []
})
</script>

<template>
  <section class="card-yxy">
    <h1>站内信</h1>
    <p v-if="rows.length === 0">暂无消息</p>
    <article v-for="(row, index) in rows" :key="index">
      <h2>{{ row.title }}</h2>
      <p>{{ row.content }}</p>
    </article>
    <router-link to="/home">返回</router-link>
  </section>
</template>
