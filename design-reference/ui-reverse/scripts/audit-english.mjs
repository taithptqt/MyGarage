import { readdir, readFile, writeFile } from 'node:fs/promises';
import { root, pageDataFromHtml } from '../server.mjs';
import { englishData } from '../english.mjs';
const findings = new Map(); let pages=0;
const ignoreKeys = new Set(['id','uri','path','url','href','sourceUrl','src','value','lang','language','dealerAddressCity','dealerAddressStreetLine1','dealerName','make','model','__typename']);
const residual = /\b(?:opbevaring|forespørgsel|samtykkeerklæring|bilens|bilopbevaring|prøvekørsel|sprinklervæske|køretøj|privatlivspolitik|særdeles|afgift|hverdage|åbningstider)\b/i;
function walk(value,key='') {
  if(typeof value==='string') {
    if(!ignoreKeys.has(key)&&!value.startsWith('http')&&residual.test(value))findings.set(value,(findings.get(value)||0)+1);
  } else if(Array.isArray(value))value.forEach(v=>walk(v,key));
  else if(value&&typeof value==='object')for(const[k,v]of Object.entries(value))walk(v,k);
}
for(const file of await readdir(root)) {
  if(!file.endsWith('.json'))continue;
  const meta=JSON.parse(await readFile(`${root}/${file}`));
  if(!meta.type.includes('html')||!new URL(meta.url).pathname.startsWith('/en/'))continue;
  const state=pageDataFromHtml(await readFile(`${root}/${file.slice(0,-5)}`,'utf8'));
  if(state){walk(englishData(state));pages++;}
}
const result={pagesChecked:pages,residuals:[...findings].map(([text,count])=>({text,count}))};
await writeFile('english-audit.json',JSON.stringify(result,null,2));
console.log(JSON.stringify({pagesChecked:pages,residuals:findings.size,samples:result.residuals.slice(0,12)},null,2));
