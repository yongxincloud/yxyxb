<script setup>
import { onMounted, ref } from 'vue'
import { get, post } from '../api.js'

const email = ref('')
const name = ref('')
const nickname = ref('')
const companyName = ref('')
const phone = ref('')
const invoiceTitle = ref('')
const invoiceTaxNo = ref('')
const invoiceEmail = ref('')
const invoiceLicense = ref('')
const message = ref('')

function onLicense(event) {
  const file = event.target.files && event.target.files[0]
  invoiceLicense.value = file ? file.name : ''
}

async function load() {
  const data = await get('/api/account/profile')
  if (!data.ok) {
    message.value = '请先登录'
    return
  }
  email.value = data.email || ''
  name.value = data.name || ''
  nickname.value = data.nickname || ''
  companyName.value = data.companyName || ''
  phone.value = data.phone || ''
  invoiceTitle.value = data.invoiceTitle || ''
  invoiceTaxNo.value = data.invoiceTaxNo || ''
  invoiceEmail.value = data.invoiceEmail || ''
  invoiceLicense.value = data.invoiceLicense || ''
}

async function save() {
  message.value = ''
  const data = await post('/api/account/profile', {
    name: name.value,
    nickname: nickname.value,
    companyName: companyName.value,
    phone: phone.value,
    invoiceTitle: invoiceTitle.value,
    invoiceTaxNo: invoiceTaxNo.value,
    invoiceEmail: invoiceEmail.value,
    invoiceLicense: invoiceLicense.value
  })
  message.value = data.ok ? '已保存' : (data.error || '保存失败')
}

onMounted(load)
</script>

<template>
  <form class="card-yxy" @submit.prevent="save">
    <h1>账号信息</h1>
    <p class="lead">邮箱 {{ email }}。找回密码仍从这里进入。</p>
    <router-link to="/forgot">找回密码</router-link>
    <h2>个人信息</h2>
    <input class="field-yxy" v-model="name" placeholder="姓名" />
    <input class="field-yxy" v-model="nickname" placeholder="昵称" />
    <input class="field-yxy" v-model="companyName" placeholder="公司名称" />
    <input class="field-yxy" v-model="phone" placeholder="手机号" />
    <h2>开票信息</h2>
    <input class="field-yxy" v-model="invoiceTitle" placeholder="公司抬头" />
    <input class="field-yxy" v-model="invoiceTaxNo" placeholder="税号" />
    <input class="field-yxy" v-model="invoiceEmail" type="email" placeholder="收票邮箱" />
    <label class="file">营业执照（JPG 或 PNG）
      <input type="file" accept="image/jpeg,image/png,.jpg,.jpeg,.png" @change="onLicense" />
    </label>
    <p v-if="invoiceLicense">已选择 {{ invoiceLicense }}</p>
    <p class="err" v-if="message">{{ message }}</p>
    <button class="btn-yxy" type="submit">保存</button>
  </form>
</template>

<style scoped>
.lead { margin: 0 0 8px; }
h2 { margin: 16px 0 8px; font-size: 16px; color: var(--c-title); }
.file { display: grid; gap: 6px; }
form { display: grid; gap: 10px; }
</style>
