import http from 'node:http';
import { mkdir, readFile, writeFile, stat, rename, unlink } from 'node:fs/promises';
import { createReadStream, createWriteStream } from 'node:fs';
import { pipeline } from 'node:stream/promises';
import { Readable } from 'node:stream';
import { createHash } from 'node:crypto';
import path from 'node:path';
import { localForms } from './forms.mjs';
import { patchBuild } from './build-patches.mjs';
import { englishRoute, englishData, englishHtml, labels } from './english.mjs';
const englishForms = englishData(localForms);
export const root = path.resolve('mirror-cache');
const origin = 'https://mygarage.dk';
const pending = new Map();
export const records = new Map();
const hosts = ['mygarage.dk', 'mygarage.skaevt.com', 'billeder.bilinfo.net'];
const downloads = new Map();
export async function cacheResource(url, timeout = 600000, offline = false) {
  if (downloads.has(url)) return downloads.get(url);
  const task = (async () => {
    const file = path.join(root, createHash('sha256').update(url).digest('hex'));
    try {
      const meta = JSON.parse(await readFile(file + '.json', 'utf8'));
      if ((await stat(file)).size === meta.bytes) return { ...meta, file };
    } catch {}
    if (offline || process.env.OFFLINE === '1') throw Error('UNCACHED');
    await mkdir(root, { recursive: true });
    const response = await fetch(url, { signal: AbortSignal.timeout(timeout) });
    if (!response.ok) { await response.body?.cancel(); throw Error(`${response.status} ${url}`); }
    const partial = `${file}.${process.pid}.partial`;
    try {
      await pipeline(Readable.fromWeb(response.body), createWriteStream(partial));
      const meta = { url, type: response.headers.get('content-type') || 'application/octet-stream', bytes: (await stat(partial)).size };
      await rename(partial, file);
      await writeFile(file + '.json', JSON.stringify(meta)); records.set(url, meta);
      return { ...meta, file };
    } catch(error) { await unlink(partial).catch(()=>{}); throw error; }
  })();
  downloads.set(url, task);
  try { return await task; } finally { downloads.delete(url); }
}
export function parseRange(value, size) {
  if (!value) return null;
  const match = /^bytes=(\d*)-(\d*)$/.exec(value);
  if (!match || !match[1] && !match[2]) return false;
  const from = match[1] ? Number(match[1]) : Math.max(0, size - Number(match[2]));
  const to = match[1] && match[2] ? Math.min(Number(match[2]), size - 1) : size - 1;
  return from >= size || from > to || to < 0 ? false : { from, to };
}
export async function saveResource(url, body, type, extra = {}) {
  const file = path.join(root, createHash('sha256').update(url).digest('hex'));
  const meta = { url, type, bytes: body.length, ...extra };
  await mkdir(root, { recursive: true });
  await writeFile(file, body); await writeFile(file + '.json', JSON.stringify(meta));
  records.set(url, meta);
  return { ...meta, body };
}
export function pageDataFromHtml(html) {
  const serialized = html.split('window.__INITIAL_STATE__=')[1]?.split(';(function()')[0];
  const hash = html.match(/name="gridsome:hash" content="([^"]+)"/)?.[1];
  if (!serialized || !hash) return null;
  return { hash, ...JSON.parse(serialized) };
}
export function rewrite(text) {
  for (const host of hosts) {
    const local = host === 'mygarage.dk' ? '' : '/_remote/' + host;
    text = text.replaceAll('https://' + host, local).replaceAll('http://' + host, local)
      .replaceAll('https:\\u002F\\u002F' + host, local.replaceAll('/', '\\u002F'))
      .replaceAll('https:\\/\\/' + host, local.replaceAll('/', '\\/'));
  }
  return text;
}
export async function acquire(url, timeout = 90000, offline = false) {
  if (pending.has(url)) return pending.get(url);
  const work = (async () => {
    const key = createHash('sha256').update(url).digest('hex');
    const file = path.join(root, key);
    try { const meta = JSON.parse(await readFile(file + '.json', 'utf8')); const body = await readFile(file); records.set(url, meta); return { ...meta, body }; } catch {}
    if (offline || process.env.OFFLINE === '1') throw Error('UNCACHED');
    const res = await fetch(url, { signal: AbortSignal.timeout(timeout) });
    const type = res.headers.get('content-type') || 'application/octet-stream';
    if (!res.ok && !(res.status === 404 && type.includes('text/html'))) {
      await res.body?.cancel();
      throw Error(`${res.status} ${url}`);
    }
    let body = Buffer.from(await res.arrayBuffer());
    return saveResource(url, body, type, res.status === 404 ? { status: 404 } : {});
  })();
  pending.set(url, work);
  try { return await work; } finally { pending.delete(url); }
}
export function htmlClean(text) {
  text = text.replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, (s) => /dataLayer|googletagmanager|Cookiebot|hotjar|fbevents|bqjbjpgu/.test(s) && !/__INITIAL_STATE__/.test(s) ? '' : s);
  return text.replace(/href="\.\/fav\/[^"<>]+"/g, 'href="/assets/static/fav.ce0531f.e5bad34a8a7153a7443299e157efff2e.png"')
    .replace('</head>', '<link rel="stylesheet" href="/local-ui.css"><script src="/local-ui.js" defer></script></head>');
}
export function start() {
  const server = http.createServer(async (req, res) => {
    try {
      if (!['GET', 'HEAD'].includes(req.method)) { res.writeHead(405); return res.end('Local UI preview: submissions disabled.'); }
      if (['/local-ui.js', '/local-ui.css'].includes(req.url)) {
        res.setHeader('Content-Type', req.url.endsWith('.js') ? 'text/javascript' : 'text/css');
        res.setHeader('Cache-Control', 'no-store');
        const file = await readFile(path.join('public', req.url.slice(1)));
        return res.end(req.url.endsWith('.js') ? `window.__ENGLISH_LABELS__=${JSON.stringify(labels)};\n${file}` : file);
      }
      const incoming = new URL(req.url, 'http://localhost');
      const localized = englishRoute(incoming.pathname);
      if (localized !== incoming.pathname || incoming.pathname === '/en') {
        res.writeHead(302, {Location:(incoming.pathname === '/en' ? '/en/' : localized) + incoming.search});
        return res.end();
      }
      const formId = incoming.pathname.match(/^\/_remote\/mygarage\.skaevt\.com\/wp-json\/gf\/v2\/forms\/(\d+)$/)?.[1];
      if (formId && englishForms[formId]) {
        res.setHeader('Content-Type', 'application/json');
        return res.end(JSON.stringify(englishForms[formId]));
      }
      if ((incoming.pathname.startsWith('/assets/data/') && incoming.pathname.endsWith('/index.json')) || incoming.pathname === '/assets/data/index.json') {
        const route = incoming.pathname.slice('/assets/data'.length).replace(/index\.json$/, '');
        const page = await acquire(origin + englishRoute(route));
        const data = pageDataFromHtml(page.body.toString());
        if (data) {
          res.setHeader('Content-Type', 'application/json');
          return res.end(rewrite(JSON.stringify(englishData(data))));
        }
      }
      let target = origin + incoming.pathname + incoming.search;
      if (incoming.pathname.startsWith('/_remote/')) {
        const [, , host, ...parts] = incoming.pathname.split('/');
        if (!hosts.includes(host)) { res.writeHead(403); return res.end(); }
        target = 'https://' + host + '/' + parts.join('/') + incoming.search;
      }
      if (/\.(?:mp4|webm|jpe?g|png|webp|gif|ico|woff2?|ttf|otf)$/i.test(new URL(target).pathname)) {
        const asset = await cacheResource(target);
        const range = parseRange(req.headers.range, asset.bytes);
        res.setHeader('Content-Type', asset.type);
        res.setHeader('Accept-Ranges', 'bytes');
        if (range === false) { res.writeHead(416, {'Content-Range': `bytes */${asset.bytes}`}); return res.end(); }
        const start = range?.from || 0, end = range?.to ?? asset.bytes - 1;
        res.setHeader('Content-Length', end - start + 1);
        if (range) res.writeHead(206, {'Content-Range': `bytes ${start}-${end}/${asset.bytes}`});
        if (req.method === 'HEAD') return res.end();
        const stream = createReadStream(asset.file, {start,end});
        res.on('close', ()=>stream.destroy()); stream.on('error', ()=>res.destroy());
        return stream.pipe(res);
      }
      const asset = await acquire(target);
      res.statusCode = asset.status || 200;
      let body = asset.body;
      if (/text|javascript|json|svg/.test(asset.type)) {
        res.setHeader('Cache-Control', 'no-cache');
        let text = body.toString();
        if (asset.type.includes('text/html')) {
          const state = pageDataFromHtml(text);
          text = englishHtml(text, state ? {data:state.data,context:state.context} : null);
        } else if (asset.type.includes('json')) {
          try { text = JSON.stringify(englishData(JSON.parse(text))); } catch {}
        }
        text = rewrite(text);
        if (/\/assets\/js\/app\.[^/]+\.js$/.test(incoming.pathname)) text = patchBuild(text);
        if (asset.type.includes('text/html')) text = htmlClean(text);
        body = Buffer.from(text);
      }
      res.setHeader('Content-Type', asset.type); res.setHeader('Accept-Ranges', 'bytes');
      res.setHeader('Content-Length', body.length); res.end(req.method === 'HEAD' ? undefined : body);
    } catch (e) { console.error(e.message); res.writeHead(502); res.end(e.message); }
  });
  server.listen(Number(process.env.PORT || 4173), '0.0.0.0', () => console.log(`MY GARAGE preview: http://localhost:${server.address().port}`));
  return server;
}
if (process.argv[1]?.endsWith('server.mjs')) start();
