# React public/customer UI migration

Date: 2026-11-15. Source: the user-provided `E:\AI biển số xe\UI Reverse` mirror.

## Scope and implementation

- The reference source is preserved in `design-reference/ui-reverse`, excluding
  installed dependencies. Its cache/evidence remain local and ignored by Git.
- The running application uses React components in `Frontend`; it does not run
  the mirror server, embed the entire website in an iframe, or load Vue bundles.
- The header, mobile navigation, footer, typography, colors, spacing, home video,
  partner strip and car cards follow the supplied reference. Fonts are served
  locally to avoid cross-origin font restrictions.
- Typography uses the normal-width Manrope variable font for both headings and
  body text. Its bundled glyph map covers the 98 checked Vietnamese letters;
  the original DIN Condensed faces are no longer selected. Public headings share
  a responsive size scale, and navigation collapses before normal-width text
  would crowd the logo. Manrope's SIL OFL license is bundled with the font.
- Public/customer routes use a scoped brand theme. Admin, accounting, workshop,
  dispatch, authorization and their business screens retain their existing theme.
- Booking, customer vehicles, appointments, notifications, quotations, parts,
  cart, checkout and authentication retain their API/data flows. Presentation
  and static marketing media are updated independently from these flows.

## React routes

| Route | Content |
| --- | --- |
| `/home` | Video hero, brand strip, linked car cards, storage/events, partners |
| `/storage` | Storage reference sections, packages and contact actions |
| `/about` | About sections and media |
| `/events` | Event sections, image carousel, native Vimeo video embed |
| `/partners` | Partner directory and links |
| `/news` | Searchable/paginated article listing |
| `/discover/*` | Imported car details, galleries, articles and other reference content |
| `/contact` | Live Garage branches, phone/email, map and email-draft inquiry |

The content adapter prepares 266 captured routes. Imported text is localized using
the reference's saved English translation catalog; no translation API is called.
Rich text is sanitized and internal links point to React routes. Imported sales
cars are reference content, not Garage stock or customer vehicle records.

Original mirror form schemas are retained as reference files, not mounted as demo
forms in the app. Their actions point to Garage contact/booking. Contact uses the
selected branch from the existing public branch API and opens an email draft;
it does not pretend that the backend sends an inquiry. Newsletter entry transfers
to that contact draft flow. No new mail/newsletter backend is introduced.

## Cloud media

Image/video resources are uploaded to the configured Azure Blob container under
`site-ui-reverse/`. `Frontend/src/shared/brand/media-manifest.json` contains public
URLs only, without keys or SAS tokens. Videos preserve their content types and
support byte-range playback. Reference Vimeo content uses its original cloud
player with privacy mode; native MP4 files use Azure.

The migration also captures media that were referenced in the source page data
but absent from the original local cache. Large source videos are downloaded in
bounded byte ranges and then uploaded using the Azure SDK. Media caches, upload
checkpoints and secrets are ignored by Git.

Rebuild content and import media from the repository root:

```powershell
node scripts/prepare-ui-reference.mjs
# Only when referenced source media are missing:
node scripts/prepare-ui-reference.mjs --capture-missing
dotnet run --project tools/UiAssetImport
```

The import tool reads existing development/environment Azure configuration. No
credentials belong in the generated manifest or command arguments.

## Verification

```powershell
npm --prefix Frontend run verify
```

The public UI tests check source-to-React navigation, Azure URLs, rich-text
sanitization, home markup/navigation, all captured content routes, and exclusion
of admin/staff paths from the public theme. Public branch access and Azure video
byte-range responses are also checked against the running local system.

Automated React rendering is not a visual browser walkthrough. Browser access was
unavailable during implementation, so desktop/mobile pixel matching and real
authenticated customer interactions require visual review in the running app.

Original reference branding/content attribution is preserved in the reference
README. The source directory outside this repository is untouched.
