export function isCustomerPath(path) {
  return String(path || '').startsWith('/')
}
