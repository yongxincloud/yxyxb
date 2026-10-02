import { createRouter, createWebHistory } from 'vue-router'
import HomeView from './views/HomeView.vue'
import RegisterView from './views/RegisterView.vue'
import LoginView from './views/LoginView.vue'
import ForgotView from './views/ForgotView.vue'
import OrdersView from './views/OrdersView.vue'
import PortalView from './views/PortalView.vue'
import InvoiceView from './views/InvoiceView.vue'
import ProviderApplyView from './views/ProviderApplyView.vue'
import DownloadView from './views/DownloadView.vue'
import CommissionView from './views/CommissionView.vue'
import VendorView from './views/VendorView.vue'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: HomeView },
    { path: '/register', component: RegisterView },
    { path: '/login', component: LoginView },
    { path: '/forgot', component: ForgotView },
    { path: '/home', component: PortalView },
    { path: '/orders', component: OrdersView },
    { path: '/portal/invoice', component: InvoiceView },
    { path: '/portal/provider', component: ProviderApplyView },
    { path: '/portal/download', component: DownloadView },
    { path: '/portal/commission', component: CommissionView },
    { path: '/vendor', component: VendorView }
  ]
})
