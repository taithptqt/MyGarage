(() => {
  document.documentElement.lang = 'en';
  const englishLabels = window.__ENGLISH_LABELS__ || {};
  const foldedLabels = Object.fromEntries(Object.entries(englishLabels).map(([key,value])=>[key.toLowerCase(),value]));
  document.addEventListener('invalid', event => {
    const input = event.target;
    if (typeof input.setCustomValidity !== 'function') return;
    input.setCustomValidity('');
    if (input.validity.valueMissing) input.setCustomValidity(input.type === 'checkbox' ? 'Please select this option.' : 'Please complete this field.');
    else if (input.validity.typeMismatch && input.type === 'email') input.setCustomValidity('Please enter a valid email address.');
    else if (!input.validity.valid) input.setCustomValidity('Please enter a valid value.');
  }, true);
  document.addEventListener('input', event => {
    if (typeof event.target.setCustomValidity === 'function') event.target.setCustomValidity('');
  }, true);
  document.addEventListener('change', event => {
    if (typeof event.target.setCustomValidity === 'function') event.target.setCustomValidity('');
  }, true);
  document.documentElement.dataset.localDocumentId = `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
  // Replace the legacy scroll-to directive only for anchors within this page.
  // All cross-page links continue through Vue Router and its transitions.
  document.addEventListener('click', event => {
    const link = event.target.closest('a');
    if (!link || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    const url = new URL(link.href, location.href);
    if (url.origin !== location.origin || url.pathname !== location.pathname || url.search !== location.search || !url.hash) return;
    let target;
    try { target = document.getElementById(decodeURIComponent(url.hash.slice(1))); } catch { return; }
    if (!target) return;
    event.preventDefault(); event.stopImmediatePropagation();
    target.scrollIntoView({ behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: 'start' });
  }, true);
  // Let Vue Router own all links, hash navigation and page transitions.
  // Only local demo form submissions are intercepted here.
  document.addEventListener('submit', event => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement)) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    if (!form.reportValidity()) return;
    let result = form.querySelector('[data-local-form-result]');
    if (!result) {
      result = document.createElement('div');
      result.dataset.localFormResult = '';
      result.setAttribute('role', 'status');
      result.tabIndex = -1;
      result.className = 'local-form-result';
      form.append(result);
    }
    result.textContent = 'Thank you. Your enquiry is complete in this local demo; no message has been sent.';
    result.focus();
  }, true);

  const consentKey = 'mygarage-local-consent-v1';
  const isEnglish = () => true;
  let previousFocus;
  function openConsent() {
    if (document.getElementById('local-cookie-dialog')) return;
    previousFocus = document.activeElement;
    document.body.classList.add('has-local-cookie');
    const en = isEnglish();
    const overlay = document.createElement('div');
    overlay.id = 'local-cookie-dialog';
    overlay.className = 'local-cookie-overlay';
    overlay.innerHTML = `<section class="local-cookie" role="dialog" aria-modal="true" aria-labelledby="local-cookie-title">
      <header><span class="cookie-brand">Cookiebot <small>by Usercentrics</small></span></header>
      <div class="cookie-content"><h2 id="local-cookie-title">${en ? 'This website uses cookies' : 'Denne hjemmeside bruger cookies'}</h2>
      <p>${en ? 'We use cookies to personalise content and ads, to provide social media features and to analyse our traffic. We also share information about your use of our site with our social media, advertising and analytics partners.' : 'Vi bruger cookies til at tilpasse vores indhold og annoncer, til at vise dig funktioner til sociale medier og til at analysere vores trafik. Vi deler også oplysninger om din brug af vores hjemmeside med vores partnere inden for sociale medier, annonceringspartnere og analysepartnere. Vores partnere kan kombinere disse data med andre oplysninger, du har givet dem, eller som de har indsamlet fra din brug af deres tjenester.'}</p>
      <fieldset><legend>${en ? 'Consent choices' : 'Samtykkevalg'}</legend>
      ${['Nødvendig', 'Præferencer', 'Statistik', 'Marketing'].map((label, i) => `<label class="cookie-option">${en ? ['Necessary', 'Preferences', 'Statistics', 'Marketing'][i] : label}<input type="checkbox" role="switch" data-category="${i}" ${i === 0 ? 'checked disabled' : ''}><span class="cookie-switch" aria-hidden="true"></span></label>`).join('')}</fieldset>
      <button type="button" class="cookie-details" aria-expanded="false">${en ? 'Show details' : 'Vis detaljer'} <span aria-hidden="true">⌄</span></button>
      <div class="cookie-detail-panel" hidden>${en ? 'Necessary cookies remember your consent choice. Preferences, statistics and marketing are optional. This local preview does not load analytics or advertising trackers.' : 'Nødvendige cookies husker dit samtykkevalg. Præferencer, statistik og marketing er valgfrie. Denne lokale demo indlæser ikke analyse- eller annonceringsværktøjer.'}</div></div>
      <footer><button type="button" data-consent="all">${en ? 'Allow all cookies' : 'Tillad alle cookies'}</button><button type="button" data-consent="selected">${en ? 'Allow selection' : 'Tillad valgte'}</button><button type="button" data-consent="necessary">${en ? 'Necessary cookies only' : 'Kun nødvendige cookies'}</button></footer>
      </section>`;
    document.body.append(overlay);
    const old = (() => { try { return JSON.parse(localStorage.getItem(consentKey)); } catch { return null; } })();
    overlay.querySelectorAll('[data-category]').forEach(input => {
      if (input.dataset.category !== '0') input.checked = !!old?.categories?.[input.dataset.category];
    });
    overlay.querySelector('.cookie-details').onclick = event => {
      const panel = overlay.querySelector('.cookie-detail-panel');
      panel.hidden = !panel.hidden;
      event.currentTarget.setAttribute('aria-expanded', String(!panel.hidden));
    };
    overlay.querySelectorAll('[data-consent]').forEach(button => button.onclick = () => {
      const categories = [...overlay.querySelectorAll('[data-category]')].map(input => input.disabled || button.dataset.consent === 'all' || (button.dataset.consent === 'selected' && input.checked));
      try { localStorage.setItem(consentKey, JSON.stringify({ categories, savedAt: new Date().toISOString() })); } catch {}
      overlay.remove();
      document.body.classList.remove('has-local-cookie');
      previousFocus?.focus();
    });
    overlay.addEventListener('keydown', event => {
      if (event.key !== 'Tab') return;
      const items = [...overlay.querySelectorAll('button,input:not([disabled])')];
      const index = items.indexOf(document.activeElement);
      if ((event.shiftKey && index <= 0) || (!event.shiftKey && index === items.length - 1)) {
        event.preventDefault(); items[event.shiftKey ? items.length - 1 : 0].focus();
      }
    });
    overlay.querySelector('button').focus();
  }
  function ready() {
    const translateLabels = () => {
      const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
      let text;
      while ((text = walker.nextNode())) {
        if (text.parentElement?.closest('script,style,textarea')) continue;
        const key = text.nodeValue.trim(), translated = englishLabels[key] || foldedLabels[key.toLowerCase()];
        if (translated && translated !== key) text.nodeValue = text.nodeValue.replace(key, translated);
        const normalized = text.nodeValue.replace(/Forespørg på /g,'Enquire about ').replace(/ pakken\b/g,' package');
        if (normalized !== text.nodeValue) text.nodeValue = normalized;
      }
      document.querySelectorAll('[placeholder],[aria-label]').forEach(element => {
        for (const attribute of ['placeholder','aria-label']) {
          const value = element.getAttribute(attribute);
          if (value && englishLabels[value] && englishLabels[value] !== value) element.setAttribute(attribute,englishLabels[value]);
        }
      });
      document.querySelectorAll('header a').forEach(link=>{
        if (/^(Danish|Dansk)$/i.test(link.textContent.trim())) (link.closest('li') || link).hidden = true;
      });
    };
    const labelFields = () => document.querySelectorAll('.form-wrapper__fields .field').forEach(field => {
      const input = field.querySelector('input:not([type="hidden"]),textarea,select');
      if (!input) return;
      if (!input.id) input.id = `${field.id}-control`;
      field.querySelectorAll('label').forEach(label => label.htmlFor = input.id);
      const labels = [...field.querySelectorAll('label')];
      const label = labels[input.type === 'checkbox' ? labels.length - 1 : 0];
      if (label) input.setAttribute('aria-label', label.textContent.replace(/\s*\*\s*$/, '').trim());
    });
    labelFields();
    translateLabels();
    new MutationObserver(()=>{ labelFields();translateLabels(); }).observe(document.body, { childList: true, subtree: true, characterData: true });
    const settings = document.createElement('button');
    settings.type = 'button'; settings.className = 'cookie-settings';
    settings.setAttribute('aria-label', 'Cookie settings'); settings.textContent = '◉';
    settings.onclick = openConsent; document.body.append(settings);
    let saved; try { saved = localStorage.getItem(consentKey); } catch {}
    if (!saved) openConsent();
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', ready, { once: true }); else ready();
})();
