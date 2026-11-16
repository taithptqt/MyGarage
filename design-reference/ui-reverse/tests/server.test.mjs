import test, { before, after } from 'node:test';
import assert from 'node:assert/strict';
import { once } from 'node:events';
import { readFile, stat } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { start, parseRange, acquire, pageDataFromHtml, htmlClean, root } from '../server.mjs';
import { storageForm, localForms } from '../forms.mjs';
import { patchBuild } from '../build-patches.mjs';
import { englishData, englishRoute, englishHtml } from '../english.mjs';
let server, base;
before(async () => {
  process.env.OFFLINE = '1'; process.env.PORT = '0';
  server = start(); await once(server, 'listening');
  base = `http://127.0.0.1:${server.address().port}`;
});
after(async()=>{server.closeAllConnections();await new Promise(resolve=>server.close(resolve));});

test('Storage schema has the original required fields and all 14 service options',()=>{
  assert.equal(storageForm.fields.filter(f=>f.type==='checkbox').length,14);
  assert.deepEqual(storageForm.fields.filter(f=>f.isRequired).map(f=>f.label),['Mail','Telefon','Bilens mærke','Model','Årgang']);
  assert.ok(storageForm.fields.filter(f=>f.type==='checkbox').every(f=>f.inputs[0].label));
});
test('SPA JSON matches the HTML snapshot hash and data, without upstream access',async()=>{
  for(const route of ['/', '/storage/', '/sales/', '/faq/', '/biler-til-salg/']) {
    const html=(await acquire('https://mygarage.dk'+englishRoute(route),1000,true)).body.toString();
    const expected=pageDataFromHtml(html);
    const response=await fetch(base+'/assets/data'+(route==='/'?'/':route)+'index.json');
    assert.equal(response.status,200,route);
    const actual=await response.json();
    assert.equal(actual.hash,expected.hash,route);
    assert.deepEqual(actual.context,{...expected.context,lang:'EN'},route);
    assert.ok(actual.data);
  }
});
test('Storage API works offline and does not require Authorization',async()=>{
  const response=await fetch(base+'/_remote/mygarage.skaevt.com/wp-json/gf/v2/forms/8');
  assert.equal(response.status,200); assert.deepEqual(await response.json(),englishData(storageForm));
});
test('All 12 public dynamic forms have local schemas, including brand dropdown choices',async()=>{
  assert.equal(Object.keys(localForms).length,12);
  for(const [id,form] of Object.entries(localForms)) {
    const response=await fetch(base+'/_remote/mygarage.skaevt.com/wp-json/gf/v2/forms/'+id);
    assert.equal(response.status,200,id); assert.deepEqual(await response.json(),englishData(form));
    assert.ok(form.fields.every(f=>f.formId===Number(id)));
    for(const field of form.fields.filter(f=>f.type==='select')) assert.ok(field.choices.length>0);
  }
});
test('No forced navigation guard; forms and cookie UI are local',async()=>{
  const response=await fetch(base+'/local-ui.js'); const script=await response.text();
  assert.ok(!script.includes('location.href='));
  assert.ok(script.includes("addEventListener('submit'"));
  assert.ok(script.includes('localStorage.setItem'));
  const clean=htmlClean('<head><script id="Cookiebot" src="https://consent.cookiebot.com/uc.js"></script></head>');
  assert.ok(!clean.includes('consent.cookiebot.com'));
  assert.ok(clean.includes('/local-ui.js'));
});
test('Video range parsing supports seek, suffix and invalid ranges',()=>{
  assert.deepEqual(parseRange('bytes=0-15',100),{from:0,to:15});
  assert.deepEqual(parseRange('bytes=-16',100),{from:84,to:99});
  assert.deepEqual(parseRange('bytes=90-',100),{from:90,to:99});
  assert.equal(parseRange('bytes=100-',100),false);
  assert.equal(parseRange('bytes=8-4',100),false);
  assert.equal(parseRange('bytes=0-1,4-5',100),false);
});
test('949MB Lotus video is complete in cache and streamed with HTTP range/HEAD',async()=>{
  const url='https://mygarage.skaevt.com/wp-content/uploads/2025/01/Lotus-T132-Studio-B-Roll-Yellow.mp4';
  const key=createHash('sha256').update(url).digest('hex');
  const info=await stat(`${root}/${key}`);
  assert.equal(info.size,948975781);
  const endpoint=base+'/_remote/mygarage.skaevt.com/wp-content/uploads/2025/01/Lotus-T132-Studio-B-Roll-Yellow.mp4';
  const chunk=await fetch(endpoint,{headers:{Range:'bytes=0-15'}});
  assert.equal(chunk.status,206); assert.equal((await chunk.arrayBuffer()).byteLength,16);
  assert.equal(chunk.headers.get('content-range'),'bytes 0-15/948975781');
  const suffix=await fetch(endpoint,{headers:{Range:'bytes=-16'}});
  assert.equal(suffix.status,206);assert.equal((await suffix.arrayBuffer()).byteLength,16);
  const invalid=await fetch(endpoint,{headers:{Range:'bytes=948975781-'}});
  assert.equal(invalid.status,416);
  const head=await fetch(endpoint,{method:'HEAD'});
  assert.equal(head.headers.get('content-length'),'948975781');assert.equal((await head.arrayBuffer()).byteLength,0);
});
test('Known upstream 404 keeps a branded HTML page and HTTP 404 status',async()=>{
  const response=await fetch(base+'/mclaren-til-salg/');
  assert.equal(response.status,404);
  assert.ok((await response.text()).includes('id="app"'));
});
test('POST stays local and never reaches the company backend',async()=>{
  const response=await fetch(base+'/_remote/mygarage.skaevt.com/wp-json/gf/v2/forms/8/submissions',{method:'POST',body:'test'});
  assert.equal(response.status,405);
});
test('Published scroll component is null-safe and removes its listener on destroy',async()=>{
  const app=(await acquire('https://mygarage.dk/assets/js/app.339bcbe9.js',1000,true)).body.toString();
  const patched=patchBuild(app);
  const modules=[...patched.matchAll(/eval\(("(?:\\.|[^"\\])*")\);/g)].map(m=>JSON.parse(m[1]));
  const scroll=modules.find(m=>m.includes('handleScroll()')&&m.includes('sourceURL=webpack:///./src/components/ScrollIndicator.vue?'));
  assert.ok(scroll.includes('if (indicator)'));
  assert.ok(scroll.includes('beforeDestroy() { window.removeEventListener'));
  const main=modules.find(m=>m.includes('sourceURL=webpack:///./src/main.js?'));
  assert.ok(main.includes('Analytics disabled in local preview'));
  assert.ok(main.includes('.defaultLocale = "en"'));
  assert.ok(!main.includes('Vue.use(vue_gtag__'));
});
test('English is the default for homepage and legacy navigation URLs',async()=>{
  for(const [oldPath,newPath] of [['/','/en/'],['/storage/','/en/storage/'],['/sales/','/en/sales/']]) {
    const response=await fetch(base+oldPath,{redirect:'manual'});
    assert.equal(response.status,302);assert.equal(response.headers.get('location'),newPath);
  }
});
test('English HTML and client state agree, including vehicle descriptions and metadata',async()=>{
  const response=await fetch(base+'/en/storage/');
  const content=await response.text();
  assert.match(content,/<html[^>]*lang="en"/);
  assert.ok(content.includes('At MY GARAGE Storage'));
  const state=pageDataFromHtml(content);
  assert.equal(state.context.lang,'EN');
  assert.ok(!state.data.page.seo.metaDesc.includes('Hos MY GARAGE'));
  const car=await fetch(base+'/assets/data/en/cars/porsche-macan-4-768053987/index.json');
  const data=(await car.json()).data.car;
  assert.ok(data.carComment.includes('Sporty elegance and exclusive comfort'));
  assert.ok(!data.carComment.includes('En særdeles veludstyret'));
  assert.equal(data.propellant,'Electric');assert.equal(data.type,'Passenger car');
});
test('All local form labels, choices and buttons are English while values remain stable',()=>{
  const forms=englishData(localForms);
  assert.equal(forms[8].button.text,'Send enquiry');
  assert.deepEqual(forms[8].fields.filter(f=>f.isRequired).map(f=>f.label),['Email','Phone','Car make','Model','Year']);
  assert.equal(forms[8].fields.find(f=>f.id===15).inputs[0].label,'Charger installation DKK 195');
  assert.deepEqual(forms[13].fields.find(f=>f.id===8).choices[1],{text:'Copenhagen',value:'København'});
});
test('Translated inline data is escaped and dynamic popup wording is localized',()=>{
  const fragment=englishHtml('<html lang="da"><head></head><body><script>window.__INITIAL_STATE__={};(function(){})();</script></body></html>',{data:{text:'<p>Content</p>'},context:{lang:'DA'}});
  assert.ok(fragment.includes('\\u003cp>'));
  const source='eval('+JSON.stringify('var text = _vm._v("Forespørg på " + title + " pakken");\n//# sourceURL=webpack:///./src/components/ModalForm.vue?')+');';
  const patched=patchBuild(source);
  assert.ok(patched.includes('Enquire about'));
  assert.ok(!patched.includes('Forespørg'));
});
