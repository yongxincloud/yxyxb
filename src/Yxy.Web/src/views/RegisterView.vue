<script setup>
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { get, post } from '../api.js'

const route = useRoute()
const router = useRouter()
const email = ref('')
const password = ref('')
const confirm = ref('')
const emailCode = ref('')
const captchaId = ref('')
const captchaAnswer = ref('')
const captchaSvg = ref('')
const error = ref('')

async function loadCaptcha() {
  const data = await get('/api/account/captcha')
  captchaId.value = data.id
  captchaSvg.value = data.svg
}

async function sendCode() {
  error.value = ''
  const data = await post('/api/account/send-code', {
    email: email.value,
    scene: 'register',
    captchaId: captchaId.value,
    captchaAnswer: captchaAnswer.value
  })
  if (!data.ok) error.value = data.error || '发送失败'
  if (data.devCode) emailCode.value = data.devCode
  await loadCaptcha()
}

async function submit() {
  error.value = ''
  const data = await post('/api/account/register', {
    email: email.value,
    password: password.value,
    confirm: confirm.value,
    emailCode: emailCode.value,
    providerNo: route.query.provider_no || null
  })
  if (!data.ok) {
    error.value = data.error || '注册失败'
    return
  }
  router.push('/login')
}

onMounted(loadCaptcha)
</script>

<template>
  <form class="card-yxy" @submit.prevent="submit">
    <h1>注册</h1>
    <input class="field-yxy" v-model="email" type="email" autocomplete="username" placeholder="邮箱" />
    <input class="field-yxy" v-model="password" type="password" autocomplete="new-password" placeholder="密码" />
    <input class="field-yxy" v-model="confirm" type="password" autocomplete="new-password" placeholder="确认密码" />
    <div v-html="captchaSvg"></div>
    <input class="field-yxy" v-model="captchaAnswer" placeholder="图形验证码" />
    <button class="btn-yxy" type="button" @click="sendCode">发送邮箱验证码</button>
    <input class="field-yxy" v-model="emailCode" placeholder="邮箱验证码" />
    <p class="err" v-if="error">{{ error }}</p>
    <button class="btn-yxy" type="submit">注册</button>
    <router-link to="/login">已有账号，去登录</router-link>
  </form>
</template>
