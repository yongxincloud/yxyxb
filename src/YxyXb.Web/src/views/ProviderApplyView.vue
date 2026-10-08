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

function onLicense(event) {
  const file = event.target.files && event.target.files[0]
  form.value.licenseFile = file ? file.name : ''
}

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
    <label class="file">营业执照（JPG 或 PNG）
      <input type="file" accept="image/jpeg,image/png,.jpg,.jpeg,.png" @change="onLicense" />
    </label>
    <p v-if="form.licenseFile">已选择 {{ form.licenseFile }}</p>
    <input class="field-yxy" v-model="form.contactName" placeholder="联系人" />
    <input class="field-yxy" v-model="form.contactEmail" type="email" placeholder="邮箱" />
    <input class="field-yxy" v-model="form.contactPhone" placeholder="手机号" />
    <p class="err" v-if="message">{{ message }}</p>
    <button class="btn-yxy" type="submit">提交申请</button>
    <router-link to="/home">返回</router-link>
  </form>
</template>
