<script setup>
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { get, post } from '../api.js'

const route = useRoute()
const router = useRouter()
const email = ref('')

const links = [
  { to: '/home', label: '我的订阅' },
  { to: '/orders', label: '续费 / 订单' },
  { to: '/portal/invoice', label: '开票' },
  { to: '/portal/provider', label: '申请服务商' },
  { to: '/portal/commission', label: '我的佣金' },
  { to: '/portal/customers', label: '我的客户' },
  { to: '/portal/settlements', label: '结算单' },
  { to: '/portal/messages', label: '站内信' },
  { to: '/portal/download', label: '客户端下载' },
  { to: '/portal/account', label: '账号信息' }
]

onMounted(async () => {
  const me = await get('/api/account/me')
  if (me.ok) email.value = me.email
})

function active(path) {
  return route.path === path
}

async function logout() {
  await post('/api/account/logout')
  router.replace('/login')
}
</script>

<template>
  <div class="shell">
    <aside>
      <img class="logo" src="/logo.jpg" alt="用心云" />
      <p class="mail">{{ email || '未登录' }}</p>
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
.mail { margin: 0 8px 16px; color: var(--c-text-muted); font-size: 12px; word-break: break-all; }
nav { display: grid; gap: 4px; }
nav a {
  display: block;
  padding: 10px 12px;
  border-radius: 6px;
  color: var(--c-text);
  text-decoration: none;
}
nav a.on, nav a:hover { background: #F8E8E8; color: var(--brand-primary); }
.out { width: 100%; margin-top: 12px; }
.main :deep(.card-yxy) { margin: 0; }
.main :deep(h1) { margin: 0 0 8px; font-size: 22px; color: var(--c-title); }
@media (max-width: 768px) {
  .shell { grid-template-columns: 1fr; }
  aside { position: static; }
}
</style>
