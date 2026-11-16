import { acquire, cacheResource, saveResource, pageDataFromHtml, root } from './server.mjs';
import { readFile, readdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
const repair = process.argv.includes('--repair');
const cacheOnly = process.env.CAPTURE_CACHE_ONLY === '1';
const limit = Number(process.env.MAX_PAGES || Infinity);
const queue = [], seen = new Set(), cached = new Set(), failed = [], deferred = [], downloaded = [], upstreamNotFound = [];
let pages = 0, processed = 0, routeData = 0;
const metadata = [];
for (const file of await readdir(root)) {
  if (!file.endsWith('.json')) continue;
  const meta = JSON.parse(await readFile(path.join(root, file), 'utf8'));
  cached.add(meta.url); metadata.push(meta);
}
function normalize(raw, base) {
  raw = raw.replaceAll('\\u002F', '/').replaceAll('\\/', '/').replaceAll('&amp;', '&');
  if (/[{}<>`]/.test(raw) || raw.includes('${')) return null;
  if (raw.startsWith('./fav/')) raw = '/fav/' + raw.slice(6);
  const url = new URL(raw, base); url.hash = '';
  if (!['mygarage.dk','mygarage.skaevt.com','billeder.bilinfo.net'].includes(url.hostname)) return null;
  if (/\/fav\//.test(url.pathname) || /\/wp-json\/|graphql/.test(url.pathname) || /\.(php|xml)$/.test(url.pathname)) return null;
  return url;
}
function add(raw, base) {
  try {
    const u = normalize(raw, base); if (!u) return;
    const media = /\.(?:js|css|json|png|jpe?g|webp|svg|gif|mp4|webm|woff2?|ttf|otf|ico)$/i.test(u.pathname);
    const page = u.hostname === 'mygarage.dk' && !path.posix.extname(u.pathname) && !u.pathname.startsWith('/assets/');
    if (!media && !page || seen.has(u.href)) return;
    if (page) {
      u.search = '';
      if (seen.has(u.href) || pages >= limit) return;
      if (repair && !cached.has(u.href) && u.pathname !== '/mclaren-til-salg/') return;
      pages++;
    }
    if (media && cached.has(u.href) && !/\.(js|css|json)$/i.test(u.pathname)) { seen.add(u.href); return; }
    seen.add(u.href); queue.push(u.href);
  } catch {}
}
for (const meta of metadata) if (/html|css|javascript/.test(meta.type)) add(meta.url, meta.url);
try {
  const old = JSON.parse(await readFile('capture-report.json', 'utf8'));
  for (const url of [...(old.deferred || []), ...(old.failures?.map(x=>x.url) || [])]) add(url, 'https://mygarage.dk/');
} catch {}
add('https://mygarage.dk/', 'https://mygarage.dk/');
async function processUrl(url) {
  try {
    const binary = /\.(?:mp4|webm|jpe?g|png|webp|gif|ico|woff2?|ttf|otf)$/i.test(new URL(url).pathname);
    const asset = binary ? await cacheResource(url, 600000, cacheOnly) : await acquire(url, 120000, cacheOnly);
    if (asset.status === 404) upstreamNotFound.push(url);
    if (!cached.has(url)) { downloaded.push({url, bytes:asset.bytes}); cached.add(url); }
    if (/html|css|javascript|json/.test(asset.type)) {
      const text = asset.body.toString().replaceAll('\\u002F', '/').replaceAll('\\/', '/');
      if (asset.type.includes('html')) {
        const data = pageDataFromHtml(asset.body.toString());
        if (data) {
          const route = new URL(url).pathname.replace(/\/$/, '');
          await saveResource(`https://mygarage.dk/assets/data${route}/index.json`, Buffer.from(JSON.stringify(data)), 'application/json', { generatedFrom: url });
          routeData++;
        }
      }
      for (const m of text.matchAll(/(?:src|href|poster|data-src)=["']([^"']+)["']/g)) add(m[1], url);
      for (const m of text.matchAll(/https?:\/\/[^\s"'<>\\)]+/g)) add(m[0], url);
      for (const m of text.matchAll(/url\(["']?([^)'"\s]+)["']?\)/g)) add(m[1], url);
      for (const m of text.matchAll(/["'](\/assets\/[^"'\s]+)["']/g)) add(m[1], url);
    }
  } catch(error) {
    if (error.message === 'UNCACHED') deferred.push(url);
    else failed.push({url,error:error.message});
  }
  processed++;
  if (processed % 100 === 0) console.log(JSON.stringify({processed, queued:queue.length, newDownloads:downloaded.length, failed:failed.length}));
}
await Promise.all(Array.from({length:4}, async()=>{while(queue.length) await processUrl(queue.shift());}));
const report = { mode:repair?'repair':'full', processed, discoveredPages:pages, routeData, downloaded, failures:failed, deferred, upstreamNotFound,
  exhaustiveOfflineArchive:false, note:'Covers discovered public file URLs; dynamic UI states still require browser verification.' };
await writeFile(repair?'repair-report.json':'capture-report.json', JSON.stringify(report,null,2));
console.log(JSON.stringify({processed,pages,routeData,newDownloads:downloaded.length,failed:failed.length,deferred:deferred.length}));
