<script setup>
import { onMounted } from 'vue'
import { day, useVendorBoard } from '../vendorBoard.js'

const { board, loadError, load } = useVendorBoard()
onMounted(load)
</script>

<template>
  <p class="err" v-if="loadError">{{ loadError }}</p>
  <section v-if="board" class="card-yxy">
    <h1>操作日志</h1>
    <table>
      <thead><tr><th>时间</th><th>模块</th><th>动作</th><th>变更前</th><th>变更后</th></tr></thead>
      <tbody>
        <tr v-for="(row, index) in board.logs" :key="index">
          <td>{{ day(row.createdAt) }}</td>
          <td>{{ row.module }}</td>
          <td>{{ row.action }}</td>
          <td>{{ row.beforeValue || '—' }}</td>
          <td>{{ row.afterValue || '—' }}</td>
        </tr>
        <tr v-if="!board.logs.length"><td colspan="5">暂无操作</td></tr>
      </tbody>
    </table>
  </section>
</template>
