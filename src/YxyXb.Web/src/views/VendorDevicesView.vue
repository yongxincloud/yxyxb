<script setup>
import { onMounted, ref } from 'vue'
import { get } from '../api.js'
import { day } from '../vendorBoard.js'

const keyword = ref('')
const rows = ref([])
const error = ref('')

function text(value) {
  const shown = String(value ?? '').trim()
  return shown || '—'
}

function statusText(status) {
  return status === 1 ? '占用中' : '已踢出'
}

async function load() {
  error.value = ''
  const q = keyword.value.trim()
  const path = q ? '/api/vendor/devices?q=' + encodeURIComponent(q) : '/api/vendor/devices'
  try {
    const data = await get(path)
    if (!data.ok) {
      error.value = data.error || '暂时读不到设备'
      rows.value = []
      return
    }
    rows.value = data.rows || []
  } catch {
    error.value = '暂时读不到设备'
    rows.value = []
  }
}

onMounted(load)
</script>

<template>
  <section class="card-yxy">
    <h1>设备查询</h1>
    <p class="lead">按邮箱、姓名、客户号、设备名或备注查找。备注是客户端写入的客户标识，也可在用户的「我的设备」里修改。</p>
    <form class="row-form" @submit.prevent="load">
      <label>查询<input class="field-yxy" v-model="keyword" placeholder="邮箱、姓名、客户号、设备名、备注" /></label>
      <button class="btn-yxy" type="submit">查询</button>
    </form>
    <p class="err" v-if="error">{{ error }}</p>
    <table>
      <thead>
        <tr>
          <th>邮箱</th>
          <th>姓名</th>
          <th>客户号</th>
          <th>设备名称</th>
          <th>设备 ID</th>
          <th>备注</th>
          <th>状态</th>
          <th>最近出现</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="row in rows" :key="row.email + row.deviceId">
          <td>{{ row.email }}</td>
          <td>{{ text(row.name) }}</td>
          <td>{{ text(row.customerNo) }}</td>
          <td>{{ text(row.displayName) }}</td>
          <td class="id">{{ row.deviceId }}</td>
          <td>{{ text(row.note) }}</td>
          <td>{{ statusText(row.status) }}</td>
          <td>{{ day(row.lastSeenAt) }}</td>
        </tr>
        <tr v-if="!error && !rows.length">
          <td colspan="8">{{ keyword.trim() ? '没有匹配的设备' : '暂无设备。' }}</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>

<style scoped>
.lead { margin: 0 0 12px; line-height: 1.7; }
.row-form { display: flex; flex-wrap: wrap; gap: 12px; margin-bottom: 12px; align-items: end; }
.row-form label { display: flex; flex-direction: column; gap: 4px; font-size: 13px; }
.row-form .field-yxy { min-width: 280px; }
.id { font-size: 12px; word-break: break-all; }
</style>
