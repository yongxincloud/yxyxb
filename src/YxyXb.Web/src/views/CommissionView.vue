<script setup>
import { onMounted, ref } from 'vue'
import QRCode from 'qrcode'
import { get, post } from '../api.js'
import { toAbsolutePromoUrl } from '../promoLink.js'

const rows = ref([])
const promo = ref('')
const qrSrc = ref('')
const copyMsg = ref('')
const promoBox = ref(null)
const error = ref('')
const invoiceFile = ref('')
const message = ref('')

onMounted(async () => {
  const code = await get('/api/portal/promo')
  if (code.ok) {
    promo.value = toAbsolutePromoUrl(code.url, window.location.origin)
    qrSrc.value = await QRCode.toDataURL(promo.value, {
      width: 168,
      margin: 1,
      color: { dark: '#1C1214', light: '#FFFFFF' }
    })
  }
  const data = await get('/api/portal/commissions')
  if (!data.ok) {
    error.value = data.error || '还不是服务商'
    return
  }
  rows.value = data.rows || []
})

async function copyPromo() {
  copyMsg.value = ''
  if (!promo.value) return
  try {
    await navigator.clipboard.writeText(promo.value)
    copyMsg.value = '已复制'
    return
  } catch {
    const box = promoBox.value
    if (box) {
      box.focus()
      box.select()
      document.execCommand('copy')
      copyMsg.value = '已复制'
    }
  }
}

async function settle() {
  message.value = ''
  const data = await post('/api/portal/settlements', { invoiceFile: invoiceFile.value })
  message.value = data.ok ? '已提交结算 ' + data.amount + ' 元' : (data.error || '结算失败')
}
</script>

<template>
  <section class="card-yxy">
    <h1>我的佣金</h1>
    <div v-if="promo" class="promo">
      <p class="label">推广链接</p>
      <div class="row">
        <input
          ref="promoBox"
          class="field-yxy"
          type="text"
          readonly
          :value="promo"
          @focus="$event.target.select()"
        />
        <button class="btn-yxy" type="button" @click="copyPromo">复制</button>
      </div>
      <p v-if="copyMsg" class="copied">{{ copyMsg }}</p>
      <img v-if="qrSrc" class="qr" :src="qrSrc" alt="推广链接二维码" />
    </div>
    <p class="err" v-if="error">{{ error }}</p>
    <p v-for="row in rows" :key="row.id">订单 {{ row.orderId }} 佣金 {{ row.amount }} 元</p>
    <input class="field-yxy" v-model="invoiceFile" placeholder="佣金发票文件名，如 invoice.pdf" />
    <button class="btn-yxy" type="button" @click="settle">发起结算</button>
    <p v-if="message">{{ message }}</p>
    <router-link to="/home">返回</router-link>
  </section>
</template>

<style scoped>
.promo { display: grid; gap: 8px; }
.label { margin: 0; color: var(--c-title); font-weight: 600; }
.row { display: grid; grid-template-columns: 1fr auto; gap: 8px; align-items: center; }
.copied { margin: 0; color: var(--brand-green); }
.qr { width: 168px; height: 168px; border: 1px solid var(--c-border); border-radius: var(--radius-input); background: #fff; }
</style>
