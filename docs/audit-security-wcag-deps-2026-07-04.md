# Eden Relics — Security + WCAG AA + Dependency Pass (2026-07-04)

_Follow-up to the [2026-06-09 full-site audit](audit-wcag-security-2026-06-09.md). This pass (a) updated dependencies (including a major Angular 21→22 upgrade), (b) security-reviewed the code added since June, and (c) WCAG-audited the UI added since June. All findings below are remediated on the `integration` branch. Verified: backend builds 0-warning + full xUnit suite green (incl. a new test); frontend builds clean + 73 unit tests green on Angular 22._

---

## 1. Dependency updates

### Backend (.NET 10) — `dotnet list package --vulnerable` now reports **0 vulnerable packages**

| Package | From → To | Note |
|---|---|---|
| **Microsoft.OpenApi** (transitive) | 2.0.0 → **2.7.5** (pinned) | **Fixes GHSA-v5pm-xwqc-g5wc** (High, DoS via circular `$ref`). Pinned directly because `Microsoft.AspNetCore.OpenApi` still resolves the vulnerable 2.0.0 even at 10.0.9. Low real exposure (app only *generates* its own spec) but cleared. |
| Microsoft.AspNetCore.* / EntityFrameworkCore.* / OpenApi / Bcl.Memory | 10.0.5 → **10.0.9** | Servicing/security patch line for net10. |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.1 → **10.0.2** | |
| AWSSDK.S3 | 4.0.19 → **4.0.100.2** | |
| Fido2 / Fido2.AspNet | 4.0.0 → **4.0.1** | |
| Google.Apis.Auth | 1.74.0 → **1.75.0** | |
| Stripe.net | 50.4.1 → **52.1.0** | Two majors. Builds + full test suite green. **Pins a newer Stripe API version** — smoke-test a live checkout post-deploy. |
| Resend | 0.2.2 → **0.5.1** | Pre-1.0 minor jump. Builds + tests green. |
| _(tests)_ Microsoft.AspNetCore.Mvc.Testing / EntityFrameworkCore.InMemory | 10.0.5 → **10.0.9** | Kept in lockstep. |

**Held back — `SixLabors.ImageSharp` 3.1.12 (not → 4.0.0).** ImageSharp v4 **changed its licence** and now *enforces a paid commercial licence key at build time* (build fails with "No Six Labors license found"). v3.1.x is the last edition usable without a purchased licence and **has no known vulnerabilities** (our scan is clean), so bumping is a cost/licensing business decision, not a security fix. **Left on 3.1.12 pending a product/licensing decision.**

### Frontend (Angular) — npm vulnerabilities **38 → 3** (all low, dev-toolchain only)

- **Angular 21.2.1 → 22.0.5** — full framework via `ng update @angular/core@22 @angular/cli@22` (core, common, compiler, forms, platform-browser, platform-server, router, ssr, build, cli). 36 source files + config auto-migrated. This clears the Angular runtime advisories (XSS in i18n/two-way bindings & namespace sanitisation; `HttpTransferCache` credential leak; `formatDate`/`digitsInfo` DoS). Stepped through 21.2.17 first (the recommended one-major-at-a-time path).
- **TypeScript 5.9 → 6.0.3** — pulled in by the Angular 22 migration.
- **vitest / @vitest/browser-playwright → 4.1.9** — clears **critical** vitest browser-mode RCE (GHSA-2h32-95rg-cppp, GHSA-g8mr-85jm-7xhm) and the high `undici`/`ws`/`esbuild` dev-server advisories.
- prettier → 3.9.4, @types/node → 20.19.43, @cloudflare/workers-types → 4.20260702.1, @playwright/test / playwright → 1.61.1.

**Residual: 3 low-severity, build-time-only, currently un-fixable.** `@babel/core` (≤7.29.0, arbitrary file read via `sourceMappingURL`) and a nested `esbuild` 0.27.3 (dev-server file read on Windows) are pinned transitively by `@angular/build@22.0.5` — the latest release. npm's only offered fix (`npm audit fix --force`) would **downgrade `@angular/build` to a vulnerable 21.2.18**, undoing the Angular 22 upgrade, so it was **not** run. These execute only during local `ng build` / `ng serve`; they are **not** in the shipped browser bundle or the deployed Cloudflare Worker. Tracked to clear when Angular ships a build-tooling bump.

**Peer note:** `@ngrx/signals` has no Angular-22 release yet (latest 21.1.1 peer-requires Angular ^21). It's kept at 21.1.1 and installed with `--legacy-peer-deps`; the signals primitives it wraps are stable across 21→22, and the build + all 73 unit tests pass. Move it to `@ngrx/signals@22` once published.

**Also available but intentionally deferred (majors, no security driver):** `@types/node` 26, `jsdom` 29, `@cloudflare/workers-types` 5.

---

## 2. Security review — code added since 2026-06-09

Scope: the Care engine (controller/services/entities), first-party Analytics ingest + Worker beacon, `SeoController.Traffic`, MerchantFeed, Collections (Wildflower Edit preview→publish). Verified against real code paths, attribute inheritance, query filters, and DTO projections. `Marketplace`/`MailingList`/`Blog` controllers were unchanged since June (already covered).

### Medium — Analytics ingest could be flooded with unbounded distinct rows — **FIXED**
**Where:** `backend/Services/AnalyticsIngestService.cs`, fed by `AnalyticsController.PageView` and the Worker beacon (`frontend/src/worker.ts`).
**Issue:** the beacon is secret-gated (so counts can't be *forged*), but the trusted Worker fires it for **every** 2xx SSR render including the visitor's chosen path. Unknown paths hit Angular's `**` not-found route, which still renders **HTTP 200**, so an unauthenticated caller requesting `/‹random›` repeatedly minted one new `PageViewDaily` row per unique path — unbounded table growth and pollution of the first-party traffic data the business relies on. No PII is stored and each hit costs the attacker a full SSR render, keeping it Medium.
**Fix:** two self-contained guards in `AnalyticsIngestService`:
1. **`RouteAllowList.IsRecordable(path)`** — only record paths matching a real front-end route shape (static routes + parameterised prefixes, kept in sync with `app.routes.ts`); arbitrary junk paths are dropped (beacon still returns 204). Kills the trivial arbitrary-path vector.
2. **`MaxNewBucketsPerDay = 5000`** — a hard daily cap on *new* `(path, isBot, country)` buckets, so even a valid-shaped high-cardinality attack (e.g. `/product/{random-guid}`) cannot grow the table without bound; existing buckets still increment.
New regression test `PageView_UnknownPath_IsAccepted_ButNotRecorded` + updated aggregation test to use a real route shape.

### Low — Anonymous fabric-identify vision endpoint (cost-DoS) — accepted, already mitigated
**Where:** `CareController.Identify` → `CareDraftService.IdentifyFabricAsync` (paid Anthropic vision call per request).
Already well-guarded for its class: `[EnableRateLimiting("contact")]` (3/min per real client IP), media-type allow-list, ~6 MB size cap, cheap Haiku model, 1024-token cap. Residual distributed-IP cost risk is Low. **Recommendation (not yet built):** add a global daily AI-spend counter as a hard ceiling above the per-IP limiter if AI cost ever spikes.

### Reviewed and clean
AuthZ (every Care admin endpoint `[Authorize(Roles="Admin")]`; Collections/Seo admin-gated at class level, inherited by the `SeoController.Traffic` partial incl. the outbound `POST traffic/run`); no raw SQL / injection; **no LLM prompt-injection-to-action** (care prompts interpolate only admin-authored fabric/issue data; model output is only deserialised into DTOs, never triggers actions); no new SSRF / path traversal; **no data exposure** (merchant feed emits Live-only; every new entity has the `!IsDeleted` query filter; **Wildflower Edit unpublished pieces are `Stock` status and never surface** through the public product list, which exposes only `Live` + `Sold && InCollection`; `PageViewDaily` stores no PII); merchant-feed XML properly `Escape()`d + CDATA-guarded; DTOs mapped field-by-field (`Id`/`Status`/`IsPublished`/`ReviewedBy` set server-side, not bindable).

---

## 3. WCAG 2.1 AA — UI added since 2026-06-09 (all fixed)

Scope: Care hub (incl. photo fabric-ID + interactive finder), Wildflower Edit collection pages, Shop/decade product list, homepage restructure + newsletter, blog column, share buttons. Verified against the established tokens (`--border-control #8A6A24`, `--text-muted #6E4A22`, global `:focus-visible`, global `prefers-reduced-motion` reset, `appFocusTrap`).

| # | Sev | SC | Where | Fix |
|---|-----|----|-------|-----|
| 1 | **High** | 2.1.1 Keyboard | `care-hub.component.html` photo fabric-ID | The `<input type="file" hidden>` was out of the tab order → the whole feature was keyboard-unreachable. Changed `hidden` → `.sr-only` (stays focusable) + `for`/`id` association + `:focus-within` outline on the visible label. |
| 2 | Med | 4.1.3 Status Messages | `care-hub.component.html` | Analysing/error/guesses/finder-result output was announced to no one. Added `role="status" aria-live="polite"` (and `role="alert"` for errors). |
| 3 | Med | 1.4.11 Non-text contrast | `care-hub.component.scss` | Finder `<select>` border 1.51:1 and dashed upload border 2.22:1 → `--border-control` (≥4.3:1). |
| 4 | Med | 1.4.11 Non-text contrast | `product-list.component.scss` | Shop/decade search input (1.37:1), size `<select>` and category buttons (1.72:1) weren't migrated to the new token → `--border-control`. |
| 5 | Med | 1.4.3 Text contrast | `care-hub.component.scss`, `care-fabric.component.scss` | Muted browns below 4.5:1 (breadcrumbs `#8a7a5e` 3.68:1, struck "was" price `#9a8f7a` 2.81:1, confidence/disclaimer/byline) → `--text-muted` (6.9:1). |
| 6 | Low | 4.1.3 / 2.4.3 | `home.component.html`, `collection-preview.component.html` | Newsletter success and collection-preview loading/error/success messages weren't announced → `role="status"`/`role="alert"` + `aria-live`. |

Confirmed already-clean by the audit (no action): reduced-motion covers the new card-zoom hover; "Sold" badges 7.88:1; share buttons' group/labels/live-region; sale modal focus-trap; product-card `aria-pressed`/pagination `aria-current`; blog-post status/alert regions.

---

## 4. Deploy notes

- **No config/secret changes required** by this pass (contrast with the June audit's `token_version` sign-out and Facebook secret).
- **Stripe.net 52** pins a newer Stripe API version — the test suite uses no live Stripe, so **smoke-test a real checkout + webhook after deploy**.
- The **Angular 22** upgrade is a client-bundle change; re-run the frontend build in CI. `@ngrx/signals` remains on v21 via `--legacy-peer-deps` (see §1) — CI installs must carry that flag until an ngrx v22 ships.
- Still genuinely open: ImageSharp v4 licence decision (§1); the 3 low dev-only npm residuals (clear on the next Angular build-tooling release); the optional global AI-spend cap (§2 Low).
</content>
</invoke>
