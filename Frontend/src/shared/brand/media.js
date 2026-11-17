import manifest from './media-manifest.json';

export function mediaUrl(source) {
  if (!source) return '';
  const url = typeof source === 'string' ? source : source.sourceUrl || source.mediaItemUrl || source.url || '';
  return manifest[url] || url;
}

export function referenceLink(source = '') {
  if (!source) return '/contact';
  if (/^(mailto:|tel:|#)/.test(source)) return source;
  let url;
  try { url = new URL(source, 'https://mygarage.dk'); } catch { return '/contact'; }
  if(!['http:','https:'].includes(url.protocol))return '/contact';
  if (url.hostname !== 'mygarage.dk') return url.href;
  let path = url.pathname.replace(/\/$/, '') || '/en';
  const aliases={'/':'/en','/om-my-garage':'/en/about-my-garage','/partnere':'/en/partners','/moede-og-event':'/en/meetings-events','/biler-til-salg':'/en/cars-for-sale','/nyheder':'/en/news','/kontakt':'/en/contact'};
  path=aliases[path]||path;
  if(path==='/about'||path==='/en/about-my-garage')return '/home'+url.search+url.hash;
  if(['/home','/contact','/storage','/storage/garage-3d','/news','/booking','/parts-shop','/events','/partners'].includes(path))return path+url.search+url.hash;
  const routes = { '/en': '/home', '/en/contact': '/contact', '/en/storage': '/storage', '/en/news': '/news',
    '/en/meetings-events': '/events', '/en/partners': '/partners' };
  return (routes[path] || `/discover${path.replace(/^\/en/, '')}`) + url.search + url.hash;
}
