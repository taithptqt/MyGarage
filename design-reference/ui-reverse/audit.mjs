import { readdir, readFile, writeFile } from 'node:fs/promises';
const files = await readdir('mirror-cache');
const assets = await Promise.all(files.filter(f=>f.endsWith('.json')).map(async f=>JSON.parse(await readFile('mirror-cache/'+f,'utf8'))));
const types = {};
for(const a of assets) types[a.type]=(types[a.type]||0)+1;
const report = {capturedAt:new Date().toISOString(),resources:assets.length,bytes:assets.reduce((n,a)=>n+a.bytes,0),types,pages:assets.filter(a=>a.type.includes('text/html')).map(a=>a.url),verification:{automated:'Run npm test for current results; tests cover offline route data, 12 local form schemas, large-video ranges, 404 and local-only submissions.',desktop:'Browser-tested Storage validation and demo success; FAQ/Sales navigation and Back preserve the document.',mobile:'355px viewport tested: cookie persistence, menu and SPA navigation; Storage form has no horizontal overflow.',scope:'Published Vue build plus local UI and targeted build patches. Cookie UI is a local reconstruction; form completion is demo-only. Exhaustive pixel/animation parity and all undiscovered pages are not claimed. Uncached routes and new search queries can still require network.'}};
await writeFile('inventory.json',JSON.stringify(report,null,2));
console.log(JSON.stringify(report,null,2));
