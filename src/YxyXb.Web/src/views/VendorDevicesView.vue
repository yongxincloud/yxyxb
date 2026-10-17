<script setup>
import { computed, onMounted, ref } from 'vue'
import { get } from '../api.js'
import { day } from '../vendorBoard.js'

const keyword = ref('')
const rows = ref([])
const error = ref('')
const page = ref(1)
const pages = ref(1)
const total = ref(0)
const pageSize = 20
const rangeText = computed(() => '第 ' + page.value + ' / ' + pages.value + ' 页，共 ' + total.value + ' 条')

function text(value) {
  const shown = String(value ?? '').trim()
  return shown || '—'
}

function statusText(status) {
  return status === 1 ? '占用中' : '已踢出'
}

async function load(nextPage) {
  error.value = ''
  if (typeof nextPage === 'number') page.value = nextPage
  const q = keyword.value.trim()
  const path = '/api/vendor/devices?page=' + page.value + '&pageSize=' + pageSize
    + (q ? '&q=' + encodeURIComponent(q) : '')
  try {
    const data = await get(path)
    if (!data.ok) {
      error.value = data.error || '暂时读不到设备'
      rows.value = []
      total.value = 0
      pages.value = 1
      return
    }
    rows.value = data.rows || []
    total.value = data.total || 0
    page.value = data.page || 1
    pages.value = data.pages || 1
  } catch {
    error.value = '暂时读不到设备'
    rows.value = []
    total.value = 0
    pages.value = 1
  }
}

function search() {
  load(1)
}

onMounted(load)
</script>

<template>
  <section class="card-yxy">
    <h1>设备查询</h1>
    <p class="lead">按邮箱、姓名、客户号、设备名或备注查找。备注是客户端写入的客户标识，也可在用户的「我的设备」里修改。</p>
    <form class="row-form" @submit.prevent="search">
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
    <div class="pager">
      <button class="btn-yxy mini" type="button" :disabled="page <= 1" @click="load(page - 1)">上一页</button>
      <span>{{ rangeText }}</span>
      <button class="btn-yxy mini" type="button" :disabled="page >= pages" @click="load(page + 1)">下一页</button>
    </div>
  </section>
</template>

<style scoped>
.lead { margin: 0 0 12px; line-height: 1.7; }
.row-form { display: flex; flex-wrap: wrap; gap: 12px; margin-bottom: 12px; align-items: end; }
.row-form label { display: flex; flex-direction: column; gap: 4px; font-size: 13px; }
.row-form .field-yxy { min-width: 280px; }
.id { font-size: 12px; word-break: break-all; }
.pager { display: flex; align-items: center; gap: 12px; margin-top: 12px; }
.pager span { color: var(--c-text-muted); font-size: 13px; }
</style>
