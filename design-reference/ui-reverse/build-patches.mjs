// Small, reviewable corrections to the published development build.
// Original cached resources stay unchanged.
import { translateText } from './english.mjs';
export function patchBuild(text) {
  return text.replace(/eval\(("(?:\\.|[^"\\])*")\);/g, (original, serialized) => {
    if (!serialized.includes('/src/')) return original;
    let code = JSON.parse(serialized);
    if (code.includes('handleScroll()') && code.includes('sourceURL=webpack:///./src/components/ScrollIndicator.vue?')) {
      code = code.replace('document.querySelector("#scroll-indicator").style.opacity = Math.max(some, 0);',
        'const indicator = document.querySelector("#scroll-indicator"); if (indicator) indicator.style.opacity = Math.max(some, 0);');
      code = code.replace('  mounted() {', '  beforeDestroy() { window.removeEventListener("scroll", this.handleScroll); },\n  mounted() {');
    }
    if (code.includes('sourceURL=webpack:///./src/main.js?')) {
      code = code.replace(/Vue\.use\(vue_gtag__[\s\S]*?\}, router\);/, '/* Analytics disabled in local preview. */');
      code = code.replace('.defaultLocale = "da"', '.defaultLocale = "en"');
    }
    code = code.replace(/_vm\._v\(("(?:\\.|[^"\\])*")/g, (match, literal)=>`_vm._v(${JSON.stringify(translateText(JSON.parse(literal)))}`);
    code = code.replace(/("(?:placeholder|aria-label|alt)":\s*)("(?:\\.|[^"\\])*")/g, (match,prefix,literal)=>prefix+JSON.stringify(translateText(JSON.parse(literal))));
    code = code.replaceAll('Forespørg på ', 'Enquire about ').replaceAll(' pakken', ' package');
    return `eval(${JSON.stringify(code)});`;
  });
}
