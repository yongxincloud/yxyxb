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
        <img class="nav-logo" src="/logo.png" :alt="PRODUCT_DISPLAY_NAME" />
      </router-link>
      <nav>
        <a href="#pricing">价格</a>
        <a href="#media">离线介质</a>
        <a href="#about">关于我们</a>
        <a href="#download">下载</a>
        <router-link to="/login">登录</router-link>
        <router-link class="nav-cta" to="/register">申请账号</router-link>
      </nav>
    </header>

    <section class="hero">
      <div class="hero-copy">
        <img class="hero-logo" src="/logo.png" :alt="PRODUCT_DISPLAY_NAME" />
        <p class="product">{{ PRODUCT_DISPLAY_NAME }}</p>
        <h1>本机加密备份，盘拷完还能离线</h1>
        <p class="lead">
          勒索加密往往连同在线备份一起带走。{{ PRODUCT_DISPLAY_NAME }}在 Windows 上把文件和数据库打成 zip，
          再拷到已登记的移动盘，并按计划让介质离线——无网时离线任务仍可执行。
        </p>
        <div class="cta">
          <a class="btn-yxy" :href="downloadUrl">下载客户端</a>
          <router-link class="btn-ghost" to="/register">申请账号</router-link>
        </div>
      </div>
      <div class="hero-visual" aria-hidden="true">
        <img src="/product1.png" alt="" />
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
        <h2>为管理软件与业务机准备的本机灾备</h2>
        <p>结构上对标常见灾备方案页，能力上突出离线介质——这是与「只做云端备份」产品的主差异。</p>
      </div>
      <ul class="value-list">
        <li>
          <strong>离线介质</strong>
          <span>登记 USB / 移动 SSD，到点把 zip 拷进盘，再按级别离线，降低同盘被一起加密的风险。</span>
        </li>
        <li>
          <strong>本机加密打包</strong>
          <span>文件、SQL Server、MySQL 打成 zip，日志留在本机，不依赖公网才能备份。</span>
        </li>
        <li>
          <strong>恢复入口</strong>
          <span>客户端侧栏提供库恢复操作，减少「只会备份不会还原」的断层。</span>
        </li>
        <li>
          <strong>授权与设备</strong>
          <span>个人 / 标准 / 专业按同时登录设备数分档，门户与客户端可踢出设备。</span>
        </li>
        <li>
          <strong>云与传输（可选）</strong>
          <span>授权有效时，可再上传七牛或送到共享目录、FTP，并按结果发邮件。</span>
        </li>
        <li>
          <strong>无网仍跑离线任务</strong>
          <span>已保存的离线介质任务到点仍会执行，适合断网机房与隔离网段。</span>
        </li>
      </ul>
    </section>

    <section id="media" class="shots">
      <article class="shot">
        <div class="shot-copy">
          <p class="eyebrow">主差异 · 离线介质</p>
          <h2>先登记盘，再谈拷贝</h2>
          <p>按序列号、签名、容量识别 USB / 移动 SSD。双击登记后，任务只写到已登记的介质，降低拷错盘的概率。</p>
        </div>
        <img src="/product4.png" alt="离线介质登记界面" />
      </article>
      <article class="shot reverse">
        <div class="shot-copy">
          <p class="eyebrow">介质任务</p>
          <h2>到点拷包，到点离线</h2>
          <p>设置任务开始时间与介质离线时间、保留版本与天数。拷贝完成后按所选级别隔离介质，而不是一直挂在系统里。</p>
        </div>
        <img src="/product3.png" alt="添加离线介质任务" />
      </article>
      <article class="shot">
        <div class="shot-copy">
          <p class="eyebrow">备份任务</p>
          <h2>文件与数据库同一套节奏</h2>
          <p>文件、MSSQL、MySQL 任务统一调度。先打到本机备份目录，再由介质任务把包带走。</p>
        </div>
        <img src="/product2.png" alt="备份任务列表" />
      </article>
      <article class="shot reverse">
        <div class="shot-copy">
          <p class="eyebrow">云与清理（可选）</p>
          <h2>授权期内再加一道去向</h2>
          <p>需要时配置百度网盘或七牛等通道，并按保留策略清理过期对象。云不是唯一依赖，离线盘才是兜底。</p>
        </div>
        <img src="/product5.png" alt="云备份配置" />
      </article>
    </section>

    <section class="notice">
      <div class="section-head">
        <h2>为什么要强调离线</h2>
        <p>
          2026 年境内多起勒索事件中，业务文件与「一直在线的备份」常被一并加密。
          {{ PRODUCT_DISPLAY_NAME }}不做查杀与解密；系统补丁、账号口令和杀毒仍需单独落实。
          它补的是：事先有一份可以离开主机的拷贝。
        </p>
      </div>
    </section>

    <section id="about" class="about">
      <div class="section-head">
        <h2>关于我们</h2>
        <p>常州用心软件有限公司为专注企业管理软件20年。公司地址：常州市新北区通江中路600号芝时商业广场2-905。400电话：400-051-9608。下面三款是目前的主要产品。</p>
      </div>
      <ul class="about-list">
        <li>
          <strong>用心云MES</strong>
          <span>面向生产现场的制造执行软件，管设备、数采、工单、质量、工序和现场进度。</span>
        </li>
        <li>
          <strong>用心云看板</strong>
          <span>把现场数据做成可视看板，方便在车间直接看进度和异常。</span>
        </li>
        <li>
          <strong>用心云循备</strong>
          <span>装在 Windows 上的备份工具。文件和数据库在本机打成 zip，再拷到离线介质。备份内容留在客户自己的电脑和盘上。</span>
        </li>
      </ul>
    </section>

    <section id="download" class="download">
      <div class="section-head">
        <h2>下载 Windows 客户端</h2>
        <p>解压后运行。安装后须登录；设备数按所购版本上限计入，可在门户或客户端踢出设备。</p>
      </div>
      <a class="btn-yxy" :href="downloadUrl">下载客户端</a>
    </section>

    <footer>
      <p>常州用心软件有限公司</p>
      <p>公司地址：常州市新北区通江中路600号芝时商业广场2-905</p>
      <p>400电话：<a href="tel:4000519608">400-051-9608</a></p>
      <p>联系邮箱 market@yongxinyun.com</p>
      <p><router-link to="/terms">《用户协议条款》</router-link> · <router-link to="/privacy">《隐私权协议条款》</router-link></p>
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

.pricing, .value, .notice, .about, .download {
  padding: 48px 0;
}
.about { background: #fff; }
.about-list {
  max-width: 1180px;
  margin: 0 auto;
  padding: 0 24px;
  list-style: none;
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
}
.about-list li {
  display: grid;
  gap: 8px;
  padding: 20px 18px;
  border: 1px solid var(--c-border);
  border-top: 3px solid #DC0606;
  border-radius: 4px;
  background: #FFFBFA;
}
.about-list strong { font-size: 18px; color: var(--c-title); }
.about-list span { line-height: 1.7; color: var(--c-text); }
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
  margin-left: 24px;
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
  .value-list,
  .about-list { grid-template-columns: 1fr; }
  .hero { padding-top: 12px; }
  .nav { padding-inline: 16px; }
  .section-head,
  .plans,
  .value-list,
  .about-list,
  .shots,
  .download .btn-yxy { padding-inline: 16px; }
  .download .btn-yxy { margin-left: 16px; }
}
</style>
