# MY GARAGE local UI mirror

The local website now defaults to English at `/en/`. Legacy main-page URLs redirect to their native English equivalents. Remaining Danish page copy, vehicle descriptions, form schemas, dynamic labels, cookie choices and demo messages are localized from `english-content.json` and `english.mjs`; proper names, original media and submitted option values are preserved. Calendar controls use the English locale.

`npm run english` refreshes the published English page snapshots and builds missing translations from public source content. Supplemental translations use Google's translation endpoint during this build step and are saved locally; form entries are never sent to the translation service. `english-report.json` records build coverage and failures. `node scripts/audit-english.mjs` checks the prepared English page data for selected residual Danish terms.

Bản sao giao diện chạy local, giữ HTML/CSS/JavaScript được website gốc công khai. Đây là bản mirror của build Vue đã biên dịch, chưa phải bộ component được viết lại để phát triển độc lập. Chạy `npm start`, mở http://localhost:4173. File đã tải nằm trong `mirror-cache`; file chưa lưu cần kết nối mạng ở lần mở đầu. Xem `inventory.json` để biết lượng tài nguyên đã lưu và phạm vi kiểm chứng; xem `capture-report.json` sau khi lượt tải kết thúc để biết lỗi tải.

Run with Node.js 20 or later:

```sh
npm start
```

Open http://localhost:4173. `npm run capture` preloads linked pages, original scripts, CSS, fonts, images and videos into `mirror-cache`. Requests also populate the cache while browsing. Cached files remain usable without fetching the original again. Uncached resources require network access.

This is a local mirror of the site's published Gridsome/Vue build, not a rewritten React project. It retains the original markup, responsive styles, Vue components, sliders, transitions and video playback. Internal links use Vue Router. Route JSON is generated from the matching cached HTML snapshot so build hashes stay consistent. Public media from the site's CMS and vehicle image CDN is served through local cache URLs, with streaming and byte-range support for large videos.

Tracking bootstrap scripts and the Vue analytics plugin are disabled. The cookie dialog is reconstructed locally, including category toggles, details, saved choices, reopening and keyboard focus handling. Twelve public dynamic form schemas are stored in `forms.mjs`. Vue renders them without upstream authentication. Native required-field/email validation and a local completion message replace message delivery. No form data is sent to MY GARAGE. External social, map and magazine links still point to their original destinations. New search queries still depend on the public search API unless cached.

`capture-report.json` preserves the initial capture report. `npm run repair` scans cached pages, repairs their discovered media and writes route JSON plus `repair-report.json`. The updated crawler ignores duplicate relative favicons and malformed template URLs. `npm run capture` performs an expanded crawl with no fixed page cap; set `MAX_PAGES` to bound it. This is not a verified exhaustive offline archive of every historical article or vehicle. `OFFLINE=1` disables cache-miss upstream fetching when starting the server. Run `npm test` to exercise offline server behavior.

Files:
- `server.mjs`: local cache server, URL rewriting, byte-range video responses and preview guards.
- `capture.mjs`: recursive public page and asset collector.
- `mirror-cache/`: original downloaded resources keyed by URL hash; adjacent JSON files preserve source URL and media type.
- `forms.mjs`: twelve public form schemas captured from rendered source forms.
- `public/local-ui.js`, `public/local-ui.css`: local cookie UI, form validation/demo completion and same-page smooth scrolling.
- `build-patches.mjs`: null-safe ScrollIndicator cleanup and disabled analytics in the served build. Original cached bundles remain unchanged.
- `tests/server.test.mjs`: offline integration and regression checks.

Source: https://mygarage.dk/ — captured November 13, 2026. Original branding and media remain attributable to their respective owners.
