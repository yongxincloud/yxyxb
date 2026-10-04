<script setup>
import { onMounted, ref } from 'vue'
import { post } from '../api.js'
import { settleText, useVendorBoard } from '../vendorBoard.js'

const { board, loadError, load } = useVendorBoard()
const message = ref('')

onMounted(load)

async function approveSettlement(id) {
  const data = await post('/api/vendor/settlements/' + id + '/approve', {})
  message.value = data.ok ? '结算已审批' : (data.error || '审批失败')
  if (data.ok) await load()
}

async function issueInvoice(id) {
  const data = await post('/api/vendor/invoice-applies/' + id + '/issue', {})
  message.value = data.ok ? '已开票' : (data.error || '开票失败')
  if (data.ok) await load()
}
</script>

<template>
  <p class="err" v-if="loadError">{{ loadError }}</p>
  <section v-if="board" class="card-yxy">
    <h1>佣金结算</h1>
    <p v-if="message" class="lead">{{ message }}</p>
    <h2>佣金</h2>
    <table>
      <thead><tr><th>编号</th><th>服务商</th><th>方向</th><th>金额</th><th>结算</th></tr></thead>
      <tbody>
        <tr v-for="row in board.commissions" :key="row.id">
          <td>{{ row.id }}</td>
          <td>{{ row.providerId }}</td>
          <td>{{ row.entryType === 2 ? '退款' : '销售' }}</td>
          <td>{{ row.amount }}</td>
          <td>{{ row.settled ? '已结算' : '未结算' }}</td>
        </tr>
        <tr v-if="!board.commissions.length"><td colspan="5">暂无佣金</td></tr>
      </tbody>
    </table>

    <h2>结算单</h2>
    <p v-for="row in board.settlements" :key="'s' + row.id">
      {{ row.settlementNo }} {{ row.amount }} 元 {{ settleText(row.status) }}
      <button v-if="row.status === 1" class="btn-yxy mini" type="button" @click="approveSettlement(row.id)">审批</button>
    </p>
    <p v-if="!board.settlements.length" class="lead">暂无结算单</p>

    <h2>客户开票</h2>
    <p v-for="row in board.invoices" :key="'i' + row.id">
      {{ row.applyNo }} {{ row.titleName }} {{ row.amount }} 元
      <button v-if="row.status === 1" class="btn-yxy mini" type="button" @click="issueInvoice(row.id)">开票</button>
    </p>
    <p v-if="!board.invoices.length" class="lead">暂无开票申请</p>
  </section>
</template>
