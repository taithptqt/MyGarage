import fs from 'node:fs/promises';
import path from 'node:path';
import { createRequire } from 'node:module';
import { createHash } from 'node:crypto';
import { createWriteStream } from 'node:fs';
import { Readable } from 'node:stream';
import { pipeline } from 'node:stream/promises';

const {parseFragment,serialize}=createRequire(new URL('../Frontend/package.json',import.meta.url))('parse5');

const source=path.resolve(process.argv.slice(2).find(argument=>!argument.startsWith('--'))||'design-reference/ui-reverse');
const cache=path.join(source,'mirror-cache');
const translations=JSON.parse(await fs.readFile(path.join(source,'english-content.json'),'utf8'));
const text=value=>{const key=value.trim();return translations[key]===undefined?value:value.replace(key,translations[key])};
function localized(value,key=''){
  if(typeof value==='string'){
    if(['id','uri','path','url','href','sourceUrl','src','__typename','value','lang','language','cssClass','className'].includes(key)||/^(https?:|data:|mailto:|tel:|\/)/.test(value))return value;
    if(/<[a-z][\s\S]*>/i.test(value)){
      const tree=parseFragment(value);
      const visit=node=>{if(node.nodeName==='#text')node.value=text(node.value);for(const child of node.childNodes||[])visit(child)};
      visit(tree);return serialize(tree);
    }
    return text(value);
  }
  if(Array.isArray(value))return value.map(item=>localized(item,key));
  if(value&&typeof value==='object')return Object.fromEntries(Object.entries(value).map(([k,v])=>[k,localized(v,k)]));
  return value;
}
const pages={};
for(const name of await fs.readdir(cache)){
  if(!name.endsWith('.json'))continue;
  const meta=JSON.parse(await fs.readFile(path.join(cache,name),'utf8'));
  if(!meta.type?.startsWith('text/html')||!meta.url?.startsWith('https://mygarage.dk/en'))continue;
  const html=await fs.readFile(path.join(cache,name.slice(0,-5)),'utf8');
  const serialized=html.split('window.__INITIAL_STATE__=')[1]?.split(';(function()')[0];
  if(!serialized)continue;
  try{
    const state=JSON.parse(serialized);
    const route=new URL(meta.url).pathname.replace(/\/$/,'')||'/en';
    if(state.data&&Object.keys(state.data).length)pages[route]=localized(state.data);
  }catch{}
}
const output='Frontend/src/shared/brand';
if(process.argv.includes('--capture-missing')){
  const urls=new Set();
  const scan=value=>{if(typeof value==='string'&&/^https?:/.test(value)&&/\.(jpe?g|png|mp4|svg|webp)(\?|$)/i.test(value))urls.add(value);else if(Array.isArray(value))value.forEach(scan);else if(value&&typeof value==='object')Object.values(value).forEach(scan)};
  scan(pages);
  const failures=[];let done=0;
  const pending=[...urls];
  await Promise.all(Array.from({length:3},async()=>{
    while(pending.length){
      const url=pending.pop(),file=path.join(cache,createHash('sha256').update(url).digest('hex'));
      try{await fs.access(file);await fs.access(file+'.json');continue}catch{}
      try{
        if(!['mygarage.dk','mygarage.skaevt.com','billeder.bilinfo.net'].includes(new URL(url).hostname))throw Error('Source host outside reference site');
        const timeout=Number(process.env.UI_MEDIA_TIMEOUT_MS)||30000;
        let type;
        if(/\.mp4(?:\?|$)/i.test(url)){
          const info=await fetch(url,{method:'HEAD',signal:AbortSignal.timeout(timeout)});
          if(!info.ok)throw Error(`HTTP ${info.status}`);
          const size=Number(info.headers.get('content-length'));
          if(!Number.isFinite(size)||size<=0)throw Error('Video length unavailable');
          type=info.headers.get('content-type')||'video/mp4';
          const etag=info.headers.get('etag');
          await fs.writeFile(file+'.partial','');
          const chunk=4*1024*1024;
          for(let start=0;start<size;start+=chunk){
            const end=Math.min(size-1,start+chunk-1);
            const response=await fetch(url,{headers:{Range:`bytes=${start}-${end}`,...(etag?{'If-Range':etag}:{})},signal:AbortSignal.timeout(timeout)});
            if(response.status!==206||response.headers.get('content-range')!==`bytes ${start}-${end}/${size}`)throw Error('Unexpected source video range');
            await pipeline(Readable.fromWeb(response.body),createWriteStream(file+'.partial',{flags:'a'}));
          }
          if((await fs.stat(file+'.partial')).size!==size)throw Error('Incomplete source video');
          console.log(`Video captured: ${new URL(url).pathname.split('/').at(-1)} (${Math.round(size/1024/1024)} MiB)`);
        }else{
          const response=await fetch(url,{signal:AbortSignal.timeout(timeout)});
          if(!response.ok)throw Error(`HTTP ${response.status}`);
          type=response.headers.get('content-type')||'application/octet-stream';
          await pipeline(Readable.fromWeb(response.body),createWriteStream(file+'.partial'));
        }
        await fs.rename(file+'.partial',file);
        const {size}=await fs.stat(file);
        await fs.writeFile(file+'.json',JSON.stringify({url,type,bytes:size}));
        if(++done%50===0)console.log(`Additional media captured: ${done}`);
      }catch(error){await fs.rm(file+'.partial',{force:true});failures.push({url,error:error.message})}
    }
  }));
  await fs.mkdir('.dev-runtime',{recursive:true});
  await fs.writeFile('.dev-runtime/ui-media-missing.json',JSON.stringify(failures,null,2));
  console.log(`Additional media: ${done} captured, ${failures.length} unavailable.`);
}
await fs.mkdir(output,{recursive:true});
await fs.writeFile(path.join(output,'reference-pages.json'),JSON.stringify(pages,null,2)+'\n');
await fs.writeFile(path.join(output,'reference-home.json'),JSON.stringify(pages['/en']?.page||{},null,2)+'\n');
const stories=Object.entries(pages).filter(([,data])=>data.post).map(([route,{post}])=>({route,title:post.title,
  image:post.acf_post?.image?.[0],excerpt:post.acf_post?.content?.replace(/<[^>]+>/g,' ').replace(/\s+/g,' ').trim().slice(0,180)}));
await fs.writeFile(path.join(output,'reference-stories.json'),JSON.stringify(stories,null,2)+'\n');
const cars=Object.entries(pages).filter(([,data])=>data.car);
const featured=['Aston Martin','Porsche','Lotus','Mercedes'].map(make=>cars.find(([,data])=>data.car.make===make)).filter(Boolean)
  .map(([route,{car}])=>({route,title:`${car.make} ${car.model}`.toUpperCase(),tag:'',subtitle:car.variant,year:car.year,mileage:Number(car.mileage||0).toLocaleString('vi-VN'),image:car.images?.[0]}));
await fs.writeFile(path.join(output,'reference-cars.json'),JSON.stringify(featured,null,2)+'\n');
console.log(`Prepared ${Object.keys(pages).length} reference routes as React-readable content.`);
console.log('Data roots:',[...new Set(Object.values(pages).flatMap(Object.keys))].join(', '));
