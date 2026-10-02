import { createRouter, createWebHistory } from 'vue-router'
import HomeView from './views/HomeView.vue'
import RegisterView from './views/RegisterView.vue'
import LoginView from './views/LoginView.vue'
import ForgotView from './views/ForgotView.vue'
import OrdersView from './views/OrdersView.vue'
import PortalView from './views/PortalView.vue'
import PortalLayout from './views/PortalLayout.vue'
import InvoiceView from './views/InvoiceView.vue'
import ProviderApplyView from './views/ProviderApplyView.vue'
import DownloadView from './views/DownloadView.vue'
import CommissionView from './views/CommissionView.vue'
import VendorView from './views/VendorView.vue'
import VendorLoginView from './views/VendorLoginView.vue'
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
    {
      path: '/vendor',
      component: VendorLayout,
      children: [
        { path: '', component: VendorView }
      ]
    },
    {
      path: '/home',
      component: PortalLayout,
      children: [
        { path: '', component: PortalView },
        { path: '/orders', component: OrdersView },
        { path: '/portal/invoice', component: InvoiceView },
        { path: '/portal/provider', component: ProviderApplyView },
        { path: '/portal/download', component: DownloadView },
        { path: '/portal/commission', component: CommissionView },
        { path: '/portal/customers', component: CustomersView },
        { path: '/portal/settlements', component: SettlementsView },
        { path: '/portal/messages', component: MessagesView },
        { path: '/forgot', component: ForgotView }
      ]
    },
    { path: '/terms', component: TermsView },
    { path: '/privacy', component: PrivacyView },
    { path: '/:pathMatch(.*)*', component: NotFoundView }
  ]
})
