<script setup>
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { get, post } from '../api.js'

const router = useRouter()
const username = ref('')

const links = [
  { href: '#customers', label: '客户' },
  { href: '#orders', label: '订单' },
  { href: '#auths', label: '授权' },
  { href: '#providers', label: '服务商' },
  { href: '#money', label: '佣金结算' },
  { href: '#mail', label: '发信服务器' },
  { href: '#ops', label: '操作' }
]

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
      <p class="brand">厂商后台</p>
      <p class="mail">{{ username || '未登录' }}</p>
      <nav>
        <a v-for="link in links" :key="link.href" :href="link.href">{{ link.label }}</a>
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
nav a:hover { background: #eef3ff; color: var(--brand-primary); }
.out { width: 100%; margin-top: 16px; cursor: pointer; }
.main :deep(h1) { margin: 0 0 8px; font-size: 22px; color: var(--c-title); }
@media (max-width: 768px) {
  .shell { grid-template-columns: 1fr; }
  aside { position: static; }
}
</style>
