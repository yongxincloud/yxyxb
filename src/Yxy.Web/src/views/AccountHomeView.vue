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
  <p class="card-yxy">{{ email }}</p>
</template>
