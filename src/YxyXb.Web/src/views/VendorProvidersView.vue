<script setup>
import { onMounted, ref } from 'vue'
import { post } from '../api.js'
import { providerText, useVendorBoard } from '../vendorBoard.js'

const { board, loadError, load } = useVendorBoard()
const levelName = ref('')
const levelRate = ref('0.6')
const levelId = ref('')
const message = ref('')

onMounted(load)

function levelNameOf(id) {
  const row = board.value?.levels?.find(x => x.id === id)
  return row ? row.name : id
}

async function createLevel() {
  const data = await post('/api/vendor/levels', { name: levelName.value, rate: Number(levelRate.value) })
  message.value = data.ok ? '等级已建立' : (data.error || '失败')
  if (data.ok) {
    levelName.value = ''
    await load()
  }
}

async function approveApply(id) {
  const data = await post('/api/vendor/provider-applies/' + id + '/approve', { levelId: Number(levelId.value) })
  message.value = data.ok ? '已通过 ' + data.providerNo : (data.error || '审批失败')
  if (data.ok) await load()
}
</script>

<template>
  <p class="err" v-if="loadError">{{ loadError }}</p>
  <section v-if="board" class="card-yxy">
    <h1>服务商</h1>
    <p v-if="message" class="lead">{{ message }}</p>
    <table>
      <thead><tr><th>编号</th><th>名称</th><th>邮箱</th><th>等级</th><th>状态</th></tr></thead>
      <tbody>
        <tr v-for="row in board.providers" :key="row.id">
          <td>{{ row.providerNo }}</td>
          <td>{{ row.name }}</td>
          <td>{{ row.email || '—' }}</td>
          <td>{{ levelNameOf(row.levelId) }}</td>
          <td>{{ providerText(row.status) }}</td>
        </tr>
        <tr v-if="!board.providers.length"><td colspan="5">暂无服务商</td></tr>
      </tbody>
    </table>

    <h2>等级</h2>
    <p v-for="row in board.levels" :key="row.id">{{ row.name }}，佣金比例 {{ row.commissionRate }}</p>
    <p v-if="!board.levels.length" class="lead">暂无等级</p>
    <div class="row-form">
      <input class="field-yxy reason" v-model="levelName" placeholder="新等级名称" />
      <input class="field-yxy" v-model="levelRate" placeholder="佣金比例，如 0.6" />
      <button class="btn-yxy mini" type="button" @click="createLevel">新增等级</button>
    </div>

    <h2>入驻申请</h2>
    <div class="row-form">
      <select class="field-yxy reason" v-model="levelId">
        <option value="">选择审批等级</option>
        <option v-for="row in board.levels" :key="row.id" :value="row.id">{{ row.name }}</option>
      </select>
    </div>
    <p v-for="row in board.applies" :key="row.id">
      {{ row.companyName }} {{ row.taxNo }} {{ row.contactEmail }}
      <button class="btn-yxy mini" type="button" @click="approveApply(row.id)">通过</button>
    </p>
    <p v-if="!board.applies.length" class="lead">暂无待审批申请</p>
  </section>
</template>
