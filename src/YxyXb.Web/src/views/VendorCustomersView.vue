<script setup>
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { get, post } from '../api.js'
import { day, periodText, useVendorBoard } from '../vendorBoard.js'

const { board, loadError, load } = useVendorBoard()
const days = ref(365)
const reason = ref('手工开通')
const grantProductNo = ref('STANDARD')
const grantDevices = ref(10)
const message = ref('')
const busyId = ref(0)
const editionDraft = reactive({})
const deviceDraft = reactive({})
const keyword = ref('')
const rows = ref([])
const listError = ref('')

const editionOptions = computed(() =>
  (board.value?.products || []).filter((p) => p.status === 1)
)

function text(value) {
  const shown = String(value ?? '').trim()
  return shown || '—'
}

onMounted(async () => {
  await load()
  if (board.value) await loadCustomers()
})

watch(rows, (list) => {
  for (const row of list || []) {
    editionDraft[row.id] = row.productNo || 'STANDARD'
    deviceDraft[row.id] = row.maxDevices || ''
  }
})

function editionText(product) {
  if (product.productNo === 'CUSTOM') return product.name + '（人数自定）'
  return product.name + '（' + product.maxDevices + ' 台）'
}

function deviceCount(raw) {
  const count = Number(raw)
  if (!count || count < 1) return null
  return Math.floor(count)
}

async function loadCustomers() {
  listError.value = ''
  const q = keyword.value.trim()
  const path = q ? '/api/vendor/customers?q=' + encodeURIComponent(q) : '/api/vendor/customers'
  try {
    const data = await get(path)
    if (!data.ok) {
      listError.value = data.error || '暂时读不到客户'
      rows.value = []
      return
    }
    rows.value = data.rows || []
  } catch {
    listError.value = '暂时读不到客户'
    rows.value = []
  }
}

async function remove(row) {
  message.value = ''
  if (!window.confirm('删除 ' + row.email + ' ？账号、订单和授权会一起去掉，不能恢复。')) return
  busyId.value = row.id
  const data = await post('/api/vendor/customers/' + row.id + '/delete', {})
  busyId.value = 0
  message.value = data.ok ? '已删除 ' + row.email : (data.error || '删除失败')
  if (data.ok) await loadCustomers()
}

async function extend(row) {
  message.value = ''
  const count = Number(days.value)
  if (!count || count < 1) {
    message.value = '天数要大于 0'
    return
  }
  const productNo = grantProductNo.value || 'STANDARD'
  const maxDevices = productNo === 'CUSTOM' ? deviceCount(grantDevices.value) : null
  if (productNo === 'CUSTOM' && !maxDevices) {
    message.value = '定制版人数要大于 0'
    return
  }
  busyId.value = row.id
  const data = await post('/api/vendor/grants', {
    email: row.email,
    days: count,
    reason: reason.value || '手工开通',
    productNo,
    maxDevices
  })
  busyId.value = 0
  message.value = data.ok
    ? row.email + ' 可用至 ' + day(data.authEndTime)
    : (data.error || '增加失败')
  if (data.ok) await loadCustomers()
}

async function changeEdition(row) {
  message.value = ''
  const productNo = editionDraft[row.id]
  if (!productNo) {
    message.value = '请选择账号类型'
    return
  }
  const maxDevices = productNo === 'CUSTOM' ? deviceCount(deviceDraft[row.id]) : null
  if (productNo === 'CUSTOM' && !maxDevices) {
    message.value = '定制版人数要大于 0'
    return
  }
  if (productNo === row.productNo && (productNo !== 'CUSTOM' || maxDevices === row.maxDevices)) {
    message.value = '账号类型未变化'
    return
  }
  const label = editionOptions.value.find((p) => p.productNo === productNo)?.name || productNo
  const people = productNo === 'CUSTOM' ? '，' + maxDevices + ' 人' : ''
  if (!window.confirm('将 ' + row.email + ' 改为「' + label + '」' + people + '？')) return
  busyId.value = row.id
  const data = await post('/api/vendor/customers/' + row.id + '/edition', {
    productNo,
    maxDevices,
    reason: reason.value || '后台改版'
  })
  busyId.value = 0
  message.value = data.ok
    ? row.email + ' 已改为 ' + (data.productName || productNo)
    : (data.error || '修改失败')
  if (data.ok) await loadCustomers()
}
</script>

<template>
  <p class="err" v-if="loadError">{{ loadError }}</p>
  <section v-if="board" class="card-yxy">
    <h1>客户</h1>
    <p class="lead">注册后的账号会出现在这里。支付未接通时，填写天数后点该行的「增加期限」。可单独修改账号类型（个人/标准/专业/定制）。定制版要填写人数。</p>
    <p v-if="message" :class="message.includes('失败') || message.includes('要大于') || message.includes('未变化') || message.includes('请选择') ? 'err' : 'ok'">{{ message }}</p>
    <div class="row-form">
      <label>增加天数<input class="field-yxy" v-model="days" type="number" min="1" /></label>
      <label>开通类型
        <select class="field-yxy" v-model="grantProductNo">
          <option v-for="p in editionOptions" :key="p.productNo" :value="p.productNo">
            {{ editionText(p) }}
          </option>
        </select>
      </label>
      <label v-if="grantProductNo === 'CUSTOM'">人数<input class="field-yxy devices" v-model="grantDevices" type="number" min="1" max="9999" /></label>
      <label>事由<input class="field-yxy reason" v-model="reason" /></label>
    </div>
    <form class="row-form" @submit.prevent="loadCustomers">
      <label>查询<input class="field-yxy" v-model="keyword" placeholder="姓名、邮箱、手机、公司、抬头" /></label>
      <button class="btn-yxy" type="submit">查询</button>
    </form>
    <p class="err" v-if="listError">{{ listError }}</p>
    <table>
      <thead>
        <tr>
          <th>个人信息</th>
          <th>邮箱</th>
          <th>客户号</th>
          <th>开票信息</th>
          <th>账号类型</th>
          <th>归属</th>
          <th>注册日</th>
          <th>到期日</th>
          <th>状态</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="row in rows" :key="row.id">
          <td class="stack">
            <div>姓名 {{ text(row.name) }}</div>
            <div>昵称 {{ text(row.nickname) }}</div>
            <div>公司 {{ text(row.companyName) }}</div>
            <div>手机 {{ text(row.phone) }}</div>
          </td>
          <td>{{ row.email }}</td>
          <td>{{ row.customerNo }}</td>
          <td class="stack">
            <div>抬头 {{ text(row.invoiceTitle) }}</div>
            <div>税号 {{ text(row.invoiceTaxNo) }}</div>
            <div>收票邮箱 {{ text(row.invoiceEmail) }}</div>
            <div>执照文件名 {{ text(row.invoiceLicense) }}</div>
          </td>
          <td>
            <div class="edition">
              <select class="field-yxy" v-model="editionDraft[row.id]">
                <option v-for="p in editionOptions" :key="p.productNo" :value="p.productNo">
                  {{ editionText(p) }}
                </option>
              </select>
              <input v-if="editionDraft[row.id] === 'CUSTOM'" class="field-yxy devices" v-model="deviceDraft[row.id]" type="number" min="1" max="9999" placeholder="人数" />
              <button class="btn-yxy mini" type="button" :disabled="busyId === row.id" @click="changeEdition(row)">修改</button>
            </div>
            <span class="muted" v-if="!row.productNo">尚未开通</span>
          </td>
          <td>{{ row.directProviderId || '厂商' }}</td>
          <td>{{ day(row.createdAt) }}</td>
          <td>{{ day(row.authEndTime) }}</td>
          <td>{{ periodText(row.authEndTime) }}</td>
          <td>
            <button class="btn-yxy mini" type="button" :disabled="busyId === row.id" @click="extend(row)">增加期限</button>
            <button class="btn-yxy mini" type="button" :disabled="busyId === row.id" @click="remove(row)">删除</button>
          </td>
        </tr>
        <tr v-if="!rows.length"><td colspan="10">{{ keyword.trim() ? '没有匹配的客户' : '暂无客户。用户先在网站注册，再回到这里开通。' }}</td></tr>
      </tbody>
    </table>
  </section>
</template>

<style scoped>
.edition { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
.edition .field-yxy { min-width: 140px; }
.devices { width: 90px; min-width: 90px; }
.muted { display: block; margin-top: 4px; color: var(--c-text-muted); font-size: 12px; }
.row-form { display: flex; flex-wrap: wrap; gap: 12px; margin-bottom: 12px; align-items: end; }
.row-form label { display: flex; flex-direction: column; gap: 4px; font-size: 13px; }
.reason { min-width: 180px; }
.stack { font-size: 13px; line-height: 1.5; white-space: nowrap; }
</style>
