<script setup>
import { onMounted, ref } from 'vue'
import { get } from '../api.js'

const emails = ref([])
const error = ref('')
onMounted(async () => {
  const data = await get('/api/portal/customers')
  if (!data.ok) error.value = data.error || '还不是服务商'
  else emails.value = data.emails || []
})
</script>

<template>
  <section class="card-yxy">
    <h1>我的客户</h1>
    <p class="err" v-if="error">{{ error }}</p>
    <p v-for="email in emails" :key="email">{{ email }}</p>
    <router-link to="/home">返回</router-link>
  </section>
</template>
