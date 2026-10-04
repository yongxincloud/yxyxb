<script setup>
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { get, post } from '../api.js'

const router = useRouter()
const route = useRoute()
const username = ref('')

const links = [
  { to: '/vendor', label: '概览' },
  { to: '/vendor/customers', label: '客户' },
  { to: '/vendor/orders', label: '订单' },
  { to: '/vendor/auths', label: '授权记录' },
  { to: '/vendor/providers', label: '服务商' },
  { to: '/vendor/money', label: '佣金结算' },
  { to: '/vendor/products', label: '产品定价' },
  { to: '/vendor/mail', label: '发信服务器' },
  { to: '/vendor/logs', label: '操作日志' }
]

function active(path) {
  return route.path === path
}

onMounted(async () => {
  const me = await get('/api/vendor/me')
  if (!me.ok) {
    router.replace('/vendor/login')
    return
  }
  username.value = me.username
})

async function logout() {
  await post('/api/vendor/logout')
  router.replace('/vendor/login')
}
</script>

<template>
  <div class="shell">
    <aside>
      <img class="logo" src="/logo.jpg" alt="用心云" />
      <p class="brand">厂商后台</p>
      <p class="mail">{{ username || '未登录' }}</p>
      <nav>
        <router-link v-for="link in links" :key="link.to" :to="link.to" :class="{ on: active(link.to) }">
          {{ link.label }}
        </router-link>
      </nav>
      <button class="btn-yxy out" type="button" @click="logout">退出</button>
    </aside>
    <div class="main">
      <router-view />
    </div>
  </div>
</template>

<style scoped>
.shell {
  display: grid;
  grid-template-columns: 220px 1fr;
  gap: 20px;
  align-items: start;
}
aside {
  background: #fff;
  border-radius: 8px;
  padding: 20px 14px;
  position: sticky;
  top: 16px;
}
.logo { height: 72px; width: auto; display: block; margin: 0 auto 8px; }
.brand { margin: 0 8px 4px; font-size: 16px; font-weight: 700; color: var(--c-title); }
.mail { margin: 0 8px 16px; color: var(--c-text-muted); font-size: 12px; }
nav { display: grid; gap: 4px; }
nav a {
  display: block;
  padding: 10px 12px;
  border-radius: 6px;
  color: var(--c-text);
  text-decoration: none;
}
nav a.on, nav a:hover { background: #F8E8E8; color: var(--brand-primary); }
.out { width: 100%; margin-top: 16px; cursor: pointer; }
.main :deep(h1) { margin: 0 0 8px; font-size: 22px; color: var(--c-title); }
.main :deep(h2) { margin: 8px 0 0; font-size: 16px; color: var(--c-title); }
.main :deep(.lead) { margin: 0; color: var(--c-text-muted); }
.main :deep(.ok) { margin: 0; color: var(--brand-green); }
.main :deep(table) { width: 100%; border-collapse: collapse; font-size: 13px; }
.main :deep(th), .main :deep(td) { text-align: left; padding: 8px 6px; border-bottom: 1px solid var(--c-border); vertical-align: middle; }
.main :deep(th) { color: var(--c-text-muted); font-weight: 500; }
.main :deep(.row-form) { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
.main :deep(.row-form label) { display: flex; align-items: center; gap: 8px; color: var(--c-title); font-size: 13px; }
.main :deep(.row-form .field-yxy) { width: 110px; }
.main :deep(.row-form .reason) { width: 180px; }
.main :deep(.mini) { height: 32px; font-size: 13px; padding: 0 14px; }
.main :deep(button) { cursor: pointer; }
.main :deep(.product) { display: grid; gap: 8px; }
@media (max-width: 768px) {
  .shell { grid-template-columns: 1fr; }
  aside { position: static; }
}
</style>
