export const SITE = 'https://backup.yongxincloud.cn'

const homeDescription = '用心云循备提供本地磁盘、移动硬盘或 U 盘离线、百度网盘与七牛云三重备份，用于勒索病毒的防范。数据直传客户自己的云账号，不中转、不托管。'
const homeKeywords = '用心云循备,用心云·循备,三重备份,勒索病毒的防范,离线备份,移动硬盘备份,U盘备份,百度网盘备份,七牛云备份,Windows备份软件,SQL Server备份,MySQL备份,用友软件,常州用友,金蝶软件,常州金蝶,ERP,企业管理软件,MES系统,用心云MES系统,电子看板,用心云电子看板,制造执行系统,数据备份,常州用心软件'

export function applySeo(to) {
  const title = to.meta?.title || '用心云·循备'
  const description = to.meta?.description || homeDescription
  const keywords = to.meta?.keywords || homeKeywords
  const robots = to.meta?.robots || (isPublic(to.path) ? 'index,follow' : 'noindex,follow')
  const path = to.path === '/' ? '/' : to.path
  const url = SITE + path
  document.title = title
  setMeta('description', description)
  setMeta('keywords', keywords)
  setMeta('robots', robots)
  setMeta('og:title', title, 'property')
  setMeta('og:description', description, 'property')
  setMeta('og:url', url, 'property')
  setMeta('og:type', 'website', 'property')
  setMeta('og:locale', 'zh_CN', 'property')
  setMeta('og:site_name', '用心云·循备', 'property')
  setLink('canonical', url)
}

function isPublic(path) {
  return path === '/' || path === '/about' || path === '/contact' || path === '/terms' || path === '/privacy'
}

function setMeta(name, content, attr = 'name') {
  let el = document.head.querySelector('meta[' + attr + '="' + name + '"]')
  if (!el) {
    el = document.createElement('meta')
    el.setAttribute(attr, name)
    document.head.appendChild(el)
  }
  el.setAttribute('content', content)
}

function setLink(rel, href) {
  let el = document.head.querySelector('link[rel="' + rel + '"]')
  if (!el) {
    el = document.createElement('link')
    el.setAttribute('rel', rel)
    document.head.appendChild(el)
  }
  el.setAttribute('href', href)
}
