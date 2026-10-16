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
  const data = await post('/api/vendor/send-code', { email: email.value, scene: 'reset' })
  if (!data.ok) error.value = data.error || '发送失败'
  if (data.devCode) code.value = data.devCode
}

async function submit() {
  error.value = ''
  const data = await post('/api/vendor/reset', {
    email: email.value,
    code: code.value,
    password: password.value,
    confirm: confirm.value
  })
  if (!data.ok) {
    error.value = data.error || '重置失败'
    return
  }
  router.push('/vendor/login')
}
</script>

<template>
  <section class="wrap">
    <form class="card-yxy panel" @submit.prevent="submit">
      <h1>厂商找回密码</h1>
      <p class="lead">使用管理员绑定的邮箱接收验证码，重置后请用原用户名登录。</p>
      <input class="field-yxy" v-model="email" type="email" placeholder="找回邮箱" />
      <button class="btn-yxy" type="button" @click="sendCode">发送邮箱验证码</button>
      <input class="field-yxy" v-model="code" placeholder="邮箱验证码" />
      <input class="field-yxy" v-model="password" type="password" autocomplete="new-password" placeholder="新密码" />
      <input class="field-yxy" v-model="confirm" type="password" autocomplete="new-password" placeholder="确认新密码" />
      <p class="err" v-if="error">{{ error }}</p>
      <button class="btn-yxy" type="submit">重置密码</button>
      <router-link to="/vendor/login">返回厂商登录</router-link>
    </form>
  </section>
</template>

<style scoped>
.wrap { display: grid; place-items: center; min-height: 480px; }
.panel { width: min(420px, 100%); gap: 12px; }
.panel h1 { margin: 0; font-size: 22px; color: var(--c-title); }
.lead { margin: 0; color: var(--c-text-muted); line-height: 1.6; }
</style>
