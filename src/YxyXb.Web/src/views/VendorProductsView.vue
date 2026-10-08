<script setup>
import { onMounted, reactive, ref, watch } from 'vue'
import { post } from '../api.js'
import { useVendorBoard } from '../vendorBoard.js'

const { board, loadError, load } = useVendorBoard()
const forms = reactive({})
const message = ref('')

onMounted(load)

watch(
  () => board.value?.products,
  (rows) => {
    if (!rows) return
    for (const row of rows) {
      forms[row.productNo] = {
        price: row.price,
        promoPrice: row.promoPrice ?? '',
        promoStart: toLocal(row.promoStart),
        promoEnd: toLocal(row.promoEnd),
        maxDevices: row.maxDevices
      }
    }
  },
  { immediate: true }
)

function toLocal(value) {
  if (!value) return ''
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return ''
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

async function save(productNo) {
  const form = forms[productNo]
  const body = {
    price: Number(form.price),
    maxDevices: Number(form.maxDevices)
  }
  if (form.promoPrice === '' || form.promoPrice === null) {
    body.promoPrice = 0
  } else {
    body.promoPrice = Number(form.promoPrice)
    body.promoStart = form.promoStart ? new Date(form.promoStart).toISOString() : null
    body.promoEnd = form.promoEnd ? new Date(form.promoEnd).toISOString() : null
  }
  const data = await post('/api/vendor/products/' + productNo, body)
  message.value = data.ok ? productNo + ' 已保存' : (data.error || '保存失败')
  if (data.ok) await load()
}
</script>

<template>
  <p class="err" v-if="loadError">{{ loadError }}</p>
  <section v-if="board" class="card-yxy">
    <h1>产品定价</h1>
    <p v-if="message" class="lead">{{ message }}</p>
    <div v-for="row in board.products" :key="row.productNo" class="product">
      <p>
        {{ row.name }}（{{ row.productNo }}）原价 {{ row.price }} 元 /
        {{ row.periodDays }} 天 · 最多 {{ row.maxDevices }} 台
      </p>
      <div class="row-form" v-if="forms[row.productNo]">
        <input class="field-yxy" v-model="forms[row.productNo].price" placeholder="原价" />
        <input class="field-yxy" v-model="forms[row.productNo].maxDevices" placeholder="设备上限" />
        <input class="field-yxy" v-model="forms[row.productNo].promoPrice" placeholder="促销价（空=清除）" />
        <input class="field-yxy" type="datetime-local" v-model="forms[row.productNo].promoStart" />
        <input class="field-yxy" type="datetime-local" v-model="forms[row.productNo].promoEnd" />
        <button class="btn-yxy mini" type="button" @click="save(row.productNo)">保存</button>
      </div>
    </div>
    <p v-if="!board.products.length" class="lead">暂无产品</p>
  </section>
</template>
