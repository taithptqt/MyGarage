import test, {before,after} from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {MemoryRouter} from 'react-router-dom';
import {JSDOM} from 'jsdom';
import {createServer} from 'vite';

let server,brand,pageModule,AuthProvider,HomePage,pages,manifest,dom;
const originalError=console.error;
before(async()=>{
  console.error=(message,...rest)=>{if(typeof message==='string'&&message.includes('useLayoutEffect does nothing on the server'))return;originalError(message,...rest)};
  dom=new JSDOM('');globalThis.DOMParser=dom.window.DOMParser;
  server=await createServer({server:{middlewareMode:true},appType:'custom',logLevel:'error'});
  brand=await server.ssrLoadModule('/src/shared/brand/media.js');
  pageModule=await server.ssrLoadModule('/src/shared/brand/ReferencePage.jsx');
  ({AuthProvider}=await server.ssrLoadModule('/src/shared/core/context/AuthContext.jsx'));
  ({default:HomePage}=await server.ssrLoadModule('/src/features/home/pages/HomePage.jsx'));
  pages=JSON.parse(await fs.readFile('src/shared/brand/reference-pages.json','utf8'));
  manifest=JSON.parse(await fs.readFile('src/shared/brand/media-manifest.json','utf8'));
});
after(async()=>{await server?.close();dom?.window.close();delete globalThis.DOMParser;console.error=originalError});
const render=(Component,props,path='/about')=>renderToStaticMarkup(React.createElement(MemoryRouter,{initialEntries:[path]},
  React.createElement(AuthProvider,null,React.createElement(Component,props))));

test('source navigation targets React routes while booking/customer routes remain intact',()=>{
  for(const [source,target]of [['/en/','/home'],['/storage','/storage'],['/en/storage/','/storage'],['/en/contact/','/contact'],['/en/about-my-garage/','/home'],['/en/partners/carglass/','/discover/partners/carglass'],['/en/news/story/','/discover/news/story'],['/booking','/booking']])assert.equal(brand.referenceLink(source),target);
  assert.equal(brand.referenceLink('mailto:hello@example.com'),'mailto:hello@example.com');
  assert.equal(brand.referenceLink('javascript:alert(1)'),'/contact');
  assert.equal(brand.referenceLink('https://example.com/page'),'https://example.com/page');
});

test('the public brand theme is disabled for all admin and staff workspaces',async()=>{
  const {isPublicRoute}=await server.ssrLoadModule('/src/shared/brand/PublicBrandTheme.jsx');
  for(const path of ['/login','/admin/default','/Admin/gate','/admin-legacy','/accounting/dashboard','/workshop/reception','/dispatch-center'])assert.equal(isPublicRoute(path),false,path);
  for(const path of ['/home','/booking','/parts-shop','/my-vehicles','/my-garage-dashboard','/my-appointments','/my-retail-orders','/discover/cars/lotus'])assert.equal(isPublicRoute(path),true,path);
});

test('imported media resolves to public Azure URLs without signed credentials',()=>{
  assert.ok(Object.keys(manifest).length>=2377);
  for(const target of Object.values(manifest)){const url=new URL(target);assert.equal(url.protocol,'https:');assert.match(url.hostname,/\.blob\.core\.windows\.net$/);assert.equal(url.search,'')}
  assert.equal(brand.mediaUrl({sourceUrl:'https://mygarage.skaevt.com/wp-content/uploads/2024/10/Website_banner_01_35MB.mp4'}),manifest['https://mygarage.skaevt.com/wp-content/uploads/2024/10/Website_banner_01_35MB.mp4']);
  const inspect=value=>{
    if(typeof value==='string'&&/^https?:/.test(value)&&/\.(jpe?g|png|mp4|svg|webp)(\?|$)/i.test(value))assert.ok(manifest[value],`Missing cloud resource: ${value}`);
    if(Array.isArray(value))value.forEach(inspect);else if(value&&typeof value==='object')Object.values(value).forEach(inspect);
  };
  inspect(pages);
});

test('imported rich text drops executable markup and rewrites internal links and media',()=>{
  const markup=pageModule.safeMarkup('<script>alert(1)</script><iframe src="https://evil.example"></iframe><p onclick="alert(1)">Hello <a href="/en/storage/">Storage</a><a href="javascript:alert(1)">bad</a><img src="/assets/booking/hero.jpg" onerror="alert(1)"></p>');
  const document=new DOMParser().parseFromString(markup,'text/html');
  assert.equal(document.querySelector('script,iframe,[onclick],[onerror]'),null);
  assert.equal(document.querySelector('a').getAttribute('href'),'/storage');
  assert.equal(document.querySelectorAll('a')[1].getAttribute('href'),null);
  assert.equal(document.querySelector('img').getAttribute('src'),manifest['/assets/booking/hero.jpg']);
});

test('home renders the original front page and existing account/menu controls',()=>{
  const html=render(HomePage,{},'/home'),document=new DOMParser().parseFromString(html,'text/html');
  assert.match(document.querySelector('video').src,/\.blob\.core\.windows\.net/);
  assert.equal(document.querySelectorAll('.reference-partners').length,2);
  assert.ok(document.querySelector('.reference-partners + .home-founder'));
  assert.equal(document.querySelectorAll('.home-founder video[autoplay][muted][loop]').length,3);
  assert.ok(document.querySelector('.home-founder-signature'));
  assert.equal(document.querySelectorAll('.reference-split').length,4);
  assert.equal(document.querySelector('.reference-split h2').textContent,'Đam mê của chúng tôi');
  assert.equal(document.body.textContent.includes('Hội họp & Sự kiện'),false);
  assert.equal(document.querySelectorAll('.reference-car-grid a').length,4);
  assert.equal(document.querySelector('iframe[src^="https://player.vimeo.com/video/"]'),null);
  assert.equal(document.body.textContent.includes('Se filmen om MY GARAGE:'),false);
  assert.ok(document.body.textContent.includes('Những chiếc xe nổi bật:'));
  assert.equal(document.querySelector('.mg-header-menu-toggle').getAttribute('aria-expanded'),'false');
  assert.ok(document.querySelector('a[href="/login"]'));
  for(const path of ['/events','/partners','/booking','/parts-shop'])assert.ok(document.querySelector(`.mg-header-wrap a[href="${path}"]`));
  for(const image of document.querySelectorAll('img'))assert.match(image.src,/\.blob\.core\.windows\.net/);
});

test('car detail uses the source gallery, specification and dealer layout',()=>{
  const sourcePath='/en/cars/lotus-emeya-1690364891';
  const html=render(pageModule.default,{sourcePath},brand.referenceLink(sourcePath));
  const document=new DOMParser().parseFromString(html,'text/html');
  assert.ok(document.querySelector('.car-detail-top .car-detail-gallery'));
  assert.equal(document.querySelectorAll('.car-detail-thumbnails button').length,pages[sourcePath].car.images.length);
  assert.equal(document.querySelectorAll('.car-detail-columns > div').length,2);
  assert.ok(document.querySelector('.car-detail-columns dl dt'));
  assert.ok(document.querySelector('a[href="#car-inquiry"]'));
  assert.ok(document.querySelector('.car-detail-dealer a[href^="tel:"]'));
  const description=document.querySelector('.car-detail-description p').textContent;
  assert.ok(description.length>100);
  assert.notEqual(description,pages[sourcePath].car.carComment);
  assert.ok(document.querySelector('.car-detail-columns').textContent.includes('Điện'));
});

test('Storage links to the dedicated 3D garage and the page exposes accessible controls',async()=>{
  const {default:StoragePage}=await server.ssrLoadModule('/src/features/storage/pages/StorageServicesPage.jsx');
  const storage=new DOMParser().parseFromString(render(StoragePage,{},'/storage'),'text/html');
  assert.ok(storage.querySelector('a[href="/storage/garage-3d"]'));
  assert.equal(brand.referenceLink('/storage/garage-3d'),'/storage/garage-3d');
  const {default:GaragePage}=await server.ssrLoadModule('/src/features/storage/garage3d/Garage3DPage.jsx');
  const document=new DOMParser().parseFromString(render(GaragePage,{},'/storage/garage-3d'),'text/html');
  assert.equal(document.querySelectorAll('.garage3d-bays button').length,6);
  assert.equal(document.querySelectorAll('.garage3d-views button').length,7);
  assert.equal(document.querySelectorAll('.garage3d-paints button').length,7);
  assert.equal(document.querySelectorAll('.garage3d-custom-selection li').length,0);
  assert.equal(document.querySelectorAll('.garage3d-custom-tabs button').length,6);
  assert.ok(document.querySelector('.garage3d-custom-send').textContent.includes('Gửi lựa chọn cho quản lý'));
  assert.ok(!document.querySelector('.garage3d-customizer').textContent.includes('Tổng minh họa'));
  assert.ok(!document.querySelector('.garage3d-customizer').textContent.includes('₫'));
  assert.ok(document.querySelector('[role="status"]'));
  assert.ok(document.querySelector('a[href="https://creativecommons.org/licenses/by/4.0/"]'));
  const glb=await fs.readFile('public/models/vinfast-lux-a20/model.glb');
  assert.equal(glb.toString('ascii',0,4),'glTF');
  assert.ok(glb.length<12*1024*1024);
});

test('every captured content page and car renders in React without the Vue runtime',()=>{
  assert.ok(Object.keys(pages).length>=260);
  for(const sourcePath of Object.keys(pages)){
    assert.doesNotThrow(()=>{
      const html=render(pageModule.default,{sourcePath},brand.referenceLink(sourcePath));
      assert.ok(html.includes('reference-page'));
      assert.ok(!html.includes('__INITIAL_STATE__'));
      assert.ok(!html.includes('NaN DKK'));
      const document=new DOMParser().parseFromString(html,'text/html');
      for(const media of document.querySelectorAll('img[src],video[src],video source[src]'))assert.match(media.getAttribute('src'),/^https:\/\/[^/]+\.blob\.core\.windows\.net\//,`${sourcePath}: ${media.getAttribute('src')}`);
    },sourcePath);
  }
});
