<script setup>
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { get, post } from '../api.js'

const router = useRouter()
const board = ref(null)
const loadError = ref('')
const message = ref('')
const email = ref('')
const days = ref(30)
const reason = ref('赠送')
const price = ref('')
const levelName = ref('')
const levelRate = ref('0.6')
const levelId = ref('')
const mailHost = ref('')
const mailPort = ref(587)
const mailUser = ref('')
const mailPassword = ref('')
const mailFrom = ref('')
const mailSsl = ref(true)
const mailConfigured = ref(false)
const mailHasPassword = ref(false)
const mailMessage = ref('')

function day(value) {
  if (!value) return '—'
  return String(value).slice(0, 10)
}
function payText(status) {
  if (status === 1) return '已支付'
  if (status === 2) return '已退款'
  return '待支付'
}
function authText(status) {
  if (status === 1) return '有效'
  if (status === 0) return '已取消'
  if (status === 2) return '已到期'
  return '其他'
}
function providerText(status) {
  if (status === 1) return '启用'
  if (status === 2) return '已解约'
  return '停用'
}
function settleText(status) {
  if (status === 1) return '待审批'
  if (status === 2) return '已批准'
  if (status === 3) return '已付款'
  return '草稿'
}

async function loadMail() {
  const data = await get('/api/vendor/mail')
  if (!data.ok) return
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

async function load() {
  loadError.value = ''
  try {
    const data = await get('/api/vendor/board')
    if (!data.ok) {
      router.replace('/vendor/login')
      return
    }
    board.value = data
  } catch {
    loadError.value = '暂时读不到数据'
  }
}

async function grant() {
  const data = await post('/api/vendor/grants', { email: email.value, days: Number(days.value), reason: reason.value })
  message.value = data.ok ? '已发放至 ' + day(data.authEndTime) : (data.error || '发放失败')
  await load()
}

async function changePrice() {
  const data = await post('/api/vendor/products/STD/price', { price: Number(price.value) })
  message.value = data.ok ? '价格已更新' : (data.error || '改价失败')
  await load()
}

async function createLevel() {
  const data = await post('/api/vendor/levels', { name: levelName.value, rate: Number(levelRate.value) })
  message.value = data.ok ? '等级已建立' : (data.error || '失败')
  await load()
}

async function approveApply(id) {
  const data = await post('/api/vendor/provider-applies/' + id + '/approve', { levelId: Number(levelId.value) })
  message.value = data.ok ? '已通过 ' + data.providerNo : (data.error || '审批失败')
  await load()
}

async function approveSettlement(id) {
  const data = await post('/api/vendor/settlements/' + id + '/approve', {})
  message.value = data.ok ? '结算已审批' : (data.error || '审批失败')
  await load()
}

async function issueInvoice(id) {
  const data = await post('/api/vendor/invoice-applies/' + id + '/issue', {})
  message.value = data.ok ? '已开票' : (data.error || '开票失败')
  await load()
}

onMounted(() => { load(); loadMail() })
</script>

<template>
  <p class="err" v-if="loadError">{{ loadError }}</p>
  <div v-if="board" class="board">
    <section class="card-yxy">
      <h1>全部信息</h1>
      <p class="lead">客户 {{ board.accounts.length }}，订单 {{ board.orders.length }}，服务商 {{ board.providers.length }}。</p>
    </section>

    <section id="customers" class="card-yxy">
      <h2>客户</h2>
      <table>
        <thead><tr><th>邮箱</th><th>客户号</th><th>归属服务商</th><th>注册日</th></tr></thead>
        <tbody>
          <tr v-for="row in board.customers" :key="row.id">
            <td>{{ row.email }}</td>
            <td>{{ row.customerNo }}</td>
            <td>{{ row.directProviderId || '厂商' }}</td>
            <td>{{ day(row.createdAt) }}</td>
          </tr>
          <tr v-if="!board.customers.length"><td colspan="4">暂无客户</td></tr>
        </tbody>
      </table>
    </section>

    <section id="orders" class="card-yxy">
      <h2>订单</h2>
      <table>
        <thead><tr><th>单号</th><th>邮箱</th><th>类型</th><th>金额</th><th>状态</th><th>下单日</th></tr></thead>
        <tbody>
          <tr v-for="row in board.orders" :key="row.id">
            <td>{{ row.orderNo }}</td>
            <td>{{ row.email }}</td>
            <td>{{ row.orderType === 2 ? '试用' : '正式' }}</td>
            <td>{{ row.amount }}</td>
            <td>{{ payText(row.payStatus) }}</td>
            <td>{{ day(row.createdAt) }}</td>
          </tr>
          <tr v-if="!board.orders.length"><td colspan="6">暂无订单</td></tr>
        </tbody>
      </table>
    </section>

    <section id="auths" class="card-yxy">
      <h2>授权</h2>
      <table>
        <thead><tr><th>邮箱</th><th>到期日</th><th>状态</th><th>来源</th><th>事由</th></tr></thead>
        <tbody>
          <tr v-for="row in board.auths" :key="row.id">
            <td>{{ row.email }}</td>
            <td>{{ day(row.authEndTime) }}</td>
            <td>{{ authText(row.status) }}</td>
            <td>{{ row.grantType === 2 ? '后台发放' : '订单' }}</td>
            <td>{{ row.grantReason || '—' }}</td>
          </tr>
          <tr v-if="!board.auths.length"><td colspan="5">暂无授权</td></tr>
        </tbody>
      </table>
    </section>

    <section id="providers" class="card-yxy">
      <h2>服务商与等级</h2>
      <table>
        <thead><tr><th>编号</th><th>名称</th><th>邮箱</th><th>等级</th><th>状态</th></tr></thead>
        <tbody>
          <tr v-for="row in board.providers" :key="row.id">
            <td>{{ row.providerNo }}</td>
            <td>{{ row.name }}</td>
            <td>{{ row.email || '—' }}</td>
            <td>{{ row.levelId }}</td>
            <td>{{ providerText(row.status) }}</td>
          </tr>
          <tr v-if="!board.providers.length"><td colspan="5">暂无服务商</td></tr>
        </tbody>
      </table>
      <p v-for="row in board.levels" :key="row.id">等级 {{ row.id }} {{ row.name }}，佣金比例 {{ row.commissionRate }}</p>
    </section>

    <section id="money" class="card-yxy">
      <h2>佣金、结算、开票</h2>
      <table>
        <thead><tr><th>佣金</th><th>服务商</th><th>方向</th><th>金额</th><th>结算</th></tr></thead>
        <tbody>
          <tr v-for="row in board.commissions" :key="row.id">
            <td>{{ row.id }}</td>
            <td>{{ row.providerId }}</td>
            <td>{{ row.entryType === 2 ? '退款' : '销售' }}</td>
            <td>{{ row.amount }}</td>
            <td>{{ row.settled ? '已结算' : '未结算' }}</td>
          </tr>
          <tr v-if="!board.commissions.length"><td colspan="5">暂无佣金</td></tr>
        </tbody>
      </table>
      <p v-for="row in board.settlements" :key="'s' + row.id">
        {{ row.settlementNo }} {{ row.amount }} 元 {{ settleText(row.status) }}
        <button v-if="row.status === 1" type="button" @click="approveSettlement(row.id)">审批</button>
      </p>
      <p v-for="row in board.invoices" :key="'i' + row.id">
        {{ row.applyNo }} {{ row.titleName }} {{ row.amount }} 元
        <button v-if="row.status === 1" type="button" @click="issueInvoice(row.id)">开票</button>
      </p>
      <p v-for="row in board.products" :key="row.productNo">{{ row.name }}（{{ row.productNo }}）{{ row.price }} 元 / {{ row.periodDays }} 天</p>
    </section>

    <section id="mail" class="card-yxy">
      <h2>发信服务器</h2>
      <p class="lead">{{ mailConfigured ? '已配置。验证码会发到用户邮箱，页面不再显示验证码。' : '还没配置。开发环境会把验证码直接显示在页面上。' }}</p>
      <div class="mail">
        <label>服务器<input class="field-yxy" v-model="mailHost" placeholder="smtp.example.com" /></label>
        <label>端口<input class="field-yxy" v-model="mailPort" type="number" /></label>
        <label>账号<input class="field-yxy" v-model="mailUser" placeholder="发信账号" /></label>
        <label>密码<input class="field-yxy" v-model="mailPassword" type="password" :placeholder="mailHasPassword ? '已保存，留空则不修改' : '发信密码'" /></label>
        <label>发件人<input class="field-yxy" v-model="mailFrom" placeholder="noreply@example.com" /></label>
        <label class="check"><input type="checkbox" v-model="mailSsl" /> 启用加密</label>
        <div class="actions">
          <button class="btn-yxy" type="button" @click="saveMail">保存</button>
          <button class="btn-yxy" type="button" @click="testMail">发测试信到发件人</button>
        </div>
        <p v-if="mailMessage">{{ mailMessage }}</p>
      </div>
    </section>

    <section id="ops" class="card-yxy">
      <h2>操作</h2>
      <p v-if="message">{{ message }}</p>
      <div class="form">
        <input class="field-yxy" v-model="email" placeholder="已注册邮箱" />
        <input class="field-yxy" v-model="days" type="number" placeholder="天数" />
        <input class="field-yxy" v-model="reason" placeholder="事由" />
        <button class="btn-yxy" type="button" @click="grant">发放授权</button>
      </div>
      <div class="form">
        <input class="field-yxy" v-model="price" placeholder="标准版新价格" />
        <button class="btn-yxy" type="button" @click="changePrice">修改价格</button>
      </div>
      <div class="form">
        <input class="field-yxy" v-model="levelName" placeholder="新等级名称" />
        <input class="field-yxy" v-model="levelRate" placeholder="佣金比例，如 0.6" />
        <button class="btn-yxy" type="button" @click="createLevel">新增等级</button>
      </div>
      <input class="field-yxy" v-model="levelId" placeholder="审批用的等级编号" />
      <p v-for="row in board.applies" :key="row.id">
        {{ row.companyName }} {{ row.taxNo }}
        <button type="button" @click="approveApply(row.id)">通过</button>
      </p>
      <p v-for="(row, index) in board.logs" :key="index">{{ row.module }} {{ row.action }} {{ row.beforeValue }} → {{ row.afterValue }}</p>
    </section>
  </div>
</template>

<style scoped>
.board { display: grid; gap: 16px; }
.lead { margin: 0; color: var(--c-text-muted); }
h2 { margin: 0; font-size: 16px; color: var(--c-title); }
table { width: 100%; border-collapse: collapse; font-size: 13px; }
th, td { text-align: left; padding: 8px 6px; border-bottom: 1px solid var(--c-border); }
th { color: var(--c-text-muted); font-weight: 500; }
.form { display: grid; grid-template-columns: 1fr 120px 1fr auto; gap: 8px; }
.mail { display: grid; gap: 10px; max-width: 480px; }
.mail label { display: grid; gap: 6px; color: var(--c-title); font-size: 13px; }
.mail .check { display: flex; align-items: center; gap: 8px; }
.actions { display: flex; gap: 8px; flex-wrap: wrap; }
button { cursor: pointer; }
@media (max-width: 768px) {
  .form { grid-template-columns: 1fr; }
}
</style>
