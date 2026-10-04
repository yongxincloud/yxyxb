async function send(path, options) {
  const res = await fetch(path, { credentials: 'same-origin', ...options })
  const text = await res.text()
  if (!text) return { ok: false, error: '服务器没有返回内容' }
  try {
    return JSON.parse(text)
  } catch {
    return { ok: false, error: '服务器返回了无法识别的内容' }
  }
}

export function get(path) {
  return send(path)
}

export function post(path, body) {
  return send(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body ?? {})
  })
}
