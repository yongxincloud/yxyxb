import { createRouter, createWebHistory } from 'vue-router'
import HomeView from './views/HomeView.vue'
import RegisterView from './views/RegisterView.vue'
import LoginView from './views/LoginView.vue'
import ForgotView from './views/ForgotView.vue'
import OrdersView from './views/OrdersView.vue'
import PortalDevicesView from './views/PortalDevicesView.vue'
import PortalView from './views/PortalView.vue'
import PortalLayout from './views/PortalLayout.vue'
import InvoiceView from './views/InvoiceView.vue'
import AccountView from './views/AccountView.vue'
import ProviderApplyView from './views/ProviderApplyView.vue'
import DownloadView from './views/DownloadView.vue'
import CommissionView from './views/CommissionView.vue'
import VendorView from './views/VendorView.vue'
import VendorCustomersView from './views/VendorCustomersView.vue'
import VendorDevicesView from './views/VendorDevicesView.vue'
import VendorOrdersView from './views/VendorOrdersView.vue'
import VendorAuthsView from './views/VendorAuthsView.vue'
import VendorProvidersView from './views/VendorProvidersView.vue'
import VendorMoneyView from './views/VendorMoneyView.vue'
import VendorProductsView from './views/VendorProductsView.vue'
import VendorMailView from './views/VendorMailView.vue'
import VendorLogsView from './views/VendorLogsView.vue'
import VendorLoginView from './views/VendorLoginView.vue'
import VendorForgotView from './views/VendorForgotView.vue'
import VendorLayout from './views/VendorLayout.vue'
import CustomersView from './views/CustomersView.vue'
import SettlementsView from './views/SettlementsView.vue'
import MessagesView from './views/MessagesView.vue'
import AboutView from './views/AboutView.vue'
import ContactView from './views/ContactView.vue'
import TermsView from './views/TermsView.vue'
import PrivacyView from './views/PrivacyView.vue'
import NotFoundView from './views/NotFoundView.vue'

const homeKeywords = '用心云循备,用心云·循备,三重备份,勒索病毒的防范,离线备份,移动硬盘备份,U盘备份,百度网盘备份,七牛云备份,Windows备份软件,SQL Server备份,MySQL备份,用友软件,常州用友,金蝶软件,常州金蝶,ERP,企业管理软件,MES系统,用心云MES系统,电子看板,用心云电子看板,制造执行系统,数据备份,常州用心软件'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      component: HomeView,
      meta: {
        title: '用心云循备｜三重备份与勒索病毒的防范',
        description: '用心云循备提供本地磁盘、移动硬盘或 U 盘离线、百度网盘与七牛云三重备份，用于勒索病毒的防范。数据直传客户自己的云账号，不中转、不托管。',
        keywords: homeKeywords
      }
    },
    { path: '/register', component: RegisterView },
    { path: '/login', component: LoginView },
    { path: '/vendor/login', component: VendorLoginView },
    { path: '/vendor/forgot', component: VendorForgotView },
    {
      path: '/vendor',
      component: VendorLayout,
      children: [
        { path: '', component: VendorView },
        { path: 'customers', component: VendorCustomersView },
        { path: 'devices', component: VendorDevicesView },
        { path: 'orders', component: VendorOrdersView },
        { path: 'auths', component: VendorAuthsView },
        { path: 'providers', component: VendorProvidersView },
        { path: 'money', component: VendorMoneyView },
        { path: 'products', component: VendorProductsView },
        { path: 'mail', component: VendorMailView },
        { path: 'logs', component: VendorLogsView }
      ]
    },
    {
      path: '/home',
      component: PortalLayout,
      children: [
        { path: '', component: PortalView },
        { path: '/orders', component: OrdersView },
        { path: '/portal/devices', component: PortalDevicesView },
        { path: '/portal/invoice', component: InvoiceView },
        { path: '/portal/provider', component: ProviderApplyView },
        { path: '/portal/download', component: DownloadView },
        { path: '/portal/commission', component: CommissionView },
        { path: '/portal/customers', component: CustomersView },
        { path: '/portal/settlements', component: SettlementsView },
        { path: '/portal/messages', component: MessagesView },
        { path: '/portal/account', component: AccountView },
        { path: '/forgot', component: ForgotView }
      ]
    },
    {
      path: '/about',
      component: AboutView,
      meta: {
        title: '关于我们｜用心云MES系统、电子看板与用心云循备',
        description: '常州用心软件有限公司提供用心云MES系统、用心云电子看板和用心云循备，并从事用友软件、常州用友、金蝶软件、常州金蝶等 ERP 与企业管理软件服务。',
        keywords: '用心云MES系统,MES系统,用心云电子看板,电子看板,用心云循备,用友软件,常州用友,金蝶软件,常州金蝶,ERP,企业管理软件,常州用心软件'
      }
    },
    {
      path: '/contact',
      component: ContactView,
      meta: {
        title: '联系我们｜用心云·循备 400-051-9608',
        description: '联系常州用心软件有限公司：服务热线 400-051-9608，邮箱 market@yongxinyun.com，地址常州市新北区通江中路 600 号芝时商业广场 2-905。',
        keywords: '联系我们,400-051-9608,常州用友,常州金蝶,用友软件,金蝶软件,用心云MES系统,用心云电子看板,用心云循备,企业管理软件,常州用心软件'
      }
    },
    {
      path: '/terms',
      component: TermsView,
      meta: {
        title: '用户协议｜用心云·循备',
        description: '用心云·循备用户协议：软件装在客户自己的电脑上，业务数据不上传到本公司服务器。',
        keywords: '用户协议,用心云循备,常州用心软件'
      }
    },
    {
      path: '/privacy',
      component: PrivacyView,
      meta: {
        title: '隐私权协议｜用心云·循备',
        description: '用心云·循备隐私权协议：网站只保存账号、订单、授权和设备记录，不保管客户的业务备份数据。',
        keywords: '隐私权协议,用心云循备,常州用心软件'
      }
    },
    { path: '/:pathMatch(.*)*', component: NotFoundView }
  ]
})
