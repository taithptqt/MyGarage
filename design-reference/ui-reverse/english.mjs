import { readFileSync } from 'node:fs';
import * as html from 'parse5';

export const routeAliases = {
  '/':'/en/', '/om-my-garage/':'/en/about-my-garage/', '/storage/':'/en/storage/',
  '/sales/':'/en/sales/', '/biler-til-salg/':'/en/cars-for-sale/',
  '/moede-og-event/':'/en/meetings-events/', '/partnere/':'/en/partners/',
  '/nyheder/':'/en/news/', '/kontakt/':'/en/contact/', '/faq/':'/en/faq/',
  '/ledige-stillinger/':'/en/jobs/', '/brands/':'/en/brands/', '/blog/':'/en/blog/',
  '/samtykkeerklaering/':'/en/samtykkeerklaering-2/',
  '/om-my-garage/aabningstider/':'/en/about-my-garage/opening-hours/'
};
export function englishRoute(path) {
  const normalized = path.endsWith('/') ? path : path + '/';
  return routeAliases[normalized] || path;
}
export const labels = {
  'Navn':'Name','Mail':'Email','E-mail':'Email','Telefon':'Phone','Telefonnummer':'Phone number',
  'Bilens mærke':'Car make','Model':'Model','Årgang':'Year','Besked':'Message','Send besked':'Send message',
  'Send':'Send','Bilopbevaring':'Car storage','Send forespørgsel':'Send enquiry',
  'Tilkøb af ekstra ydelser':'Additional services','Partner email':'Partner email',
  'Vælg afdeling':'Select location','Jeg vil gerne høre mere om':'I would like to know more about',
  'Bliv kontaktet':'Request a callback','Book prøvekørsel':'Book a test drive',
  'Bliv kontaktet af vores salgsafdeling':'Contact our sales team',
  'Bliv kontaktet af vores servicecenter':'Contact our service centre',
  'København':'Copenhagen','Ja':'Yes','Nej':'No','Benzin':'Petrol','El':'Electric','Læder':'Leather',
  'Kontaktformular':'Contact form','Kontakt os':'Contact us','Kontakt':'Contact','Læs mere':'Read more',
  'Forespørg nu':'Enquire now','Få din bil opbevaret':'Store your car',
  'Forespørg på':'Enquire about','pakken':'package','Kontakt os for priser':'Contact us for prices',
  'MY GARAGE er skabt ud fra filosofien om at samle de bedste kompetencer i den automotive verden i ét og samme hus. Læs de seneste nyheder her.':'MY GARAGE brings together the finest automotive expertise under one roof. Read our latest news.',
  'Stilstandsforsikring: Kontakt for tilbud':'Storage insurance: contact us for a quote',
  'Montering af lader 195 kr.':'Charger installation DKK 195',
  'Klargøring Deluxe 2.395 kr.':'Deluxe detailing DKK 2,395',
  'Kontrol af dæktryk & påfyldning af max dæktryk 395 kr.':'Tyre pressure check and inflation to maximum pressure DKK 395',
  'Kontrol af olie, sprinkler- og kølervæske (ekskl. påfyldning) 395 kr.':'Oil, washer fluid and coolant check (excluding top-ups) DKK 395',
  'Kontrol af batteri samt montering af lader (inkl. vedligeholdelseslader) 995 kr.':'Battery check and charger installation (including maintenance charger) DKK 995',
  'Syn af bil inkl. kørsel 895 kr.':'Vehicle inspection including transport DKK 895',
  'Toldsyn af bil inkl. kørsel 1.595 kr.':'Vehicle customs inspection including transport DKK 1,595',
  'Afhentning / levering af bil i lukket trailer pr. time + Yderligere udgifter 750 kr.':'Collection/delivery in an enclosed trailer, per hour, plus expenses DKK 750',
  'Afmontering og afmelding af nummerplader 395 kr.':'Number plate removal and deregistration DKK 395',
  'Indregistrering inkl. nummerplader og montering 1.895 kr.':'Registration including number plates and fitting DKK 1,895',
  'Tankning af brændstof + Dagspris pr. liter 195 kr.':'Refuelling service, plus fuel at the daily price per litre DKK 195',
  'Indleveringstjek: Påfyldning af max dæktryk, montering af vedligeholdelseslader, afmontering og afmelding af nummerplade 1.995 kr.':'Check-in: maximum tyre pressure, maintenance charger installation, number plate removal and deregistration DKK 1,995',
  'Udleveringstjek: Kontrol af dæktryk, dækmønster, væsker, evt. opfyldning, afmontering af lader 1.995 kr.':'Check-out: tyre pressure, tread and fluid checks, top-ups if needed, and charger removal DKK 1,995',
  'Danish':'English','Dansk':'English','Cookieindstillinger':'Cookie settings'
  ,'Søg på siden':'Search this site','Søg':'Search','Sider:':'Pages:','Nyheder:':'News:',
  'Åbningstider':'Opening hours','Ingen resultater':'No results','Ryd':'Clear',
  'Se flere detaljer':'Show more details','Se færre detaljer':'Show fewer details',
  'Teknisk Data':'Technical data','Beskrivelse':'Description','Del':'Share'
  ,'Nyheder':'News','Partnere':'Partners','Personvogn':'Passenger car','Koksmetal':'Dark grey metallic',
  'Sortmetal':'Metallic black','Motor type':'Engine type','Farve':'Colour','Betræk':'Upholstery',
  'Servicebog':'Service history','Hjultræk':'Drive wheels','GearType':'Transmission',
  'Inkl. moms pr måned':'Incl. VAT per month','Inkl. moms':'Incl. VAT','Pr. måned':'Per month',
  '6 måneders binding':'6-month minimum term','Afhentning op til 2 gange pr halvår':'Collection up to twice per six-month period',
  'Opbevaring i glasboks':'Storage in a glass bay',
  'Vi har skræddersyet tre pakker, som har til formål at afdække ethvert behov':'We have tailored three packages to cover every need'
};
const foldedLabels = new Map(Object.entries(labels).map(([key,value])=>[key.toLowerCase(),value]));
let translations = {};
try { translations = JSON.parse(readFileSync('english-content.json','utf8')); } catch {}
export function translateText(value, catalog = translations) {
  const key = value.trim();
  const translated = labels[key] ?? catalog[key] ?? foldedLabels.get(key.toLowerCase());
  return translated === undefined ? value : value.replace(key, translated);
}
export function visitHtml(tree, textFn, attrFn = textFn) {
  function visit(node, raw = false) {
    if (node.nodeName === '#text' && !raw) node.value = textFn(node.value);
    if (node.attrs) for (const attribute of node.attrs) {
      if (['placeholder','aria-label','alt','title'].includes(attribute.name)) attribute.value = attrFn(attribute.value);
      if (['href','action'].includes(attribute.name) && attribute.value.startsWith('/')) attribute.value = englishRoute(attribute.value);
      if (node.tagName === 'html' && attribute.name === 'lang') attribute.value = 'en';
    }
    for (const child of node.childNodes || []) visit(child, raw || ['script','style'].includes(node.tagName));
  }
  visit(tree);
  return tree;
}
const skipKeys = new Set(['id','uri','path','url','href','sourceUrl','src','__typename','value','lang','language','cssClass','className']);
export function englishData(value, key = '') {
  if (typeof value === 'string') {
    if (['uri','path','href'].includes(key) && value.startsWith('/')) return englishRoute(value);
    if (skipKeys.has(key) || /^(https?:|data:|mailto:|tel:|\/)/.test(value)) return value;
    if (/<[a-z][\s\S]*>/i.test(value)) return html.serialize(visitHtml(html.parseFragment(value),translateText));
    const translated = translateText(value);
    return ['variant','bodyType'].includes(key) ? translated.replace(/\baut\./gi,'Automatic') : translated;
  }
  if (Array.isArray(value)) return value.map(item=>englishData(item,key));
  if (value && typeof value === 'object') {
    const result = Object.fromEntries(Object.entries(value).map(([k,v])=>[k,englishData(v,k)]));
    if (result.context) result.context.lang = 'EN';
    return result;
  }
  return value;
}
export function englishHtml(source, state) {
  const tree = visitHtml(html.parse(source),translateText);
  function update(node) {
    if (node.tagName === 'script') for (const child of node.childNodes || []) {
      if (child.value?.includes('window.__INITIAL_STATE__=') && state) {
        const start = child.value.indexOf('window.__INITIAL_STATE__=') + 'window.__INITIAL_STATE__='.length;
        const end = child.value.indexOf(';(function()',start);
        if (end >= 0) child.value = child.value.slice(0,start) + JSON.stringify(englishData(state)).replace(/</g,'\\u003c') + child.value.slice(end);
      }
    }
    if (node.tagName === 'meta') for (const attr of node.attrs || []) if (attr.name === 'content') attr.value = translateText(attr.value);
    for (const child of node.childNodes || []) update(child);
  }
  update(tree);
  return html.serialize(tree);
}
