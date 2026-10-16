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
import TermsView from './views/TermsView.vue'
import PrivacyView from './views/PrivacyView.vue'
import NotFoundView from './views/NotFoundView.vue'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: HomeView },
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
    { path: '/terms', component: TermsView },
    { path: '/privacy', component: PrivacyView },
    { path: '/:pathMatch(.*)*', component: NotFoundView }
  ]
})
