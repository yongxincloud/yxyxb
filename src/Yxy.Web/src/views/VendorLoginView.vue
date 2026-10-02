<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { post } from '../api.js'

const router = useRouter()
const username = ref('admin')
const password = ref('')
const error = ref('')

async function submit() {
  error.value = ''
  const data = await post('/api/vendor/login', { username: username.value, password: password.value })
  if (!data.ok) {
    error.value = data.error || '登录失败'
    return
  }
  router.push('/vendor')
}
</script>

<template>
  <section class="login">
    <aside>
      <p class="brand">用心云备份</p>
      <h1>厂商后台</h1>
      <p>登录后可查看全部客户、订单、授权、服务商和结算。</p>
    </aside>
    <form class="panel" @submit.prevent="submit">
      <h2>管理员登录</h2>
      <label>
        账号
        <input class="field-yxy" v-model="username" autocomplete="username" placeholder="admin" />
      </label>
      <label>
        密码
        <input class="field-yxy" v-model="password" type="password" autocomplete="current-password" placeholder="请输入密码" />
      </label>
      <p class="err" v-if="error">{{ error }}</p>
      <button class="btn-yxy submit" type="submit">登录</button>
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
aside h1 { margin: 0 0 12px; font-size: 26px; font-weight: 600; }
aside p { margin: 0; max-width: 360px; line-height: 1.7; color: rgba(255,255,255,.86); }
.panel { padding: 48px 40px; display: grid; align-content: center; gap: 14px; }
.panel h2 { margin: 0; font-size: 22px; color: var(--c-title); }
label { display: grid; gap: 6px; color: var(--c-title); font-size: 13px; }
.submit { width: 100%; cursor: pointer; }
@media (max-width: 768px) {
  .login { grid-template-columns: 1fr; }
  aside, .panel { padding: 28px 20px; }
}
</style>
