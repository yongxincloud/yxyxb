<script setup>
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { get, post } from '../api.js'
import { PRODUCT_DISPLAY_NAME } from '../brand.js'

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
  <section class="login">
    <aside>
      <div class="plate"><img class="logo" src="/logo.jpg" :alt="PRODUCT_DISPLAY_NAME" /></div>
      <h1>申请账号，开始本机离线备份</h1>
      <p>{{ PRODUCT_DISPLAY_NAME }}把文件和 SQL Server 打成 zip，再拷到已登记的移动硬盘并离线。个人版 / 标准版 / 专业版按可同时登录设备数区分，登录后可在门户选购或续费。</p>
      <ul>
        <li>本机加密打包，日志留在电脑上</li>
        <li>离线介质到点拷贝并按级别离线</li>
        <li>授权有效时可再上传云、共享目录或 FTP</li>
      </ul>
    </aside>
    <form class="panel" @submit.prevent="submit">
      <h2>申请账号</h2>
      <label>
        邮箱
        <input class="field-yxy" v-model="email" type="email" autocomplete="username" placeholder="name@example.com" />
      </label>
      <label>
        密码
        <input class="field-yxy" v-model="password" type="password" autocomplete="new-password" placeholder="请设置密码" />
      </label>
      <label>
        确认密码
        <input class="field-yxy" v-model="confirm" type="password" autocomplete="new-password" placeholder="再次输入密码" />
      </label>
      <label>
        图形验证码
        <span class="captcha-row">
          <input class="field-yxy" v-model="captchaAnswer" placeholder="请输入图中字符" />
          <button class="captcha" type="button" title="点击刷新" @click="loadCaptcha" v-html="captchaSvg"></button>
        </span>
      </label>
      <label>
        邮箱验证码
        <span class="code-row">
          <input class="field-yxy" v-model="emailCode" inputmode="numeric" autocomplete="one-time-code" placeholder="6 位数字" />
          <button class="btn-ghost" type="button" @click="sendCode">发送验证码</button>
        </span>
      </label>
      <p class="err" v-if="error">{{ error }}</p>
      <button class="btn-yxy submit" type="submit">注册</button>
      <p class="links">
        <router-link to="/login">已有账号，去登录</router-link>
      </p>
    </form>
  </section>
</template>

<style scoped>
.login {
  display: grid;
  grid-template-columns: 1.1fr 1fr;
  min-height: 560px;
  background: #fff;
  border-radius: 8px;
  overflow: hidden;
}
aside {
  background: linear-gradient(160deg, var(--c-hero-from), var(--c-hero-to));
  color: #fff;
  padding: 48px 40px;
}
.plate { display: inline-block; background: #fff; border-radius: 12px; padding: 6px 8px; margin-bottom: 22px; }
.logo { height: 64px; width: auto; display: block; }
aside h1 { margin: 0 0 12px; font-size: 26px; font-weight: 600; line-height: 1.4; }
aside p { margin: 0 0 16px; max-width: 360px; line-height: 1.7; color: rgba(255,255,255,.86); }
aside ul { margin: 0; padding-left: 18px; max-width: 360px; line-height: 1.8; color: rgba(255,255,255,.9); }
.panel { padding: 40px; display: grid; align-content: center; gap: 14px; }
.panel h2 { margin: 0; font-size: 22px; color: var(--c-title); }
label { display: grid; gap: 6px; color: var(--c-title); font-size: 13px; }
.captcha-row, .code-row { display: grid; grid-template-columns: 1fr auto; gap: 8px; align-items: center; }
.captcha {
  height: 40px;
  min-width: 120px;
  padding: 0 8px;
  border: 1px solid var(--c-border);
  border-radius: var(--radius-input);
  background: #fff;
  cursor: pointer;
  display: grid;
  place-items: center;
}
.captcha :deep(svg) { display: block; height: 32px; width: auto; }
.btn-ghost {
  height: 40px;
  padding: 0 14px;
  border-radius: var(--radius-input);
  border: 1px solid var(--c-border);
  background: #fff;
  color: var(--brand-primary);
  cursor: pointer;
  white-space: nowrap;
}
.submit { width: 100%; cursor: pointer; }
.links { margin: 0; }
.links a { color: var(--brand-primary); text-decoration: none; }
@media (max-width: 768px) {
  .login { grid-template-columns: 1fr; }
  aside, .panel { padding: 28px 20px; }
  aside h1 { font-size: 22px; }
}
</style>
