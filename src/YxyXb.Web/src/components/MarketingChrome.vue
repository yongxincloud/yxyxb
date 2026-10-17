<script setup>
import { PRODUCT_DISPLAY_NAME } from '../brand.js'
</script>

<template>
  <div class="site">
    <header class="nav">
      <router-link class="brand" to="/">
        <img class="nav-logo" src="/images/logo.png" :alt="PRODUCT_DISPLAY_NAME" />
      </router-link>
      <nav>
        <router-link to="/">首页</router-link>
        <router-link to="/about">关于我们</router-link>
        <router-link to="/contact">联系我们</router-link>
        <router-link to="/login">登录</router-link>
        <router-link class="nav-cta" to="/register">申请账号</router-link>
      </nav>
    </header>
    <main>
      <slot />
    </main>
    <footer>
      <p>常州用心软件有限公司</p>
      <p>公司地址：常州市新北区通江中路600号芝时商业广场2-905</p>
      <p>400电话：<a href="tel:4000519608">400-051-9608</a></p>
      <p>联系邮箱 <a href="mailto:market@yongxinyun.com">market@yongxinyun.com</a></p>
      <p>
        <router-link to="/about">关于我们</router-link>
        ·
        <router-link to="/contact">联系我们</router-link>
        ·
        <router-link to="/terms">《用户协议条款》</router-link>
        ·
        <router-link to="/privacy">《隐私权协议条款》</router-link>
      </p>
      <p class="legal">
        版权所有：常州用心软件有限公司
        <a href="https://beian.miit.gov.cn/" target="_blank" rel="noopener">苏ICP备19043516号-6</a>
        公安备案号:
        <a href="https://beian.mps.gov.cn/#/query/webSearch?code=32041102001697" target="_blank" rel="noopener">苏公网安备32041102001697号</a>
      </p>
    </footer>
  </div>
</template>

<style scoped>
.site {
  margin: -65px -16px 0;
  color: var(--c-text);
  background:
    radial-gradient(1200px 480px at 10% -10%, rgba(220, 6, 6, 0.08), transparent 55%),
    linear-gradient(180deg, #F8F4F2 0%, var(--c-portal-bg) 28%, var(--c-portal-bg) 100%);
  min-height: 100vh;
}
.nav {
  max-width: 1180px;
  margin: 0 auto;
  padding: 18px 24px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
}
.brand { display: flex; align-items: center; text-decoration: none; }
.nav-logo { height: 48px; width: auto; display: block; }
.nav nav { display: flex; align-items: center; flex-wrap: wrap; gap: 4px 14px; }
.nav a {
  color: var(--c-title);
  text-decoration: none;
  font-size: 14px;
  font-weight: 500;
}
.nav a:hover,
.nav a.router-link-active { color: #DC0606; }
.nav-cta {
  display: inline-flex;
  align-items: center;
  height: 36px;
  padding: 0 14px;
  border-radius: 4px;
  background: #DC0606;
  color: #fff !important;
}
.nav-cta:hover { background: #B00404; }
main { max-width: 880px; margin: 0 auto; padding: 12px 24px 64px; }
footer {
  max-width: 1180px;
  margin: 0 auto;
  padding: 28px 24px 40px;
  color: #fff;
  background: #1C1214;
  border-radius: 0;
}
footer p { margin: 0 0 8px; line-height: 1.7; }
footer a { color: #fff; }
.legal { color: rgba(255,255,255,.82); }
</style>
