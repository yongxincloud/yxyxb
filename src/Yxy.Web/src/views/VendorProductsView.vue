<script setup>
import { onMounted, ref } from 'vue'
import { post } from '../api.js'
import { useVendorBoard } from '../vendorBoard.js'

const { board, loadError, load } = useVendorBoard()
const prices = ref({})
const message = ref('')

onMounted(load)

async function changePrice(productNo) {
  const data = await post('/api/vendor/products/' + productNo + '/price', { price: Number(prices.value[productNo]) })
  message.value = data.ok ? productNo + ' 价格已更新' : (data.error || '改价失败')
  if (data.ok) await load()
}
</script>

<template>
  <p class="err" v-if="loadError">{{ loadError }}</p>
  <section v-if="board" class="card-yxy">
    <h1>产品定价</h1>
    <p v-if="message" class="lead">{{ message }}</p>
    <div v-for="row in board.products" :key="row.productNo" class="product">
      <p>{{ row.name }}（{{ row.productNo }}）现价 {{ row.price }} 元 / {{ row.periodDays }} 天</p>
      <div class="row-form">
        <input class="field-yxy" v-model="prices[row.productNo]" placeholder="新价格" />
        <button class="btn-yxy mini" type="button" @click="changePrice(row.productNo)">修改价格</button>
      </div>
    </div>
    <p v-if="!board.products.length" class="lead">暂无产品</p>
  </section>
</template>
