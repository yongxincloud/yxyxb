<script setup>
import { onMounted } from 'vue'
import { authText, day, useVendorBoard } from '../vendorBoard.js'

const { board, loadError, load } = useVendorBoard()
onMounted(load)
</script>

<template>
  <p class="err" v-if="loadError">{{ loadError }}</p>
  <section v-if="board" class="card-yxy">
    <h1>授权记录</h1>
    <p class="lead">这里只查看已发放的授权。给客户加天数请到「客户」页。</p>
    <table>
      <thead><tr><th>邮箱</th><th>到期日</th><th>状态</th><th>来源</th><th>事由</th></tr></thead>
      <tbody>
        <tr v-for="row in board.auths" :key="row.id">
          <td>{{ row.email }}</td>
          <td>{{ day(row.authEndTime) }}</td>
          <td>{{ authText(row.status) }}</td>
          <td>{{ row.grantType === 2 ? '后台发放' : '订单' }}</td>
          <td>{{ row.grantReason || '—' }}</td>
        </tr>
        <tr v-if="!board.auths.length"><td colspan="5">暂无授权</td></tr>
      </tbody>
    </table>
  </section>
</template>
