<script setup>
import { onMounted, ref } from 'vue'
import { post } from '../api.js'
import { day, periodText, useVendorBoard } from '../vendorBoard.js'

const { board, loadError, load } = useVendorBoard()
const days = ref(365)
const reason = ref('手工开通')
const message = ref('')
const busyId = ref(0)

onMounted(load)

async function remove(row) {
  message.value = ''
  if (!window.confirm('删除 ' + row.email + ' ？账号、订单和授权会一起去掉，不能恢复。')) return
  busyId.value = row.id
  const data = await post('/api/vendor/customers/' + row.id + '/delete', {})
  busyId.value = 0
  message.value = data.ok ? '已删除 ' + row.email : (data.error || '删除失败')
  if (data.ok) await load()
}

async function extend(row) {
  message.value = ''
  const count = Number(days.value)
  if (!count || count < 1) {
    message.value = '天数要大于 0'
    return
  }
  busyId.value = row.id
  const data = await post('/api/vendor/grants', {
    email: row.email,
    days: count,
    reason: reason.value || '手工开通'
  })
  busyId.value = 0
  message.value = data.ok
    ? row.email + ' 可用至 ' + day(data.authEndTime)
    : (data.error || '增加失败')
  if (data.ok) await load()
}
</script>

<template>
  <p class="err" v-if="loadError">{{ loadError }}</p>
  <section v-if="board" class="card-yxy">
    <h1>客户</h1>
    <p class="lead">注册后的账号会出现在这里。支付未接通时，填写天数后点该行的「增加期限」。</p>
    <p v-if="message" :class="message.includes('可用至') ? 'ok' : 'err'">{{ message }}</p>
    <div class="row-form">
      <label>增加天数<input class="field-yxy" v-model="days" type="number" min="1" /></label>
      <label>事由<input class="field-yxy reason" v-model="reason" /></label>
    </div>
    <table>
      <thead>
        <tr><th>邮箱</th><th>客户号</th><th>归属</th><th>注册日</th><th>到期日</th><th>状态</th><th></th></tr>
      </thead>
      <tbody>
        <tr v-for="row in board.customers" :key="row.id">
          <td>{{ row.email }}</td>
          <td>{{ row.customerNo }}</td>
          <td>{{ row.directProviderId || '厂商' }}</td>
          <td>{{ day(row.createdAt) }}</td>
          <td>{{ day(row.authEndTime) }}</td>
          <td>{{ periodText(row.authEndTime) }}</td>
          <td>
            <button class="btn-yxy mini" type="button" :disabled="busyId === row.id" @click="extend(row)">增加期限</button>
            <button class="btn-yxy mini" type="button" :disabled="busyId === row.id" @click="remove(row)">删除</button>
          </td>
        </tr>
        <tr v-if="!board.customers.length"><td colspan="7">暂无客户。用户先在网站注册，再回到这里开通。</td></tr>
      </tbody>
    </table>
  </section>
</template>
