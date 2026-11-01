export function toAbsolutePromoUrl(url, origin) {
  if (!url) return ''
  const raw = String(url).trim()
  if (/^https?:\/\//i.test(raw)) return raw
  const base = String(origin || '').replace(/\/$/, '')
  if (!base) return raw
  return raw.startsWith('/') ? base + raw : base + '/' + raw
}
