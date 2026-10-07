<script setup>
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { get } from '../api.js'

const router = useRouter()
const plans = ref([])

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
      <img class="logo" src="/logo.jpg" alt="用心云" />
      <span>
        <router-link to="/login">登录</router-link>
        <router-link to="/register">注册</router-link>
      </span>
    </header>
    <section class="hero">
      <h1>文件被加密之后，靠的是事先离线的那一份</h1>
      <p>2026 年 8 月，国家计算机病毒应急处理中心通报，我国境内出现多起「Sorry」勒索事件。重要文件被加密后，官方说明：没有解密密钥时，暂时没有可靠的恢复办法。</p>
      <p>用心云备份运行在 Windows 上。它不查杀病毒，也不能解密。它把文件和 SQL Server 打成 zip，再拷到已登记的移动硬盘并离线。</p>
      <p>三档版本按可同时登录设备数区分；续费按当时价格，在原到期日上顺延 365 天。价格由后台配置，促销期内显示促销价。</p>
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
      <a class="btn-secondary" href="http://download.yongxinyun.com/yongxinyunbackup/yongxinyunbackup.zip">下载</a>
    </section>
    <section class="notice">
      <h2>近期国内情况</h2>
      <p>央广网 2026 年 8 月 18 日的报道援引专家意见：预防和检测之外，备份是兜底。数据量大、停不起工的单位更常被盯上，中小企业、制造、医疗、金融都在其中。</p>
      <p>2026 年 7 月底到 8 月，安全机构公开的处置记录里，有企业服务器上的业务文件和数据库备份被一起加密。一直挂在本机、一直在线的备份，会和业务文件落在同一次加密里。</p>
      <p class="limit">本软件不写入侵过程，也不提供解密。系统补丁、账号口令和杀毒仍要单独做。</p>
    </section>
    <section class="cards">
      <article>
        <h2>文件备份</h2>
        <p>备份本机、共享目录或 FTP 上的文件夹。到点自动打成 zip，也可以选中后立即执行。可以为之后新打的包设置压缩密码。已经生成的旧包不会补上密码。</p>
      </article>
      <article>
        <h2>SQL Server</h2>
        <p>先登记实例并测试连接，再做库备份。zip 放在备份目录的「库备份」下。本期支持 SQL Server。</p>
      </article>
      <article>
        <h2>离线介质</h2>
        <p>登记 U 盘或移动硬盘。到开始时间把 zip 拷进盘，完成后按所选级别离线。默认去掉盘符并拒绝读写。这不是杀毒，也不会格式化磁盘。电脑没有网络时，已保存的离线任务到点仍会执行。</p>
      </article>
      <article>
        <h2>云、传输和邮件</h2>
        <p>授权有效时，zip 可以再上传七牛，或送到共享目录、FTP，并按结果发邮件。邮件标题和内容可以改。腾讯云、天翼云本期不能保存。</p>
      </article>
    </section>
    <section class="scope">
      <h2>授权停了以后还剩什么</h2>
      <p>没有有效授权时，文件备份、数据库备份、云、共享目录、FTP 和邮件会停用。离线介质在无网时仍按计划执行。日志留在本机，不上传到网站。</p>
    </section>
    <section id="download" class="download">
      <h2>下载</h2>
      <p>Windows 客户端可以直接下载。解压后安装，安装后必须登录。设备数按所购版本上限计入，可在门户或客户端踢出设备。</p>
      <a class="btn-yxy dl" href="http://download.yongxinyun.com/yongxinyunbackup/yongxinyunbackup.zip">下载客户端</a>
    </section>
    <footer>
      <p>常州用心软件有限公司</p>
      <p>联系电话 <a href="tel:4000519608">400-051-9608</a></p>
      <p>联系邮箱 market.yongxinyun.com</p>
      <p><router-link to="/terms">用户协议</router-link> <router-link to="/privacy">隐私政策</router-link></p>
      <p class="legal">Copyright ©2026 常州用心软件有限公司 <a href="https://beian.miit.gov.cn/" target="_blank" rel="noopener">苏ICP备19043516号-6</a> 公安备案号:<a href="https://beian.mps.gov.cn/#/query/webSearch?code=32041102001697" target="_blank" rel="noopener">苏公网安备32041102001697号</a></p>
    </footer>
  </div>
</template>

<style scoped>
.home { margin: -65px -16px; }
.nav, .cards, .download { max-width: 1400px; margin: 0 auto; padding: 24px 16px; }
.nav { display: flex; justify-content: space-between; align-items: center; }
.logo { height: 64px; width: auto; display: block; }
.nav a { margin-left: 16px; color: var(--brand-primary); }
.hero {
  background: linear-gradient(160deg, var(--c-hero-from), var(--c-hero-to));
  color: #fff;
  padding: 65px 16px;
}
.hero h1 { font-size: 32px; margin: 0 0 12px; max-width: 760px; line-height: 1.35; }
.hero p { max-width: 760px; }
.plans { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; max-width: 960px; margin: 20px 0; }
.plan { background: rgba(255,255,255,0.1); border: 1px solid rgba(255,255,255,0.35); padding: 16px; border-radius: 6px; }
.plan h3 { margin: 0 0 8px; font-size: 18px; }
.plan .devices { margin: 0 0 8px; opacity: 0.9; }
.plan .price { margin: 0 0 12px; }
.plan .price s { margin-right: 8px; opacity: 0.75; }
.plan .price strong { font-size: 24px; }
@media (max-width: 768px) { .plans { grid-template-columns: 1fr; } }
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
  text-decoration: none;
}
.notice, .scope, .download { max-width: 1400px; margin: 0 auto; padding: 8px 16px 24px; }
.notice, .scope {
  background: #fff;
  border-radius: 6px;
  padding: 24px;
  margin: 16px auto;
  max-width: calc(1400px - 32px);
}
.notice h2, .scope h2, .download h2, .cards h2 { margin: 0 0 8px; color: var(--c-title); font-size: 18px; }
.notice p, .scope p, .download p, .cards p { margin: 0 0 10px; line-height: 1.7; }
.limit { color: var(--c-text-muted); margin-bottom: 0; }
.cards { display: grid; grid-template-columns: repeat(2, 1fr); gap: 16px; }
.cards article { background: #fff; border-radius: 6px; padding: 24px; }
.download { background: var(--c-surface-alt); padding: 24px 16px; }
.dl { display: inline-block; line-height: 43px; text-decoration: none; }
footer { background: var(--c-footer); color: #fff; text-align: center; padding: 24px 16px; }
footer a { color: #fff; }
.legal { font-size: 13px; line-height: 1.7; }
@media (max-width: 768px) {
  .cards { grid-template-columns: 1fr; }
  .hero h1 { font-size: 24px; }
}
</style>
