<script setup>
import { onMounted } from 'vue'
import { useVendorBoard } from '../vendorBoard.js'

const { board, loadError, load } = useVendorBoard()
onMounted(load)
</script>

<template>
  <p class="err" v-if="loadError">{{ loadError }}</p>
  <section v-if="board" class="card-yxy">
    <h1>概览</h1>
    <p class="lead">客户 {{ board.customers.length }}，订单 {{ board.orders.length }}，服务商 {{ board.providers.length }}，授权 {{ board.auths.length }}。</p>
    <p class="lead">支付未接通时，到「客户」页给已注册账号增加使用天数。</p>
  </section>
</template>
