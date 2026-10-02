<script setup>
import { onMounted, ref } from 'vue'
import { get } from '../api.js'

const info = ref(null)
const error = ref('')

onMounted(async () => {
  const data = await get('/api/portal/subscription')
  if (!data.ok) {
    error.value = '请先登录'
    return
  }
  info.value = data
})
</script>

<template>
  <section class="portal">
    <aside>
      <router-link to="/home">我的订阅</router-link>
      <router-link to="/orders">续费 / 试用</router-link>
      <router-link to="/portal/invoice">开票</router-link>
      <router-link to="/portal/provider">申请服务商</router-link>
      <router-link to="/portal/commission">我的佣金</router-link>
      <router-link to="/portal/download">客户端下载</router-link>
      <router-link to="/forgot">账号安全</router-link>
    </aside>
    <div class="card-yxy">
      <h1>我的订阅</h1>
      <p class="err" v-if="error">{{ error }}</p>
      <template v-if="info">
        <p>状态：{{ info.authStatus }}</p>
        <p>到期：{{ info.authEndTime || '暂无' }}</p>
        <p>下次联网校验：{{ info.nextCheckAt || '暂无' }}（间隔 {{ info.nextCheckDays }} 天）</p>
        <p>{{ info.usable ? '授权有效，客户端可用。' : '当前没有有效授权。一个账号不限台数。' }}</p>
        <router-link class="btn-yxy link" to="/orders">续费</router-link>
      </template>
    </div>
  </section>
</template>

<style scoped>
.portal { display: grid; grid-template-columns: 180px 1fr; gap: 16px; }
aside { display: grid; align-content: start; gap: 12px; }
aside a { color: var(--c-title); }
.link { display: inline-block; line-height: 43px; text-decoration: none; }
@media (max-width: 768px) {
  .portal { grid-template-columns: 1fr; }
}
</style>
