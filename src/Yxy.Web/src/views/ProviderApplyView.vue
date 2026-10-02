<script setup>
import { ref } from 'vue'
import { post } from '../api.js'

const form = ref({
  companyName: '',
  taxNo: '',
  licenseFile: '',
  contactName: '',
  contactEmail: '',
  contactPhone: ''
})
const message = ref('')

async function submit() {
  message.value = ''
  const data = await post('/api/portal/provider-apply', form.value)
  message.value = data.ok ? '已提交，等待厂商审批。' : (data.error || '提交失败')
}
</script>

<template>
  <form class="card-yxy" @submit.prevent="submit">
    <h1>申请成为服务商</h1>
    <input class="field-yxy" v-model="form.companyName" placeholder="公司名称" />
    <input class="field-yxy" v-model="form.taxNo" placeholder="税号" />
    <input class="field-yxy" v-model="form.licenseFile" placeholder="营业执照文件名，如 license.jpg" />
    <input class="field-yxy" v-model="form.contactName" placeholder="联系人" />
    <input class="field-yxy" v-model="form.contactEmail" type="email" placeholder="邮箱" />
    <input class="field-yxy" v-model="form.contactPhone" placeholder="手机号" />
    <p class="err" v-if="message">{{ message }}</p>
    <button class="btn-yxy" type="submit">提交申请</button>
    <router-link to="/home">返回</router-link>
  </form>
</template>
