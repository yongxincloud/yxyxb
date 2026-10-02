<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { post } from '../api.js'

const router = useRouter()
const email = ref('')
const password = ref('')
const codeEmail = ref('')
const code = ref('')
const error = ref('')

async function loginPassword() {
  error.value = ''
  const data = await post('/api/account/login', { email: email.value, password: password.value })
  if (!data.ok) {
    error.value = data.error || '登录失败'
    return
  }
  router.push('/home')
}

async function sendLoginCode() {
  error.value = ''
  const data = await post('/api/account/send-code', { email: codeEmail.value, scene: 'login' })
  if (!data.ok) error.value = data.error || '发送失败'
  if (data.devCode) code.value = data.devCode
}

async function loginCode() {
  error.value = ''
  const data = await post('/api/account/login-code', { email: codeEmail.value, code: code.value })
  if (!data.ok) {
    error.value = data.error || '登录失败'
    return
  }
  router.push('/home')
}
</script>

<template>
  <section class="card-yxy">
    <h1>登录</h1>
    <form @submit.prevent="loginPassword">
      <input class="field-yxy" v-model="email" type="email" autocomplete="username" placeholder="邮箱" />
      <input class="field-yxy" v-model="password" type="password" autocomplete="current-password" placeholder="密码" />
      <button class="btn-yxy" type="submit">密码登录</button>
    </form>
    <form @submit.prevent="loginCode">
      <input class="field-yxy" v-model="codeEmail" type="email" placeholder="邮箱" />
      <button class="btn-yxy" type="button" @click="sendLoginCode">发送邮箱验证码</button>
      <input class="field-yxy" v-model="code" placeholder="邮箱验证码" />
      <button class="btn-yxy" type="submit">验证码登录</button>
    </form>
    <p class="err" v-if="error">{{ error }}</p>
    <router-link to="/register">注册</router-link>
    <router-link to="/forgot">找回密码</router-link>
  </section>
</template>
