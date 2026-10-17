<script setup>
import MarketingChrome from '../components/MarketingChrome.vue'
import { PRODUCT_DISPLAY_NAME } from '../brand.js'
</script>

<template>
  <MarketingChrome>
    <article class="page">
      <p class="eyebrow">常州用心软件有限公司</p>
      <h1>联系我们</h1>
      <p>
        购买用心云循备、了解用心云MES系统、用心云电子看板，以及用友软件、金蝶软件、常州用友、常州金蝶相关的企业管理软件，都可以用下面的方式联系。
      </p>

      <dl>
        <div>
          <dt>公司名称</dt>
          <dd>常州用心软件有限公司</dd>
        </div>
        <div>
          <dt>公司地址</dt>
          <dd>常州市新北区通江中路600号芝时商业广场2-905</dd>
        </div>
        <div>
          <dt>400 电话</dt>
          <dd><a href="tel:4000519608">400-051-9608</a></dd>
        </div>
        <div>
          <dt>联系邮箱</dt>
          <dd><a href="mailto:market@yongxinyun.com">market@yongxinyun.com</a></dd>
        </div>
      </dl>

      <h2>可以咨询的事项</h2>
      <ul>
        <li>个人版、标准版、专业版的设备数量和购买方式。</li>
        <li>Windows 客户端下载、安装后登录，以及设备被占用时如何踢出。</li>
        <li>订单、授权期限和发票资料。</li>
        <li>备份目录、离线介质和数据库任务如何在客户端里设置。</li>
      </ul>
      <p>
        公司与产品介绍见
        <router-link to="/about">关于我们</router-link>。
        使用前请阅读
        <router-link to="/terms">用户协议条款</router-link>
        和
        <router-link to="/privacy">隐私权协议条款</router-link>。
      </p>
    </article>
  </MarketingChrome>
</template>

<style scoped>
.page { line-height: 1.8; }
.eyebrow { margin: 0 0 8px; color: #DC0606; font-weight: 700; letter-spacing: 0.06em; }
h1 { margin: 0 0 16px; font-size: 32px; color: var(--c-title); }
h2 { margin: 28px 0 8px; font-size: 20px; color: var(--c-title); }
p { margin: 0 0 12px; }
dl { margin: 0 0 8px; display: grid; gap: 12px; }
dl div {
  padding: 14px 16px;
  background: #fff;
  border: 1px solid var(--c-border);
  border-radius: 8px;
}
dt { color: var(--c-text-muted); font-size: 13px; }
dd { margin: 4px 0 0; color: var(--c-title); font-size: 18px; }
ul { margin: 0 0 16px; padding-left: 1.2em; }
a { color: #DC0606; }
</style>
