<script setup>
import { ref } from 'vue'
import { post } from '../api.js'

const want = ref('no')
const titleName = ref('')
const taxNo = ref('')
const email = ref('')
const licenseFile = ref('')
const message = ref('')

async function submit() {
  message.value = ''
  if (want.value === 'no') {
    message.value = '已选择不开票。'
    return
  }
  const data = await post('/api/portal/invoice-apply', {
    titleName: titleName.value,
    taxNo: taxNo.value,
    email: email.value,
    licenseFile: licenseFile.value
  })
  message.value = data.ok ? '已提交，由厂商开具。' : (data.error || '提交失败')
}
</script>

<template>
  <form class="card-yxy" @submit.prevent="submit">
    <h1>开票</h1>
    <p>可以不开票。要开票由厂商开具，需提供公司抬头、税号、邮箱和 JPG 或 PNG 营业执照。</p>
    <label><input type="radio" value="no" v-model="want" /> 不开票</label>
    <label><input type="radio" value="yes" v-model="want" /> 开票</label>
    <template v-if="want === 'yes'">
      <input class="field-yxy" v-model="titleName" placeholder="公司抬头" />
      <input class="field-yxy" v-model="taxNo" placeholder="税号" />
      <input class="field-yxy" v-model="email" type="email" placeholder="邮箱" />
      <input class="field-yxy" v-model="licenseFile" placeholder="营业执照文件名，如 license.png" />
    </template>
    <p class="err" v-if="message">{{ message }}</p>
    <button class="btn-yxy" type="submit">确定</button>
    <router-link to="/home">返回</router-link>
  </form>
</template>
