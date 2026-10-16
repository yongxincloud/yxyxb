<script setup>
import { onMounted, ref } from 'vue'
import { get, post } from '../api.js'

const maxDevices = ref(null)
const devices = ref([])
const error = ref('')
const message = ref('')
const savingId = ref('')

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

async function saveNote(row) {
  savingId.value = row.deviceId
  error.value = ''
  message.value = ''
  const data = await post('/api/portal/devices/note', { deviceId: row.deviceId, note: row.note || '' })
  savingId.value = ''
  if (!data.ok) {
    error.value = data.error || '保存失败'
    return
  }
  message.value = '备注已保存'
  await load()
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
        <div class="head">
          <div>
            <strong>{{ row.displayName || '未上报名称' }}</strong>
            <span class="muted">设备 ID：{{ row.deviceId }}</span>
            <span class="muted" v-if="row.displayName">格式：电脑名 · MAC · Windows 用户</span>
          </div>
          <button class="btn-ghost" type="button" @click="kick(row.deviceId)">踢出</button>
        </div>
        <label class="note">
          <span>使用人</span>
          <input class="field-yxy" v-model="row.note" maxlength="40" placeholder="程序启动后写入客户标识，也可在这里改" />
          <button class="btn-ghost" type="button" :disabled="savingId === row.deviceId" @click="saveNote(row)">保存</button>
        </label>
      </article>
      <p v-if="!error && !devices.length">暂无占用中的设备。</p>
    </div>
  </section>
</template>

<style scoped>
.lead { margin: 0 0 12px; line-height: 1.7; }
.list { display: grid; gap: 12px; }
.list article { display: grid; gap: 10px; padding: 12px 0; border-top: 1px solid var(--c-border); }
.head { display: flex; justify-content: space-between; align-items: center; gap: 12px; }
.note { display: flex; align-items: center; gap: 8px; }
.note span { flex: none; color: var(--c-text-muted); font-size: 13px; }
.note .field-yxy { flex: 1; width: auto; }
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
