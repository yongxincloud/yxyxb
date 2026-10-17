<script setup>
import MarketingChrome from '../components/MarketingChrome.vue'
import { PRODUCT_DISPLAY_NAME } from '../brand.js'
</script>

<template>
  <MarketingChrome>
    <article class="page">
      <p class="eyebrow">常州用心软件有限公司</p>
      <h1>关于我们</h1>
      <p>
        常州用心软件有限公司的主要产品是用心云 MES、用心云看板 和 {{ PRODUCT_DISPLAY_NAME }}。
        办公地址在常州市新北区通江中路 600 号芝时商业广场 2-905。
      </p>
      <ul class="products">
        <li>
          <strong>用心云MES系统</strong>
          <span>面向制造业的 MES系统。一系统管住车间、设备、工单、质量与进度，让生产全程透明、高效可控。</span>
        </li>
        <li>
          <strong>用心云电子看板</strong>
          <span>把企业 ERP、MES系统里已有的数据做成电子看板，实时显示到大屏。用心云电子看板打通企业管理软件的最后一段，让管理从看报表变成看现场。</span>
        </li>
        <li>
          <strong>{{ PRODUCT_DISPLAY_NAME }}</strong>
          <span>本地（含离线介质）+自有云盘三重备份，文件数据库自动打包，数据存你手里、你账号里，我们只管授权，不碰你的业务数据。</span>
        </li>
      </ul>
      <p>
        公司是用友软件在常州的服务经销机构。在常州，客户也会按常州用友、常州金蝶来找我们。
        团队经营过用友软件、金蝶软件、畅捷通、鼎捷等 ERP 的销售与实施，从事企业管理软件已有 20 多年。
      </p>

      <h2>发展节点</h2>
      <ol>
        <li>2019 年公司成立，推出本地部署的 MES 1.0。</li>
        <li>2020 年推出用心云 MES，面向中小微企业的数字化入门。</li>
        <li>2021 年打通用友、金蝶 Open API，形成 ERP + MES；推出质检与 PDA、协同小程序，并开始 SaaS 化。与 ERP 对接的中小企业客户超过 200 家。</li>
        <li>2022 年支持用友 U8+，轻 MES 入驻畅捷通 T+ 云应用市场。</li>
        <li>2023 年轻 MES 在多个行业应用，成为用友畅捷通合格 ISV。</li>
        <li>2024 年支持国产化系统，并与鸿蒙、安卓、iOS 及小程序配合使用。</li>
        <li>2025 年 7 月推出用心云伙伴 SaaS，面向合作伙伴做订阅合作。</li>
        <li>公司规划 2027 年发布用心云 4.0，接入大模型和智能助手。</li>
      </ol>

      <h2>联系方式</h2>
      <ul>
        <li><strong>服务热线</strong><span>400-051-9608</span></li>
        <li><strong>电话 / 传真</strong><span>0519-89889576</span></li>
        <li><strong>邮箱</strong><span>market@yongxinyun.com</span></li>
        <li><strong>服务时间</strong><span>周一至周六 8:30—17:30</span></li>
        <li><strong>地址</strong><span>常州市新北区通江中路 600 号芝时商业广场 2-905</span></li>
      </ul>
      <p>购买、下载和账号问题见 <router-link to="/contact">联系我们</router-link>。</p>
    </article>
  </MarketingChrome>
</template>

<style scoped>
.page { line-height: 1.8; }
.eyebrow { margin: 0 0 8px; color: #DC0606; font-weight: 700; letter-spacing: 0.06em; }
h1 { margin: 0 0 16px; font-size: 32px; color: var(--c-title); }
h2 { margin: 28px 0 8px; font-size: 20px; color: var(--c-title); }
p { margin: 0 0 12px; }
ul, ol { margin: 0 0 16px; padding: 0; list-style: none; display: grid; gap: 12px; }
ol { counter-reset: step; }
ol li { counter-increment: step; }
ol li::before {
  content: counter(step);
  display: inline-block;
  margin-right: 8px;
  color: #DC0606;
  font-weight: 700;
}
li {
  padding: 14px 16px;
  background: #fff;
  border: 1px solid var(--c-border);
  border-radius: 8px;
}
strong { display: block; color: var(--c-title); }
span { color: var(--c-text); }
a { color: #DC0606; }
</style>
