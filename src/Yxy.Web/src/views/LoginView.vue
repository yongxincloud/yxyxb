<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { post } from '../api.js'

const router = useRouter()
const mode = ref('password')
const email = ref('')
const password = ref('')
const code = ref('')
const error = ref('')
const hint = ref('')

async function loginPassword() {
  error.value = ''
  hint.value = ''
  const data = await post('/api/account/login', { email: email.value, password: password.value })
  if (!data.ok) {
    error.value = data.error || '登录失败'
    return
  }
  router.push('/home')
}

async function sendLoginCode() {
  error.value = ''
  hint.value = ''
  const data = await post('/api/account/send-code', { email: email.value, scene: 'login' })
  if (!data.ok) {
    error.value = data.error || '发送失败'
    return
  }
  if (data.devCode) {
    code.value = data.devCode
    hint.value = '开发环境已填入验证码'
  } else {
    hint.value = '验证码已发送，5 分钟内有效'
  }
}

async function loginCode() {
  error.value = ''
  const data = await post('/api/account/login-code', { email: email.value, code: code.value })
  if (!data.ok) {
    error.value = data.error || '登录失败'
    return
  }
  router.push('/home')
}

function submit() {
  if (mode.value === 'password') loginPassword()
  else loginCode()
}
</script>

<template>
  <section class="login">
    <aside>
      <p class="brand">用心云备份</p>
      <h1>登录后管理订阅和订单</h1>
      <p>请使用注册邮箱。一个账号不限台数。</p>
    </aside>
    <form class="panel" @submit.prevent="submit">
      <h2>登录</h2>
      <div class="tabs">
        <button type="button" :class="{ on: mode === 'password' }" @click="mode = 'password'">密码登录</button>
        <button type="button" :class="{ on: mode === 'code' }" @click="mode = 'code'">验证码登录</button>
      </div>
      <label>
        邮箱
        <input class="field-yxy" v-model="email" type="email" autocomplete="username" placeholder="name@example.com" />
      </label>
      <label v-if="mode === 'password'">
        密码
        <input class="field-yxy" v-model="password" type="password" autocomplete="current-password" placeholder="请输入密码" />
      </label>
      <label v-else>
        邮箱验证码
        <span class="code-row">
          <input class="field-yxy" v-model="code" inputmode="numeric" autocomplete="one-time-code" placeholder="6 位数字" />
          <button class="btn-ghost" type="button" @click="sendLoginCode">获取验证码</button>
        </span>
      </label>
      <p class="hint" v-if="hint">{{ hint }}</p>
      <p class="err" v-if="error">{{ error }}</p>
      <button class="btn-yxy submit" type="submit">{{ mode === 'password' ? '登录' : '验证码登录' }}</button>
      <p class="links">
        <router-link to="/register">注册账号</router-link>
        <router-link to="/forgot">找回密码</router-link>
      </p>
    </form>
  </section>
</template>

<style scoped>
.login {
  display: grid;
  grid-template-columns: 1.1fr 1fr;
  min-height: 520px;
  background: #fff;
  border-radius: 8px;
  overflow: hidden;
}
aside {
  background: linear-gradient(160deg, var(--c-hero-from), var(--c-hero-to));
  color: #fff;
  padding: 48px 40px;
}
.brand { margin: 0 0 28px; font-size: 16px; letter-spacing: 1px; }
aside h1 { margin: 0 0 12px; font-size: 26px; font-weight: 600; line-height: 1.4; }
aside p { margin: 0; max-width: 360px; line-height: 1.7; color: rgba(255,255,255,.86); }
.panel { padding: 48px 40px; display: grid; align-content: center; gap: 14px; }
.panel h2 { margin: 0; font-size: 22px; color: var(--c-title); }
.tabs { display: flex; gap: 8px; }
.tabs button {
  height: 36px;
  padding: 0 16px;
  border-radius: 18px;
  border: 1px solid var(--c-border);
  background: #fff;
  color: var(--c-text);
  cursor: pointer;
}
.tabs button.on { background: var(--brand-primary); border-color: var(--brand-primary); color: #fff; }
label { display: grid; gap: 6px; color: var(--c-title); font-size: 13px; }
.code-row { display: grid; grid-template-columns: 1fr auto; gap: 8px; }
.btn-ghost {
  height: 40px;
  padding: 0 14px;
  border-radius: var(--radius-input);
  border: 1px solid var(--c-border);
  background: #fff;
  color: var(--brand-primary);
  cursor: pointer;
}
.hint { margin: 0; color: var(--c-text-muted); }
.submit { width: 100%; cursor: pointer; }
.links { display: flex; justify-content: space-between; margin: 0; }
.links a { color: var(--brand-primary); text-decoration: none; }
@media (max-width: 768px) {
  .login { grid-template-columns: 1fr; }
  aside, .panel { padding: 28px 20px; }
  aside h1 { font-size: 22px; }
}
</style>
