async function send(path, options) {
  const res = await fetch(path, { credentials: 'same-origin', ...options })
  return res.json()
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
