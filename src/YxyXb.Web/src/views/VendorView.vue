<script setup>
import { onMounted, ref } from 'vue'
import { get, post } from '../api.js'
import { useVendorBoard } from '../vendorBoard.js'

const { board, loadError, load } = useVendorBoard()
const email = ref('')
const message = ref('')
const error = ref('')

onMounted(async () => {
  await load()
  const me = await get('/api/vendor/me')
  if (me.ok) email.value = me.email || ''
})

async function saveEmail() {
  message.value = ''
  error.value = ''
  const data = await post('/api/vendor/profile', { email: email.value })
  if (!data.ok) {
    error.value = data.error || '保存失败'
    return
  }
  email.value = data.email || email.value
  message.value = '找回邮箱已保存，忘记密码时可用来收验证码。'
}
</script>

<template>
  <p class="err" v-if="loadError">{{ loadError }}</p>
  <section v-if="board" class="card-yxy">
    <h1>概览</h1>
    <p class="lead">客户 {{ board.customers.length }}，订单 {{ board.orders.length }}，服务商 {{ board.providers.length }}，授权 {{ board.auths.length }}。</p>
    <p class="lead">支付未接通时，到「客户」页给已注册账号增加使用天数。</p>
  </section>
  <section class="card-yxy">
    <h2>找回密码邮箱</h2>
    <p class="lead">绑定后可在登录页用邮箱验证码重置密码。也可在 appsettings 的 Admin:Email 预置。</p>
    <div class="row">
      <input class="field-yxy" v-model="email" type="email" placeholder="管理员邮箱" />
      <button class="btn-yxy" type="button" @click="saveEmail">保存</button>
    </div>
    <p class="ok" v-if="message">{{ message }}</p>
    <p class="err" v-if="error">{{ error }}</p>
  </section>
</template>

<style scoped>
.row { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
.row .field-yxy { flex: 1; min-width: 220px; }
</style>
