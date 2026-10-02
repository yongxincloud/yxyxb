<script setup>
import { onMounted, ref } from 'vue'
import { get } from '../api.js'

const info = ref(null)
const error = ref('')
const names = {
  none: '未开通',
  active: '有效',
  grace: '宽限期',
  expired: '已到期',
  paused: '已暂停',
  cancelled: '已取消'
}

onMounted(async () => {
  const data = await get('/api/portal/subscription')
  if (!data.ok) {
    error.value = '请先登录'
    return
  }
  info.value = data
})

function day(value) {
  if (!value) return '暂无'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '暂无'
  const pad = (n) => String(n).padStart(2, '0')
  return date.getFullYear() + '-' + pad(date.getMonth() + 1) + '-' + pad(date.getDate())
}
</script>

<template>
  <section class="card-yxy">
    <h1>我的订阅</h1>
    <p class="err" v-if="error">{{ error }}</p>
    <template v-if="info">
      <div class="stats">
        <article>
          <span>授权状态</span>
          <strong>{{ names[info.authStatus] || info.authStatus }}</strong>
        </article>
        <article>
          <span>到期日</span>
          <strong>{{ day(info.authEndTime) }}</strong>
        </article>
        <article>
          <span>下次联网校验</span>
          <strong>{{ day(info.nextCheckAt) }}</strong>
        </article>
      </div>
      <p class="note">{{ info.usable ? '授权有效，客户端可以使用。一个账号不限台数。' : '当前没有有效授权。文件备份、数据库备份、云、UNC、FTP、邮件在客户端会停用。离线介质仍可使用。' }}</p>
      <p class="note" v-if="info.nextCheckDays">联网校验间隔 {{ info.nextCheckDays }} 天。</p>
      <router-link class="btn-yxy link" to="/orders">续费或购买试用</router-link>
    </template>
  </section>
</template>

<style scoped>
.stats { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; }
article { background: var(--c-surface-alt); border-radius: 6px; padding: 16px; }
article span { display: block; color: var(--c-text-muted); font-size: 12px; margin-bottom: 6px; }
article strong { font-size: 18px; color: var(--c-title); }
.note { margin: 0; line-height: 1.7; }
.link { justify-self: start; display: inline-block; line-height: 43px; text-decoration: none; }
@media (max-width: 768px) {
  .stats { grid-template-columns: 1fr; }
}
</style>
