<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { post } from '../api.js'

const router = useRouter()
const email = ref('')
const code = ref('')
const password = ref('')
const confirm = ref('')
const error = ref('')

async function sendCode() {
  error.value = ''
  const data = await post('/api/account/send-code', { email: email.value, scene: 'reset' })
  if (!data.ok) error.value = data.error || '发送失败'
}

async function submit() {
  error.value = ''
  const data = await post('/api/account/reset', {
    email: email.value,
    code: code.value,
    password: password.value,
    confirm: confirm.value
  })
  if (!data.ok) {
    error.value = data.error || '重置失败'
    return
  }
  router.push('/login')
}
</script>

<template>
  <form class="card-yxy" @submit.prevent="submit">
    <h1>找回密码</h1>
    <input class="field-yxy" v-model="email" type="email" placeholder="邮箱" />
    <button class="btn-yxy" type="button" @click="sendCode">发送邮箱验证码</button>
    <input class="field-yxy" v-model="code" placeholder="邮箱验证码" />
    <input class="field-yxy" v-model="password" type="password" autocomplete="new-password" placeholder="新密码" />
    <input class="field-yxy" v-model="confirm" type="password" autocomplete="new-password" placeholder="确认新密码" />
    <p class="err" v-if="error">{{ error }}</p>
    <button class="btn-yxy" type="submit">重置密码</button>
    <router-link to="/login">返回登录</router-link>
  </form>
</template>
