<script setup>
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { get } from '../api.js'
import { PRODUCT_DISPLAY_NAME } from '../brand.js'

const router = useRouter()
const plans = ref([])
const downloadUrl = 'http://download.yongxinyun.com/yxyxb/yxyxb.zip'

onMounted(async () => {
  const data = await get('/api/public/price')
  if (data.ok) plans.value = data.plans || []
})

async function buy(productNo) {
  const me = await get('/api/account/me')
  router.push(me.ok ? '/orders?productNo=' + encodeURIComponent(productNo || '') : '/login')
}
</script>

<template>
  <div class="home">
    <header class="nav">
      <router-link class="brand" to="/">
        <img class="nav-logo" src="/images/logo.png" :alt="PRODUCT_DISPLAY_NAME" />
      </router-link>
      <nav>
        <a href="#pricing">价格</a>
        <a href="#media">离线介质</a>
        <router-link to="/about">关于我们</router-link>
        <router-link to="/contact">联系我们</router-link>
        <a href="#download">下载</a>
        <router-link to="/login">登录</router-link>
        <router-link class="nav-cta" to="/register">申请账号</router-link>
      </nav>
    </header>

    <section class="hero">
      <div class="hero-copy">
        <img class="hero-logo" src="/images/logo.png" :alt="PRODUCT_DISPLAY_NAME" />
        <p class="product">{{ PRODUCT_DISPLAY_NAME }}</p>
        <h1>本地、离线、自有云，三重备份</h1>
        <p class="lead">
          {{ PRODUCT_DISPLAY_NAME }}装在客户自己的 Windows 电脑上，无需额外服务器。
          文件和数据库先备到本机磁盘，再拷到移动硬盘或 U 盘离线，也可直传到客户自己的百度网盘、七牛云。
          我们不中转、不托管、不碰业务数据。
        </p>
        <div class="cta">
          <a class="btn-yxy" :href="downloadUrl">下载客户端</a>
          <router-link class="btn-ghost" to="/register">申请账号</router-link>
        </div>
      </div>
      <div class="hero-visual">
        <img src="/images/product1.png" alt="用心云·循备 Windows 客户端：本地备份、离线介质与自有云盘" />
      </div>
    </section>

    <section id="pricing" class="pricing">
      <div class="section-head">
        <h2>选择适合你的设备档位</h2>
        <p>功能相同，差别在可同时登录的设备数。活动促销期内最总以促销价结算。</p>
      </div>
      <div class="plans" v-if="plans.length">
        <article v-for="plan in plans" :key="plan.productNo" class="plan">
          <h3>{{ plan.name }}</h3>
          <p class="devices">最多 {{ plan.maxDevices }} 台设备</p>
          <p class="price">
            <s v-if="plan.promoActive">¥{{ plan.listPrice }}</s>
            <strong>¥{{ plan.salePrice }}</strong>
            <span> / 年</span>
          </p>
          <button class="btn-yxy" type="button" @click="buy(plan.productNo)">立即购买</button>
        </article>
      </div>
      <p v-else class="plans-empty">价格加载中，或请确认接口已启动。</p>
    </section>

    <section id="value" class="value">
      <div class="section-head">
        <h2>三重备份，层层防护</h2>
        <p>本地磁盘、移动硬盘或 U 盘、自有云盘，三层都在。断网也能备，数据主权在客户手里。</p>
      </div>
      <ul class="value-list">
        <li>
          <strong>本地磁盘备份</strong>
          <span>文件、SQL Server、MySQL 先在本机打成备份包，留在客户自己的电脑上。</span>
        </li>
        <li>
          <strong>离线介质，物理隔离</strong>
          <span>支持备份到移动硬盘、U 盘。断网也能备，断网更安全，用于勒索病毒的防范和网络攻击防护。</span>
        </li>
        <li>
          <strong>自有云盘在线备份</strong>
          <span>可直传百度网盘、七牛云等客户自己的云账号。数据直传，我们不中转、不托管。</span>
        </li>
        <li>
          <strong>数据主权归你</strong>
          <span>云端用的是客户自己的账号。业务数据不经过我们的服务器，我们不碰备份内容。</span>
        </li>
        <li>
          <strong>轻量免运维</strong>
          <span>装在客户自己的 Windows 电脑上，装好即用，无需额外服务器。</span>
        </li>
        <li>
          <strong>三层一起用</strong>
          <span>本地、离线、自有云缺一不可。在线备份被一起加密时，离线盘仍是离开主机的那一份。</span>
        </li>
      </ul>
    </section>

    <section id="media" class="shots">
      <article class="shot">
        <div class="shot-copy">
          <p class="eyebrow">离线介质 · 物理隔离</p>
          <h2>移动硬盘、U 盘，断网也能备</h2>
          <p>按序列号、签名、容量识别介质。备份写到已登记的盘上，和主机、网络分开，用来挡住勒索和网络攻击。</p>
        </div>
        <img src="/images/product4.png" alt="离线介质登记界面" />
      </article>
      <article class="shot reverse">
        <div class="shot-copy">
          <p class="eyebrow">介质任务</p>
          <h2>到点拷包，到点离线</h2>
          <p>设置任务开始时间与介质离线时间、保留版本与天数。拷贝完成后按所选级别隔离介质，而不是一直挂在系统里。</p>
        </div>
        <img src="/images/product3.png" alt="添加离线介质任务" />
      </article>
      <article class="shot">
        <div class="shot-copy">
          <p class="eyebrow">本地磁盘</p>
          <h2>先落在自己的电脑上</h2>
          <p>文件、SQL Server、MySQL 按同一套节奏打包，先存进本机备份目录，再由离线介质或自有云盘带走。</p>
        </div>
        <img src="/images/product2.png" alt="备份任务列表" />
      </article>
      <article class="shot reverse">
        <div class="shot-copy">
          <p class="eyebrow">自有云盘</p>
          <h2>百度网盘、七牛云，数据直传</h2>
          <p>用客户自己的云账号。备份从电脑直接传到云盘，我们不中转、不托管、不碰业务数据。</p>
        </div>
        <img src="/images/product5.png" alt="云备份配置" />
      </article>
    </section>

    <section class="notice">
      <div class="section-head">
        <h2>为什么离线这一层不能省</h2>
        <p>
          一直挂在网上的备份，可能和业务文件一起被加密。
          移动硬盘和 U 盘可以在拷完后离开主机，断网时任务仍能执行。
          {{ PRODUCT_DISPLAY_NAME }}不做查杀和解密，它提供的是离开网络的那一份拷贝。
        </p>
      </div>
    </section>

    <section id="download" class="download">
      <div class="section-head">
        <h2>下载 Windows 客户端</h2>
        <p>装在客户自己的 Windows 电脑上，装好即用，无需额外服务器。安装后登录；设备数按所购版本计入。</p>
      </div>
      <a class="btn-yxy" :href="downloadUrl">下载客户端</a>
    </section>

    <footer>
      <p>常州用心软件有限公司</p>
      <p>公司地址：常州市新北区通江中路600号芝时商业广场2-905</p>
      <p>400电话：<a href="tel:4000519608">400-051-9608</a></p>
      <p>联系邮箱 market@yongxinyun.com</p>
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
.home {
  margin: -65px -16px 0;
  color: var(--c-text);
  background:
    radial-gradient(1200px 480px at 10% -10%, rgba(220, 6, 6, 0.08), transparent 55%),
    linear-gradient(180deg, #F8F4F2 0%, var(--c-portal-bg) 28%, var(--c-portal-bg) 100%);
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
.nav a:hover { color: #DC0606; }
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

.hero {
  max-width: 1180px;
  margin: 0 auto;
  padding: 28px 24px 56px;
  display: grid;
  grid-template-columns: minmax(280px, 0.95fr) minmax(320px, 1.15fr);
  gap: 36px;
  align-items: center;
  min-height: calc(100vh - 100px);
}
.hero-copy { text-align: left; }
.hero-logo {
  height: 88px;
  width: auto;
  display: block;
  margin-bottom: 18px;
}
.product {
  margin: 0 0 10px;
  font-size: 15px;
  font-weight: 700;
  letter-spacing: 0.08em;
  color: #DC0606;
}
.hero h1 {
  margin: 0 0 16px;
  font-family: var(--font-display);
  font-size: clamp(28px, 4vw, 40px);
  line-height: 1.28;
  color: var(--c-title);
  font-weight: 700;
}
.lead {
  margin: 0 0 24px;
  max-width: 34em;
  line-height: 1.75;
  font-size: 15px;
  color: var(--c-text);
}
.cta { display: flex; flex-wrap: wrap; gap: 12px; }
.btn-ghost {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  height: 43px;
  padding: 0 22px;
  border-radius: var(--radius-pill);
  border: 1px solid #1C1214;
  color: #1C1214;
  text-decoration: none;
  background: transparent;
}
.btn-ghost:hover { border-color: #DC0606; color: #DC0606; }

.hero-visual {
  position: relative;
  animation: hero-in 0.8s ease both;
}
.hero-visual img {
  display: block;
  width: 100%;
  height: auto;
  border-radius: 4px;
  box-shadow: 0 18px 50px rgba(28, 18, 20, 0.18);
  border: 1px solid rgba(28, 18, 20, 0.08);
}
@keyframes hero-in {
  from { opacity: 0; transform: translateY(18px); }
  to { opacity: 1; transform: none; }
}

.section-head {
  max-width: 1180px;
  margin: 0 auto 28px;
  padding: 0 24px;
  text-align: left;
}
.section-head h2 {
  margin: 0 0 10px;
  font-family: var(--font-display);
  font-size: 28px;
  color: var(--c-title);
  font-weight: 700;
}
.section-head p {
  margin: 0;
  max-width: 40em;
  line-height: 1.7;
  color: var(--c-text);
}

.pricing, .value, .notice, .download {
  padding: 48px 0;
}
.pricing { background: #fff; }
.plans {
  max-width: 1180px;
  margin: 0 auto;
  padding: 0 24px;
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
}
.plan {
  border: 1px solid var(--c-border);
  border-radius: 4px;
  padding: 22px 20px;
  text-align: left;
  background: #FFFBFA;
}
.plan h3 { margin: 0 0 8px; font-size: 18px; color: var(--c-title); }
.plan .devices { margin: 0 0 12px; color: var(--c-text-muted); }
.plan .price { margin: 0 0 18px; }
.plan .price s { margin-right: 8px; color: var(--c-text-muted); }
.plan .price strong { font-size: 28px; color: #DC0606; }
.plans-empty { max-width: 1180px; margin: 0 auto; padding: 0 24px; color: var(--c-text-muted); }

.value-list {
  max-width: 1180px;
  margin: 0 auto;
  padding: 0 24px;
  list-style: none;
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 18px 28px;
}
.value-list li {
  display: grid;
  gap: 6px;
  padding: 0 0 0 14px;
  border-left: 3px solid #DC0606;
  animation: fade-up 0.6s ease both;
}
.value-list strong {
  font-size: 16px;
  color: var(--c-title);
}
.value-list span {
  line-height: 1.65;
  color: var(--c-text);
}
.value-list li:nth-child(1) { animation-delay: 0.05s; }
.value-list li:nth-child(2) { animation-delay: 0.1s; }
.value-list li:nth-child(3) { animation-delay: 0.15s; }

.shots {
  max-width: 1180px;
  margin: 0 auto;
  padding: 24px 24px 56px;
  display: grid;
  gap: 48px;
}
.shot {
  display: grid;
  grid-template-columns: 0.9fr 1.1fr;
  gap: 28px;
  align-items: center;
}
.shot.reverse { grid-template-columns: 1.1fr 0.9fr; }
.shot.reverse .shot-copy { order: 2; }
.shot.reverse img { order: 1; }
.shot-copy { text-align: left; }
.eyebrow {
  margin: 0 0 8px;
  font-size: 13px;
  font-weight: 700;
  letter-spacing: 0.06em;
  color: #DC0606;
}
.shot h2 {
  margin: 0 0 12px;
  font-family: var(--font-display);
  font-size: 24px;
  color: var(--c-title);
}
.shot p { margin: 0; line-height: 1.7; }
.shot img {
  width: 100%;
  height: auto;
  display: block;
  border-radius: 4px;
  border: 1px solid var(--c-border);
  box-shadow: 0 12px 32px rgba(28, 18, 20, 0.1);
}

.notice { background: #1C1214; color: rgba(255, 255, 255, 0.86); }
.notice .section-head h2 { color: #fff; }
.notice .section-head p { color: rgba(255, 255, 255, 0.78); max-width: 46em; }

.download {
  text-align: left;
  background: #fff;
}
.download .btn-yxy {
  margin-left: max(24px, calc((100% - 1180px) / 2 + 24px));
  text-decoration: none;
  display: inline-flex;
  align-items: center;
}

footer {
  background: #1C1214;
  color: rgba(255, 255, 255, 0.88);
  text-align: left;
  padding: 36px 24px 48px;
}
footer > * {
  max-width: 1180px;
  margin-left: auto;
  margin-right: auto;
}
footer p { margin: 0 0 8px; }
footer a { color: #fff; }
.legal { font-size: 13px; line-height: 1.7; opacity: 0.85; }

@keyframes fade-up {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: none; }
}

@media (max-width: 900px) {
  .hero,
  .shot,
  .shot.reverse {
    grid-template-columns: 1fr;
    min-height: 0;
  }
  .shot.reverse .shot-copy,
  .shot.reverse img { order: initial; }
  .plans,
  .value-list { grid-template-columns: 1fr; }
  .hero { padding-top: 12px; }
  .nav { padding-inline: 16px; }
  .section-head,
  .plans,
  .value-list,
  .shots { padding-inline: 16px; }
  .download .btn-yxy { margin-left: 16px; }
}
</style>
