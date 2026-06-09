# Eden Relics — Full-Site WCAG 2.1 AA + Security Audit

_Generated 2026-06-09 via multi-agent audit (24 controllers + 13 UI areas, adversarially verified). 68 confirmed findings._

## Overall verdict

The site is **fundamentally sound and not in crisis** — there is no unauthenticated breach, RCE, or public data leak, and the bulk of the codebase shows good instincts (constant-time token compares, ImageSharp re-encoding, parameterised queries, deliberate non-enumerable login/reset flows). However, there are **two must-fix-now authentication flaws**: the Facebook login accepts access tokens minted for *any* app (a token-substitution account-takeover path), and the passkey login silently bypasses the MFA second factor you explicitly configured (Admin accounts included). Both should be fixed before anything else. Beyond those, the recurring themes are missing rate limits on several public/sensitive endpoints, a fleet of SSRF/path-traversal gaps in admin tooling, and a systemic accessibility problem: form-field borders and status colours that fall below WCAG contrast minimums, plus a handful of keyboard/ARIA defects in both the storefront and admin UI.

---

## Critical
*None.* No finding rises to an unauthenticated, directly-exploitable compromise.

---

## High

### 1. Facebook login accepts tokens issued for any app → account takeover
**Where:** `backend/Controllers/AuthController.cs:301-332` (`VerifyFacebookToken`)
**Why it matters:** You authenticate a Facebook user by calling the Graph `/me` endpoint and trusting whatever email it returns, but never check the token was minted for *your* Facebook app. A token a victim granted to any other (e.g. attacker-controlled) app is accepted here, and the login logic then links to or silently creates a verified Eden Relics account for that email — impersonation/takeover. Your Google and Apple paths do validate audience; Facebook is the weak link.
**Fix:** Call `GET /debug_token?input_token={token}&access_token={APP_ID}|{APP_SECRET}`, require `data.is_valid == true` **and** `data.app_id == your Facebook App ID` before reading id/email, and add `appsecret_proof` to the `/me` call. Reject on mismatch.

### 2. Passkey login bypasses the MFA second factor
**Where:** `backend/Controllers/PasskeyController.cs:223`
**Why it matters:** The password path correctly withholds a session token and demands a TOTP code when MFA is enabled (`AuthController.cs:91-95`). The passkey path has no such check — after a successful assertion it issues a full 7-day token unconditionally. Any account (Admins included) with both MFA enabled and a passkey registered can sign in without ever presenting the second factor, defeating the control. Since enrolling a passkey only needs an authenticated session, a transiently-stolen session can be turned into a persistent MFA-free login.
**Fix:** Apply the same MFA gate to the passkey path — if `user.MfaEnabled`, return the `mfaRequired` challenge instead of a session token. (Optionally accept a *user-verified* passkey assertion in lieu of TOTP if you explicitly decide that policy.)

### 3. Checkout form fields are nearly invisible (border contrast 1.28:1)
**Where:** `cart.component.scss:307-314`; also `order-confirmation.component.scss:129-131` and the radio/checkbox rows (`cart.component.scss:353`). Token: `--border-color #E0D5BF` on `--bg-primary #F5F0E6`.
**Why it matters:** The checkout inputs/selects are delimited only by a 1px `#E0D5BF` border whose fill matches the page background, giving 1.28–1.37:1 — far below the 3:1 WCAG 1.4.11 requires. Low-vision users on your primary purchase form can't reliably see where the fields are.
**Fix:** Use a much darker border for input/select boundaries — around `#8A6A24`/`#6E4A22` (note `--border-dark #C9B79A` is still only ~1.7:1, not enough). This is one instance of a site-wide pattern (see Medium #5 and the auth-form duplicate).

### 4. Keyboard/ARIA failures on essential controls
Three Level-A defects where a keyboard or screen-reader user is blocked or misled. All are high because they sit on core flows:

- **MFA "Back to Sign In" is a dead anchor** — `admin-login.component.html:65`. An `<a>` with `(click)` but no `href`/`tabindex`/keydown isn't focusable or activatable by keyboard, so there's no way back from the two-factor step. **Fix:** make it a real `<button type="button">`.
- **Star-rating radiogroup has no radios** — `review-page.component.html:25-32` (and 38-45, 51-58). A `role="radiogroup"` whose children are plain `<button>`s with no `role="radio"`/`aria-checked`; AT announces a group with no selectable state, and the selection is conveyed by colour only. **Fix:** give each button `role="radio"` + `[attr.aria-checked]` (and arrow-key handling), or drop the radiogroup role and use `aria-pressed`.
- **Drag-only product gallery reorder** — `admin-page.component.html:313-331`. Reordering is HTML5 drag-and-drop on a `<div>` with no keyboard alternative, and "set primary" is a click on a non-focusable `<img>`. A keyboard-only admin can't order images. **Fix:** add real "Move left/right" and "Set as primary" `<button>`s.

### 5. "Pending" calendar chips: white on amber fails contrast (2.78:1)
**Where:** `admin-calendar.component.scss:203-226` (markup `admin-calendar.component.html:107-115`)
**Why it matters:** White 11px text on `#c89200` is 2.78:1 (needs 4.5:1). Pending is the *default/most common* chip state and the text is the obligation title — essential content. (Scheduled and complete chips pass.)
**Fix:** Darken the pending background (e.g. `#946b00` or lower) or use dark text on the amber. Re-check the waived chip (its `opacity:0.7` lowers effective contrast).

---

## Medium

### 1. Missing rate limits on public & sensitive endpoints (cross-cutting)
Rate limiting is opt-in per controller and several controllers never opted in (there is **no** global limiter). Throttle these by adding `[EnableRateLimiting("auth")]` / `[EnableRateLimiting("contact")]` at the class level, mirroring `AuthController`/`ContactController`:
- **Passkey login-options/login** — `PasskeyController.cs:21`. Unthrottled assertion brute-force + cache-write amplification (each `login-options` writes a 5-min cache entry).
- **MFA verify/disable + change-password** — `AccountController.cs:158-213`. Unlimited TOTP guessing against a wide ±90s verification window; `mfa/disable` also needs only a code, no password.
- **Mailing-list subscribe/unsubscribe** — `MailingListController.cs:9-67`. Unauthenticated DB-row flooding with garbage emails. (Note: no confirmation email is sent, so there's no third-party mail-bombing vector.)
- *(See also the Low-severity rate-limit gaps on orders, reviews, locale-detect and the iCal feed.)*

### 2. Stolen JWTs survive a password change; sensitive mutations don't re-verify MFA
**Where:** `AccountController.cs:112-131`
**Why it matters:** JWTs are stateless with no version/revocation claim, last 7 days, and `Refresh` (`ValidateLifetime=false`) renews them up to ~30 days past expiry. Changing your password doesn't invalidate a captured token — defeating the whole point of a post-compromise password reset (attacker keeps access ~37 days). Address/payment/password mutations also never re-prompt for MFA. (Note: MFA *disable* does correctly require a fresh TOTP.)
**Fix:** Add a per-user security-stamp/token-version claim, validate it on every request and in `Refresh`, and bump it on password change. Require a fresh MFA code or password re-entry before sensitive mutations.

### 3. SSRF & path traversal in admin tooling (cross-cutting)
All three are Admin-gated (hence Medium, not High) but let a request body steer server-side fetches/reads from inside the trust boundary — reachable internal services and cloud metadata (`169.254.169.254`) on Fly.io:
- **SEO analyser** — `SeoController.cs:43-62`. Fetches an arbitrary URL and reflects title/meta/headings/body back to the caller (read-capable SSRF); follows redirects; no scheme/host validation.
- **AnalyseImage (remote)** — `ProductsController.cs:828-829`. Fetches an unvalidated `ImageUrl` (blind SSRF).
- **AnalyseImage (local)** — `ProductsController.cs:819-824`. Builds a disk path via `Path.Combine` from caller input with no `..` containment — arbitrary file read.
**Fix:** For the SSRF cases, require absolute http/https, allow-list to your own/R2/CDN hosts, reject loopback/private/link-local/metadata IPs (re-check after DNS), disable auto-redirect, set timeouts/size caps. For the path case, `Path.GetFullPath` and verify it stays under the uploads root (or accept only `Path.GetFileName`).

### 4. Accessible-name & ARIA-state gaps on controls (cross-cutting, 4.1.2/1.3.1)
A recurring "labelled by placeholder / column position / nothing, and state conveyed by colour" pattern. Fix by adding `aria-label`s and exposing state programmatically:
- **Product thumbnails** — `product-detail.component.html:9-16`. All buttons announce the same name (product name); active state is border-colour only. Add per-button `aria-label="View image N"` (empty `alt` on inner img) + `aria-pressed`.
- **Settings accordion headers** — `settings-page.component.html:7-10` (×8). `role="button"` on a `<div>` wrapping an `<h2>` flattens the heading out of the outline. Move the role onto a `<button>` *inside* the `<h2>`.
- **Star-rating group name** — `review-page.component.html:24-25` (×3). Visible "Transaction/Delivery/Product" label isn't associated; use `aria-labelledby`.
- **Mailing-list email input** — `order-confirmation.component.html:42-49`. Placeholder-only; add `aria-label`.
- **Admin: mobile-nav toggle** (`admin-page.component.html:35-39`) lacks `aria-expanded`/`aria-controls`; **inline table selects/inputs** (`673-695`, `1485-1522`, `2163-2169`, `1658`) lack accessible names — add `aria-label`s naming action + row context.

### 5. Non-text contrast: form/control borders and status colours (cross-cutting, 1.4.11 / 1.4.3)
The same `#E0D5BF`-on-cream 1.28:1 border problem from High #3 recurs across **auth forms** (register/forgot/reset/verify/admin-login inputs, social-login & passkey buttons), **settings** (`settings-page.component.scss:70-79`), the **size converter** (`#C9B79A` 1.72:1), and **review** inputs/stars (`rgba(0,0,0,0.15)` ≈1.41:1). Plus admin status-text below 4.5:1: **Monzo Pending/Settled/Declined** (`admin-page.component.scss:1986-1995`, ~2.0–3.6:1) and **SEO warnings/rank** colours (`#d69e2e` 2.25:1, `#c96226` 3.5:1). **Fix:** adopt darker border and text tokens verified ≥3:1 (borders) / ≥4.5:1 (text) against both `#F5F0E6` and `#FBF8F1` — don't rely on `--border-dark`, which still fails 3:1.

### 6. Keyboard focus outline removed from inputs (2.4.7)
**Where:** auth forms (`register-page.component.scss:60-63` and the four siblings) and settings inputs. `outline:none` on `:focus` (not `:focus-visible`) out-specifies the global accent ring, leaving only a 1px border recolour on an already-faint border.
**Fix:** Drop `outline:none`, or scope it to `:focus:not(:focus-visible)` and add an explicit `&:focus-visible` ring. (See also the Low admin-UI duplicate.)

### 7. Sale-notification modal lacks focus management
**Where:** `product-detail.component.html:100-109` (dup in `product-list.component.html:169-176`)
**Why it matters:** Marked `role="dialog" aria-modal="true"` but no initial focus, no focus trap, no Escape-to-close.
**Fix:** Move focus in on open, trap Tab/Shift+Tab, restore on close, add `(keydown.escape)` (Angular CDK `A11yModule`).

### 8. Per-review star ratings have no text alternative (1.1.1)
**Where:** `home-reviews.component.html:23-28`. Stars are `aria-hidden` with no equivalent, so screen-reader users never learn a reviewer's rating.
**Fix:** Add an sr-only "Rated N out of 5" (or `role="img"` + `aria-label`).

---

## Low

Unverified nits — worth fixing opportunistically, none urgent.

**Security hardening**
- **Passkey enrollment enumeration oracle** — `PasskeyController.cs:146`; non-empty `allowCredentials` reveals who has passkeys. Prefer discoverable-credential login; gate behind the rate limiter.
- **DeleteAccount is a soft-delete** — `AccountController.cs:269-281`; orphans PII/related rows while claiming "permanently deleted." GDPR-accuracy issue — truly erase/anonymise, or fix the message.
- **Registration is enumerable** — `AuthController.cs:48-52`; 409 reveals existing emails (login/reset are non-enumerable). Also the dup-check uses raw vs lowercased email, allowing a mixed-case duplicate row.
- **Unbounded GeoIP cache on attacker-controlled header** — `GeoIpService.cs:11`; unbounded heap growth + outbound `ip-api.com` amplification via `/api/locale/detect`. Validate the IP, bound the cache (TTL), rate-limit the endpoint.
- **Unsubscribe IDOR** — `MailingListController.cs:48-67`; no ownership token. Use a signed per-subscriber unsubscribe token.
- **CSV formula injection** — `MonzoController.cs:394` and `FinanceController.cs:372`; prefix cells starting with `= + - @` with a quote. (Single-admin trust model, so optional.)
- **Upload allow-list / size nits** — receipt `.pdf` faults through ImageSharp (`MonzoController.cs:270-298`); branding `.svg` is non-functional (`BrandingController.cs:56-70`); video stored without re-encode (`ProductsController.cs:429-476`); blog upload allows a 4 GB body (`BlogController.cs:135`). All Admin-only; tidy allow-lists, add empty-file guards, cap sizes.
- **OAuth `state` never validated** — `MonzoController.cs:49-77`; persist and compare it.
- **Stored review text not sanitised** — `ReviewsController.cs:136-149`; rely on moderation but HTML-encode at render too. Display name is fully attacker-chosen.
- **iCal feed: no rate limit on token check / token in query string** — `CalendarFeedController.cs:30-50`, `CalendarController.cs:40`; throttle and enforce a high-entropy, rotatable token.
- **Order quantity unvalidated** — `OrdersController.cs:88`; allows 0/negative/>1 on one-of-one stock. Enforce `[Range(1,1)]` and reject before persisting.
- **Anonymous order-create not rate limited** — `OrdersController.cs:32`; throttle to cap junk orders / Stripe sessions.

**Accessibility nits**
- **No `prefers-reduced-motion` anywhere** — storefront (`product-list.component.scss:276-279`) and admin. Add one global `@media (prefers-reduced-motion: reduce)` reset.
- **Heading-structure issues** — cart h1→h3 skip (`cart.component.html:9,15`); size-converter hardcoded `<h3>` risks h1→h3 in blog posts (`vintage-size-converter.component.html:3`); admin sub-components emit their own `<h1>` (multiple H1s) (`admin-accounting/admin-calendar .html:4`).
- **Missing live-region / status announcements** — mailing-list success (`order-confirmation.component.html:37-55`), blog loading/not-found (`blog-post.component.html:84-93`), copy-link success (`share-buttons.component.html:49-66`).
- **ARIA polish** — breadcrumb current page missing `aria-current="page"` (`designer-page.component.html:8`); settings panels missing `aria-controls` (`settings-page.component.html:7-29`); calendar `role="grid"` without row/gridcell structure (`admin-calendar.component.html:92-121`); cookie banner `role="dialog"` without modal behaviour (`cookie-banner.component.html:2`) — consider `role="region"` instead.
- **Star-button label grammar** — `"1 stars"` (`review-page.component.html:31`); pluralise.
- **MFA QR alt text** — `settings-page.component.html:227`; `alt="MFA QR Code"` should be decorative `alt=""` (the manual key is the text equivalent).
- **Home/admin focus-outline removal** — duplicates of Medium #6 on `home.component.scss:216-219` and admin inputs.

---

## Themes & quick wins

**Themes**
1. **OAuth/MFA consistency.** Your auth controls are good individually but applied unevenly — Facebook lacks the audience check Google/Apple have; passkeys skip the MFA gate the password path enforces; sensitive mutations don't re-verify. *Make every auth path enforce the same rules.*
2. **Rate limiting is opt-in and forgotten.** There's no global limiter, so each new controller silently ships unthrottled. Consider a sensible **global** default limiter so the safe behaviour is automatic.
3. **One bad colour token, many failures.** `--border-color #E0D5BF` on cream backgrounds is the single root cause behind most of the 1.4.11 findings. Fix the token once and most border failures disappear.
4. **`outline:none` on `:focus`.** Repeated across the app, it silently defeats your own global `:focus-visible` ring. Search-and-replace to `:focus:not(:focus-visible)`.
5. **Admin tooling trusts request bodies.** SSRF/path-traversal/injection cluster in admin endpoints — add input validation even behind the Admin gate (the Admin JWT is the only barrier, and there's no per-request MFA).

**Quick wins (high impact, low effort)**
- Add the Facebook `debug_token` app-id check (#High-1) and the passkey MFA gate (#High-2).
- Add `[EnableRateLimiting("auth"/"contact")]` to `PasskeyController`, `AccountController`, `MailingListController`, `OrdersController`, `ReviewsController`, `CalendarFeedController` — a one-line attribute each.
- Define new darker `--border-control` (≥3:1) and status-text tokens, swap them in — clears most contrast findings at once.
- Global `@media (prefers-reduced-motion: reduce)` reset and global `:focus:not(:focus-visible)` outline scoping — two small CSS changes covering many nits.
- Change the `DeleteAccount` success message (or implement real erasure) to match GDPR claims.
