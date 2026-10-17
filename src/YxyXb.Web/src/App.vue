<script setup>
import { watch } from 'vue'
import { useRoute } from 'vue-router'
import { isCustomerPath } from './theme.js'
import { applySeo } from './seo.js'

const route = useRoute()

watch(() => route.path, (path) => {
  document.body.classList.toggle('theme-customer', isCustomerPath(path))
  applySeo(route)
}, { immediate: true })
</script>

<template>
  <main class="page-wrap">
    <router-view />
  </main>
</template>
