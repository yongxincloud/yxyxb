<script setup>
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { post } from '../api.js'
import { providerText, useVendorBoard } from '../vendorBoard.js'

const { board, loadError, load } = useVendorBoard()
const levelName = ref('')
const levelRate = ref('0.6')
const levelId = ref('')
const message = ref('')
const messageBad = ref(false)
const busyId = ref(0)
const levelDraft = reactive({})

const activeLevels = computed(() =>
  (board.value?.levels || []).filter((row) => row.status === 1)
)

onMounted(load)

watch(() => board.value?.providers, (list) => {
  for (const row of list || []) levelDraft[row.id] = row.levelId
})

function levelNameOf(id) {
  const row = board.value?.levels?.find(x => x.id === id)
  return row ? row.name : id
}

function levelOptions(row) {
  if (activeLevels.value.some((x) => x.id === row.levelId)) return activeLevels.value
  const current = (board.value?.levels || []).find((x) => x.id === row.levelId)
  return current ? [current, ...activeLevels.value] : activeLevels.value
}

function say(text, bad) {
  message.value = text
  messageBad.value = bad
}

async function createLevel() {
  const data = await post('/api/vendor/levels', { name: levelName.value, rate: Number(levelRate.value) })
  say(data.ok ? '等级已建立' : (data.error || '失败'), !data.ok)
  if (data.ok) {
    levelName.value = ''
    await load()
  }
}

async function approveApply(id) {
  const data = await post('/api/vendor/provider-applies/' + id + '/approve', { levelId: Number(levelId.value) })
  say(data.ok ? '已通过 ' + data.providerNo : (data.error || '审批失败'), !data.ok)
  if (data.ok) await load()
}

async function setStatus(row, status) {
  if (status === 0 && !window.confirm('停用 ' + row.name + ' ？停用期间新付款不计佣金，客户归属不变。')) return
  busyId.value = row.id
  const data = await post('/api/vendor/providers/' + row.id + '/status', { status })
  busyId.value = 0
  say(data.ok ? (status === 1 ? '已启用 ' : '已停用 ') + row.name : (data.error || '操作失败'), !data.ok)
  if (data.ok) await load()
}

async function saveLevel(row) {
  const nextId = Number(levelDraft[row.id])
  if (!nextId) {
    say('请选择等级', true)
    return
  }
  if (nextId === row.levelId) {
    say('等级未变化', true)
    return
  }
  busyId.value = row.id
  const data = await post('/api/vendor/providers/' + row.id + '/level', { levelId: nextId })
  busyId.value = 0
  say(data.ok ? row.name + ' 等级已更新' : (data.error || '修改失败'), !data.ok)
  if (data.ok) await load()
}
</script>

<template>
  <p class="err" v-if="loadError">{{ loadError }}</p>
  <section v-if="board" class="card-yxy">
    <h1>服务商</h1>
    <p class="lead">已通过的服务商可以启用、停用，并修改等级。已解约的不能再改。</p>
    <p v-if="message" :class="messageBad ? 'err' : 'lead'">{{ message }}</p>
    <table>
      <thead><tr><th>编号</th><th>名称</th><th>邮箱</th><th>等级</th><th>状态</th></tr></thead>
      <tbody>
        <tr v-for="row in board.providers" :key="row.id">
          <td>{{ row.providerNo }}</td>
          <td>{{ row.name }}</td>
          <td>{{ row.email || '—' }}</td>
          <td>
            <span v-if="row.status === 2">{{ levelNameOf(row.levelId) }}</span>
            <div v-else class="ops">
              <select class="field-yxy" v-model="levelDraft[row.id]">
                <option v-for="lv in levelOptions(row)" :key="lv.id" :value="lv.id">{{ lv.name }}</option>
              </select>
              <button class="btn-yxy mini" type="button" :disabled="busyId === row.id" @click="saveLevel(row)">保存</button>
            </div>
          </td>
          <td>
            {{ providerText(row.status) }}
            <button v-if="row.status === 1" class="btn-yxy mini" type="button" :disabled="busyId === row.id" @click="setStatus(row, 0)">停用</button>
            <button v-if="row.status === 0" class="btn-yxy mini" type="button" :disabled="busyId === row.id" @click="setStatus(row, 1)">启用</button>
          </td>
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

<style scoped>
.ops { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
.ops .field-yxy { width: 140px; }
td .mini { margin-left: 8px; }
</style>
