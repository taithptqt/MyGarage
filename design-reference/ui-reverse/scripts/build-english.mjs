import { acquire, pageDataFromHtml, root } from '../server.mjs';
import { labels, visitHtml } from '../english.mjs';
import { readFile, readdir, writeFile } from 'node:fs/promises';
import * as html from 'parse5';
import path from 'node:path';

const app = (await acquire('https://mygarage.dk/assets/js/app.339bcbe9.js')).body.toString();
const englishPaths = new Set();
for (const match of app.matchAll(/eval\(("(?:\\.|[^"\\])*")\);/g)) {
  const code = JSON.parse(match[1]);
  if (!code.includes('sourceURL=webpack:///./src/.temp/routes.js?')) continue;
  for (const route of code.matchAll(/path: "([^"]+)"/g)) if (route[1].startsWith('/en/')) englishPaths.add(route[1]);
}
const fetchFailures = [];
const routes = [...englishPaths];
await Promise.all(Array.from({length:4}, async()=>{
  while(routes.length) {
    const route=routes.shift();
    try { await acquire('https://mygarage.dk'+route,90000); }
    catch(error) { fetchFailures.push({route,error:error.message}); }
  }
}));
console.log(JSON.stringify({englishRoutes:englishPaths.size,fetchFailures:fetchFailures.length}));

let catalog = {};
try { catalog=JSON.parse(await readFile('english-content.json','utf8')); } catch {}
const candidates = new Set();
const danish = /(?:\b(?:og|til|fra|med|vores|din|dit|dine|jeres|du|vi|biler|bilen|bilens|huset|har|hos|ikke|eller|mere|denne|dette|opbevaring|opbevaret|kontakt|besked|priser|moms|afgift|udstyr|leasingforslag|indregistrering|finansiering|levering|hverdage|lukket|privatlivspolitik|samtykke|forespørgsel|vinduer|automatgear|sæder|dæk|fælge|køretøj|forsikring|brændstof)\b|[æøåÆØÅ])/i;
const skippedKeys = new Set(['id','uri','path','url','href','sourceUrl','src','__typename','value','lang','language','cssClass','className']);
function collect(text) {
  const key=text.trim();
  if (!key || labels[key]!==undefined || catalog[key]!==undefined || /^(https?:|data:|mailto:|tel:|\/)/.test(key)) return text;
  if (/^[\d\s+.,:/|%()–—-]+$/.test(key) || /(?:@|base64,|webpack|__typename)/.test(key)) return text;
  // Preserve personal/brand names, addresses and model names used as proper nouns.
  if (/^(?:Wittrupvej|Vejle Øst|Troels Kær|Søren |København$|AH Autolak$|Århus$)/.test(key)) return text;
  if (danish.test(key)) candidates.add(key);
  return text;
}
function collectData(value,key='') {
  if(typeof value==='string') {
    if(skippedKeys.has(key)) return;
    if(/<[a-z][\s\S]*>/i.test(value)) visitHtml(html.parseFragment(value),collect);
    else collect(value);
  } else if(Array.isArray(value)) value.forEach(item=>collectData(item,key));
  else if(value && typeof value==='object') for(const [k,v]of Object.entries(value)) collectData(v,k);
}
for(const file of await readdir(root)) {
  if(!file.endsWith('.json'))continue;
  const meta=JSON.parse(await readFile(path.join(root,file),'utf8'));
  if(!/html|json|javascript/.test(meta.type))continue;
  const text=await readFile(path.join(root,file.slice(0,-5)),'utf8');
  try {
    if(meta.type.includes('html')) {
      visitHtml(html.parse(text),collect);
      const state=pageDataFromHtml(text);if(state)collectData(state);
    } else if(meta.type.includes('javascript')) {
      for(const match of text.matchAll(/eval\(("(?:\\.|[^"\\])*")\);/g)) {
        const code=JSON.parse(match[1]);
        if(!code.includes('sourceURL=webpack:///./src/'))continue;
        for(const token of code.matchAll(/_vm\._v\(("(?:\\.|[^"\\])*")/g)) collect(JSON.parse(token[1]));
        for(const token of code.matchAll(/"(?:placeholder|aria-label|alt)":\s*("(?:\\.|[^"\\])*")/g)) collect(JSON.parse(token[1]));
      }
    } else collectData(JSON.parse(text));
  } catch(error) { console.log('Skipped non-page content: '+meta.url); }
}
console.log(JSON.stringify({stringsToTranslate:candidates.size,cachedTranslations:Object.keys(catalog).length}));
function chunks(text) {
  const result=[]; let offset=0;
  while(offset<text.length) {
    let end=Math.min(offset+2200,text.length);
    if(end<text.length) { const space=text.lastIndexOf(' ',end);if(space>offset+1000)end=space; }
    const value=text.slice(offset,end);
    result.push({text:value.trim(),prefix:value.match(/^\s*/)[0],suffix:value.match(/\s*$/)[0],translated:null});
    offset=end;
  }
  return result;
}
const tasks=[...candidates].map(source=>({source,chunks:chunks(source)}));
const jobs=tasks.flatMap(task=>task.chunks);
const batches=[];
while(jobs.length) {
  const batch=[];let length=0;
  while(jobs.length && batch.length<16) {
    const next=jobs[0], size=encodeURIComponent(next.text).length+3;
    if(batch.length && length+size>6500)break;
    batch.push(jobs.shift());length+=size;
  }
  batches.push(batch);
}
const failures=[];let completed=0;
await Promise.all(Array.from({length:3},async()=>{
  while(batches.length) {
    const batch=batches.shift();let error;
    for(let attempt=0;attempt<3;attempt++) {
      try {
        const url=new URL('https://translate.googleapis.com/translate_a/t');
        url.search=new URLSearchParams({client:'dict-chrome-ex',sl:'auto',tl:'en'});
        for(const job of batch)url.searchParams.append('q',job.text);
        const response=await fetch(url,{signal:AbortSignal.timeout(30000)});
        if(!response.ok)throw Error('Translation HTTP '+response.status);
        const values=await response.json();
        if(!Array.isArray(values)||values.length!==batch.length)throw Error('Invalid translation batch length');
        batch.forEach((job,index)=>job.translated=Array.isArray(values[index])?values[index][0]:values[index]);
        if(batch.some(job=>typeof job.translated!=='string'))throw Error('Invalid translation response');
        error=null;break;
      } catch(e){error=e;}
    }
    if(error)failures.push(error.message);
    completed+=batch.length;
    if(completed%100<16)console.log(JSON.stringify({translatedSegments:completed,queuedBatches:batches.length,errors:failures.length}));
  }
}));
for(const task of tasks)if(task.chunks.every(c=>typeof c.translated==='string'))catalog[task.source]=task.chunks.map(c=>c.prefix+c.translated+c.suffix).join('');
await writeFile('english-content.json',JSON.stringify(catalog,null,2));
await writeFile('english-report.json',JSON.stringify({englishRoutes:[...englishPaths],fetchFailures,translationFailures:failures,translations:Object.keys(catalog).length,untranslated:tasks.filter(t=>!catalog[t.source]).map(t=>t.source)},null,2));
console.log(JSON.stringify({translations:Object.keys(catalog).length,failedBatches:failures.length,untranslated:tasks.filter(t=>!catalog[t.source]).length}));
