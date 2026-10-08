<script setup>
import { onMounted } from 'vue'
import { day, payText, useVendorBoard } from '../vendorBoard.js'

const { board, loadError, load } = useVendorBoard()
onMounted(load)
</script>

<template>
  <p class="err" v-if="loadError">{{ loadError }}</p>
  <section v-if="board" class="card-yxy">
    <h1>订单</h1>
    <table>
      <thead><tr><th>单号</th><th>邮箱</th><th>类型</th><th>金额</th><th>状态</th><th>下单日</th></tr></thead>
      <tbody>
        <tr v-for="row in board.orders" :key="row.id">
          <td>{{ row.orderNo }}</td>
          <td>{{ row.email }}</td>
          <td>{{ row.orderType === 2 ? '试用' : '正式' }}</td>
          <td>{{ row.amount }}</td>
          <td>{{ payText(row.payStatus) }}</td>
          <td>{{ day(row.createdAt) }}</td>
        </tr>
        <tr v-if="!board.orders.length"><td colspan="6">暂无订单</td></tr>
      </tbody>
    </table>
  </section>
</template>
