import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { get } from './api.js'

export function day(value) {
  if (!value) return '—'
  return String(value).slice(0, 10)
}

export function payText(status) {
  if (status === 1) return '已支付'
  if (status === 2) return '已退款'
  return '待支付'
}

export function authText(status) {
  if (status === 1) return '有效'
  if (status === 0) return '已取消'
  if (status === 2) return '已到期'
  return '其他'
}

export function providerText(status) {
  if (status === 1) return '启用'
  if (status === 2) return '已解约'
  return '停用'
}

export function settleText(status) {
  if (status === 1) return '待审批'
  if (status === 2) return '已批准'
  if (status === 3) return '已付款'
  return '草稿'
}

export function periodText(end) {
  if (!end) return '未开通'
  const today = new Date()
  const todayText = today.getFullYear() + '-'
    + String(today.getMonth() + 1).padStart(2, '0') + '-'
    + String(today.getDate()).padStart(2, '0')
  return String(end).slice(0, 10) >= todayText ? '使用中' : '已到期'
}

export function useVendorBoard() {
  const router = useRouter()
  const board = ref(null)
  const loadError = ref('')

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

  return { board, loadError, load }
}
