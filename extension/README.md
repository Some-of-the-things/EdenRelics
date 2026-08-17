# Eden Relics crosslister — browser extension

Fills Vinted and Depop listing forms from one Eden Relics record. Etsy and eBay do **not** go through
here: they have real APIs and are driven from the server, so they work whether the seller's machine is
on or not (brief §3). This exists only for the two platforms where no legitimate server-side route
exists.

One extension, per-platform modules — [decision 1](../docs/vintage-data-engine/crosslisting-decisions.md).
Session handling, pacing, messaging and failure reporting are written once; the selectors that
actually break live in isolated modules, so a Vinted redesign cannot take Depop down with it.

No build step, no dependencies. Plain ES modules, loaded directly by Chrome and by `node --test`.

---

## The four rules this is built around

These are not style preferences. Each one is here because breaking it costs a seller something.

1. **It never presses publish.** It fills the form and stops; the seller reviews and publishes. This
   is brief §4.1, it is what makes the tool honestly "assistive", and it is what keeps a seller's
   Vinted account away from automated enforcement — where the consequence lands on them, not us.
   Enforced by a test that reads the marketplace-side files and fails if a `.click(` or `.submit(`
   ever appears in one.
2. **Unresearched means refuse.** Two independent gates: the server has to say it knows the
   platform's fields *and* our selector table has to be researched. Either one unresearched and we
   do not touch the form. A tool that quietly fills the wrong fields is worse than one that admits it
   cannot.
3. **Failure is visible, and always carries the paste text.** The seller must never believe something
   went live that didn't. The pasteable content is produced by the server up front, precisely so it
   exists whether or not the extension worked.
4. **No marketplace credentials, ever.** It works inside the seller's existing logged-in sessions.
   The only credential it holds is an Eden Relics session token, handed over by a button press on our
   own site and revocable from the popup.

## Install (gated beta)

`chrome://extensions` → Developer mode → **Load unpacked** → this directory.

Then, signed in as an admin on the site, open **Admin → Cross-listing readiness** and press
**Connect extension**. That hands over the Eden session token and the API endpoints for whichever
environment the page is on, so a staging pairing cannot post events into prod's metrics.

Not on the Chrome Web Store. Before it is submitted, drop the two `http://localhost` entries from
`host_permissions` — they exist for local development and are a review flag.

## How a listing goes out

```
admin page ──press──▶ bridge.js ──▶ service-worker ──▶ /api/cross-listing/preview/{id}
                                          │
                                          ├─ gate: server researched? publishable? our selectors researched?
                                          ├─ pace: rate limit + jittered gap since the last listing
                                          ├─ open the platform's new-listing tab
                                          ▼
                                     listing.js ──▶ fill.js ──▶ platform field table
                                          │
                                          └──▶ overlay: "filled — you press publish"
```

The seller then publishes. The content script sees the URL change to a published item and only
*then* reports `ExtensionPublishSucceeded` — we never press the button, so a form we filled and the
seller walked away from is neither a success nor a failure, and is not recorded as either.

## The monthly fix

Brief §10: "Vinted integration is a permanent maintenance tax. It will break when Vinted updates
their DOM — plan for roughly monthly fixes, forever." The design assumption is that this costs a data
edit, not a code change.

**Symptom → fix:**

| What you see | What it means | What to do |
|---|---|---|
| Metrics panel, commonest reason `field-not-found:price` | A selector no longer matches | Edit that field's `strategies` in `src/content/platforms/<platform>.js` |
| Overlay says "found N fields the long way round" | Resolved by a fallback strategy | Same file — promote the working strategy to first. This is the early warning, act on it before it becomes the row above |
| `page-not-recognised` | The listing URL or flow changed | `newListingUrl` / `isListingPage` in the platform module |
| `not-signed-in` climbing | Our sign-in probe is wrong, not the seller | `signedIn()` in the platform module |

The failure reason is a fixed vocabulary with the variable part after a colon (`field-not-found:price`)
so the "commonest reason" column groups usefully. Don't invent free-text reasons — a column where
every entry is unique tells you nothing.

After any edit: `npm test`.

## Flipping a platform to researched

Both gates, and they are separate on purpose:

- **Server side** — `FieldMappingResearch.Documented` on the adapter in
  `backend/Services/CrossListing/PlatformAdapters.cs`, once brief §6's research exists for it.
- **Extension side** — `research: 'documented'` in the platform module, once every field in the table
  has been confirmed against the live form.

`test/platforms.test.js` asserts both platforms are still `unresearched` and will fail when you flip
one. That is intentional: it makes you delete an assertion that says "has the research actually been
done?", which is a decent moment to be sure it has.

## What is not built

- **Sale detection is inert.** The machinery is here — jittered polling, only while the browser is
  open, honest "last checked" reporting — but the per-platform request is unresearched and returns
  nothing rather than guessing. Guessing which request lists sold items would be guessing about
  money, and a delist on a wrong guess is worse than no delist.
- **Both field tables are placeholders.** The selectors are the right *shape*, confirmed by nobody.
- **Relisting.** Deliberately not here at all. Relist is pull-only on the site and must never become
  something the extension does on a schedule.

## Tests

```sh
npm test          # node --test, 53 tests, no dependencies
```

They cover the pacing (ranges, decorrelation, the rate limiter surviving a worker restart), the two
research gates, the fill engine (strategy order, fallbacks, stopping dead on a miss), the event
outbox (parking, retry, and the one case where a batch is deliberately dropped), the wording of
every failure, and the no-submit invariant.

The fill engine is tested against a hand-written fake DOM rather than jsdom. Its contract is narrow —
`querySelector`, a label sweep, and a prototype `value` setter — and a fake that implements only that
makes it obvious the moment the engine starts depending on something wider.

## Layout

```
src/shared/       protocol, pacing, seller-facing wording — no chrome APIs, so Node can test them
src/background/   service worker, queue, auth, API client, sale watch — all the state lives here
src/content/      bridge (our site) and listing (marketplaces) + the fill engine and overlay
src/content/platforms/   the disposable half: one file per marketplace, selectors only
src/popup/        status, and the paste text when something failed
src/onboarding/   the plain-English disclosure, shown on install (brief §8)
```

The split is deliberate: the half that breaks monthly holds no state and no credentials.
