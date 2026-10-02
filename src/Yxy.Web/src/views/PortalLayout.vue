<script setup>
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { get } from '../api.js'

const route = useRoute()
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
  { to: '/forgot', label: '账号安全' }
]

onMounted(async () => {
  const me = await get('/api/account/me')
  if (me.ok) email.value = me.email
})

function active(path) {
  return route.path === path
}
</script>

<template>
  <div class="shell">
    <aside>
      <p class="brand">用心云备份</p>
      <p class="mail">{{ email || '未登录' }}</p>
      <nav>
        <router-link v-for="link in links" :key="link.to" :to="link.to" :class="{ on: active(link.to) }">
          {{ link.label }}
        </router-link>
      </nav>
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
.brand { margin: 0 8px 4px; font-size: 16px; font-weight: 700; color: var(--c-title); }
.mail { margin: 0 8px 16px; color: var(--c-text-muted); font-size: 12px; word-break: break-all; }
nav { display: grid; gap: 4px; }
nav a {
  display: block;
  padding: 10px 12px;
  border-radius: 6px;
  color: var(--c-text);
  text-decoration: none;
}
nav a.on, nav a:hover { background: #eef3ff; color: var(--brand-primary); }
.main :deep(.card-yxy) { margin: 0; }
.main :deep(h1) { margin: 0 0 8px; font-size: 22px; color: var(--c-title); }
@media (max-width: 768px) {
  .shell { grid-template-columns: 1fr; }
  aside { position: static; }
}
</style>
