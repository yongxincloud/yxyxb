<script setup>
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { get, post } from '../api.js'

const router = useRouter()
const mailHost = ref('')
const mailPort = ref(587)
const mailUser = ref('')
const mailPassword = ref('')
const mailFrom = ref('')
const mailSsl = ref(true)
const mailConfigured = ref(false)
const mailHasPassword = ref(false)
const mailMessage = ref('')
const loadError = ref('')

async function loadMail() {
  const data = await get('/api/vendor/mail')
  if (!data.ok) {
    router.replace('/vendor/login')
    return
  }
  mailHost.value = data.host || ''
  mailPort.value = data.port || 587
  mailUser.value = data.user || ''
  mailFrom.value = data.from || ''
  mailSsl.value = data.ssl !== false
  mailConfigured.value = !!data.configured
  mailHasPassword.value = !!data.hasPassword
  mailPassword.value = ''
}

async function saveMail() {
  const data = await post('/api/vendor/mail', {
    host: mailHost.value,
    port: Number(mailPort.value),
    user: mailUser.value,
    password: mailPassword.value,
    from: mailFrom.value,
    ssl: mailSsl.value
  })
  mailMessage.value = data.ok ? '发信服务器已保存' : (data.error || '保存失败')
  if (data.ok) await loadMail()
}

async function testMail() {
  const data = await post('/api/vendor/mail/test', { email: mailFrom.value })
  mailMessage.value = data.ok ? '测试邮件已发出' : (data.error || '发送失败')
}

onMounted(async () => {
  try {
    await loadMail()
  } catch {
    loadError.value = '暂时读不到数据'
  }
})
</script>

<template>
  <p class="err" v-if="loadError">{{ loadError }}</p>
  <section class="card-yxy">
    <h1>发信服务器</h1>
    <p class="lead">{{ mailConfigured ? '已配置。验证码会发到用户邮箱，页面不再显示验证码。' : '还没配置。开发环境会把验证码直接显示在页面上。' }}</p>
    <div class="mail">
      <label>服务器<input class="field-yxy" v-model="mailHost" placeholder="smtp.example.com" /></label>
      <label>端口<input class="field-yxy" v-model="mailPort" type="number" /></label>
      <label>账号<input class="field-yxy" v-model="mailUser" placeholder="发信账号" /></label>
      <label>密码<input class="field-yxy" v-model="mailPassword" type="password" :placeholder="mailHasPassword ? '已保存，留空则不修改' : '发信密码'" /></label>
      <label>发件人<input class="field-yxy" v-model="mailFrom" placeholder="noreply@example.com" /></label>
      <label class="check"><input type="checkbox" v-model="mailSsl" /> 启用加密</label>
      <div class="actions">
        <button class="btn-yxy mini" type="button" @click="saveMail">保存</button>
        <button class="btn-yxy mini" type="button" @click="testMail">发测试信到发件人</button>
      </div>
      <p v-if="mailMessage" class="lead">{{ mailMessage }}</p>
    </div>
  </section>
</template>

<style scoped>
.mail { display: grid; gap: 10px; max-width: 480px; }
.mail label { display: grid; gap: 6px; color: var(--c-title); font-size: 13px; }
.mail .check { display: flex; align-items: center; gap: 8px; }
.actions { display: flex; gap: 8px; flex-wrap: wrap; }
</style>
