<script setup>
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { get } from '../api.js'

const router = useRouter()
const email = ref('')
onMounted(async () => {
  const data = await get('/api/account/me')
  if (!data.ok) {
    router.push('/login')
    return
  }
  email.value = data.email
})
</script>

<template>
  <section class="card-yxy">
    <p>{{ email }}</p>
    <router-link to="/orders">我的订单</router-link>
  </section>
</template>
