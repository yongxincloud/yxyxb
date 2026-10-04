<script setup>
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { get } from '../api.js'

const router = useRouter()
const price = ref('')

onMounted(async () => {
  const data = await get('/api/public/price')
  if (data.ok) price.value = data.price
})

async function buy() {
  const me = await get('/api/account/me')
  router.push(me.ok ? '/orders' : '/login')
}
</script>

<template>
  <div class="home">
    <header class="nav">
      <img class="logo" src="/logo.jpg" alt="用心云" />
      <span>
        <router-link to="/login">登录</router-link>
        <router-link to="/register">注册</router-link>
      </span>
    </header>
    <section class="hero">
      <h1>Windows 备份，一个账号不限台数</h1>
      <p>文件、数据库和离线介质备份。登录并持有有效授权后，文件备份、数据库备份、云、UNC、FTP、邮件才可用。无网时离线介质仍可执行。</p>
      <p v-if="price">当前价格 {{ price }} 元 / 年，续费按当时价格，在原到期日上顺延 365 天。</p>
      <button class="btn-yxy" type="button" @click="buy">立即购买</button>
      <a class="btn-secondary" href="#download">下载</a>
    </section>
    <section class="cards">
      <article>
        <h2>文件与数据库</h2>
        <p>按任务备份本机文件和 SQL Server。授权有效期内可用。</p>
      </article>
      <article>
        <h2>离线介质</h2>
        <p>无网或联网校验未成功时，离线介质任务仍按计划执行。</p>
      </article>
      <article>
        <h2>云与传输</h2>
        <p>云、UNC、FTP、邮件在登录且授权有效时可用。</p>
      </article>
    </section>
    <section id="download" class="download">
      <p>客户端安装包从这里下载。安装后必须登录。</p>
    </section>
    <footer>
      <p>要开票由厂商开具，也可以选择不开票。一个账号不限台数。</p>
      <p><router-link to="/terms">用户协议</router-link> <router-link to="/privacy">隐私政策</router-link></p>
    </footer>
  </div>
</template>

<style scoped>
.home { margin: -65px -16px; }
.nav, .cards, .download, footer { max-width: 1400px; margin: 0 auto; padding: 24px 16px; }
.nav { display: flex; justify-content: space-between; align-items: center; }
.logo { height: 64px; width: auto; display: block; }
.nav a { margin-left: 16px; color: var(--brand-primary); }
.hero {
  background: linear-gradient(160deg, var(--c-hero-from), var(--c-hero-to));
  color: #fff;
  padding: 65px 16px;
}
.hero h1 { font-size: 26px; margin: 0 0 12px; }
.hero p { max-width: 720px; }
.btn-secondary {
  display: inline-block;
  margin-left: 12px;
  height: 43px;
  line-height: 43px;
  padding: 0 22px;
  border-radius: 6px;
  background: transparent;
  color: #fff;
  border: 1px solid #fff;
}
.cards { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; }
.cards article { background: #fff; border-radius: 6px; padding: 24px; }
.download { background: var(--c-surface-alt); }
footer { background: var(--c-footer); color: #fff; }
@media (max-width: 768px) {
  .cards { grid-template-columns: 1fr; }
  .hero h1 { font-size: 20px; }
}
</style>
