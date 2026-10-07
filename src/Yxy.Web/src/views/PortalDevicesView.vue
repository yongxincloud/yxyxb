<script setup>
import { onMounted, ref } from 'vue'
import { get, post } from '../api.js'

const maxDevices = ref(null)
const devices = ref([])
const error = ref('')
const message = ref('')

async function load() {
  error.value = ''
  const data = await get('/api/portal/devices')
  if (!data.ok) {
    error.value = data.error || '加载失败'
    return
  }
  maxDevices.value = data.maxDevices
  devices.value = data.devices || []
}

async function kick(deviceId) {
  if (!window.confirm('确定踢出设备 ' + deviceId + '？该设备上的登录将立即失效。')) return
  const data = await post('/api/portal/devices/kick', { deviceId })
  message.value = data.ok ? '已踢出' : (data.error || '踢出失败')
  await load()
}

onMounted(load)
</script>

<template>
  <section class="card-yxy">
    <h1>我的设备</h1>
    <p class="lead">当前上限 {{ maxDevices ?? '—' }} 台。退出登录不释放名额，需主动踢出。</p>
    <p class="err" v-if="error">{{ error }}</p>
    <p class="lead" v-if="message">{{ message }}</p>
    <div class="list">
      <article v-for="row in devices" :key="row.deviceId">
        <div>
          <strong>{{ row.displayName || '未上报名称' }}</strong>
          <span class="muted">设备 ID：{{ row.deviceId }}</span>
          <span class="muted" v-if="row.displayName">格式：电脑名 · MAC · Windows 用户</span>
        </div>
        <button class="btn-ghost" type="button" @click="kick(row.deviceId)">踢出</button>
      </article>
      <p v-if="!devices.length">暂无占用中的设备。</p>
    </div>
  </section>
</template>

<style scoped>
.lead { margin: 0 0 12px; line-height: 1.7; }
.list { display: grid; gap: 12px; }
.list article { display: flex; justify-content: space-between; align-items: center; gap: 12px; padding: 12px 0; border-top: 1px solid var(--c-border); }
.muted { display: block; color: var(--c-text-muted); font-size: 13px; }
.btn-ghost {
  height: 36px;
  padding: 0 14px;
  border-radius: var(--radius-pill);
  border: 1px solid var(--c-border);
  background: #fff;
  color: var(--brand-primary);
  cursor: pointer;
}
.err { color: var(--c-danger, #7A1F1A); }
</style>
