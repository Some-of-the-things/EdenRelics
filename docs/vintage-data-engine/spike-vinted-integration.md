# Spike: Vinted integration

**Engineering brief §4.1, first de-risking spike. Run 2026-07-31.**

The brief's framing:

> Vinted has **no official public API**. Every competing tool automates it via browser
> extension, and it breaks routinely; automation also sits in a grey area of their terms.
> Vinted is the most important UK marketplace and a tool without it is dead on arrival — so
> find out now how bad this is. **Assume it is a permanent maintenance tax, not a one-off
> build.**

## Headline finding: the premise is out of date

**Vinted now has an official API — Vinted Pro Integrations — and the UK is a supported
market.** It is not public, but it exists, it is documented, and it does exactly what a
crosslisting tool needs.

Docs: <https://pro-docs.svc.vinted.com/> · Portal: <https://pro-portal.svc.vinted.com/>

| API | What it does |
|---|---|
| **Items** | Create, read, update, delete items; validate; price suggestions; ontologies |
| **Orders** | Retrieve orders, cancel, shipment info and labels, relist |
| **Webhooks** | Register and monitor event notifications — including item sold |

Auth is HMAC-SHA256: `X-Vpi-Access-Key` plus `X-Vpi-Hmac-Sha256`, signing timestamp,
method, path, access key and body. Conventional, and nothing exotic to build.

Markets: Austria, Belgium, Germany, Spain, France, Italy, Luxembourg, Netherlands,
Portugal, **United Kingdom**. GBP supported.

### Why this matters more than it first looks

**Auto-delist becomes properly solvable.** The brief calls it "the one deferred feature
that matters" — the thing sellers won't forgive the absence of, where Crosslist's lack of
it is its main criticism and Zipsale's unreliable version is a top complaint. The Webhooks
API notifies on sale. That turns auto-delist from a polling hack into an event handler, and
it is the single most valuable thing this API unlocks.

**The incumbents have not moved.** Zipsale and Vendoo both still reach Vinted through a
Chrome extension, and reviews of both describe exactly the brittleness the brief predicted —
repeated reinstalls, re-logins, inconsistent performance specifically on Vinted. (Vendoo's
own marketplace list does not include Vinted at all in some material.) Nothing found
suggests any crosslisting vendor is on the Pro API.

That is a real, if temporary, competitive opening: reliability the incumbents structurally
cannot match while they remain on browser automation.

## The catch, and it is a big one

Access requires **a Vinted Pro account** *and* **manual allowlisting by Vinted**:

> "Vinted Pro Integrations is available only to a limited set of allowlisted Vinted Pro
> businesses."

Three constraints follow, in descending order of how much they hurt.

### 1. Our target sellers are mostly not Pro — and Pro is a real commitment

Vinted Pro is for "a sole trader (entrepreneur), a non-profit organisation, or a registered
company". Registration requires a company registration number and business details.

The obligations are not cosmetic. Pro sellers must offer a **14-day right of return**, and
take on compliant terms of sale, legal notices (trading name, business address, VAT number
where applicable) and the increased liability of trading as a business.

For a vintage seller listing one-of-one items, a mandatory 14-day returns window is a
genuine change to how the business runs, not a checkbox. **This is the adoption barrier,
and it is the thing to test with real sellers before building anything.**

Mitigating point, and it cuts our way: Vinted's own Catalogue Rules say **"Commercial
selling is only allowed for Pro sellers,"** and Vinted will hide listings or ban accounts
for commercial activity on a private account. Their stated detection signals — selling for
regular income or profit, listing similar items in multiple sizes, large quantities —
describe our target sellers precisely. So the sellers we want are *already* supposed to be
Pro, and many are already sole traders for HMRC purposes (the £1,000 trading allowance
threshold). We would be pushing them towards compliance they already owe, not inventing a
burden.

That is a defensible pitch, but it is still a harder sell than "install our extension", and
it should be validated in conversation before it is designed around.

### 2. The 500-slot cap

> "Initially, this allocation is set to 500 active items per API user."

Requests beyond available slots are **rejected outright**. After 30 days of favourable
performance a seller can ask Account Management for an increase.

For our stock profile this is probably a non-issue — every item is one-of-one, and a small
UK vintage seller with 500 simultaneously *active* listings is doing very well. Worth
stating in seller comms rather than discovering at the cap, and worth handling explicitly in
code: a slot-exhausted rejection must surface as a clear message, never as a silent failure.

### 3. Allowlisting is a gate we do not control

No public application process was found. It appears to be per-business rather than per-
vendor: the seller generates their own token in the Pro Portal, which suggests a tool
authenticates *as the seller* with the seller's key. That is workable — arguably better than
an OAuth vendor relationship, since we never hold a password — but it means **every seller
must individually get through Vinted's allowlist**, and Vinted can decline.

This is the single biggest unknown left, and it cannot be resolved by reading. See open
questions.

## Fees

Reporting varies and it needs first-hand confirmation. Vinted's own Pro page states
plainly: **"Vinted Pro is entirely free. With Vinted Pro, you can list as many second-hand
items as you like at no cost."** Vinted's revenue comes from the buyer-side Buyer Protection
fee (roughly £0.70 + ~5% in the UK). Some third-party commentary claims Pro sellers pay
commission, which contradicts Vinted's own page. **Do not plan seller economics on
second-hand blog sources — confirm directly.**

## What this changes about the architecture

The brief's open question 1 was extension vs cloud, "probably hybrid". The finding sharpens
it into a clear recommendation:

**Build cloud/direct against the Pro API. Do not build a browser extension for v1.**

- Cloud gives reliable auto-delist without the seller's machine being on — the brief's own
  stated advantage — and the Webhooks API now makes that straightforward.
- The extension route's entire justification was that it was "the only way into Vinted".
  That is no longer true for Pro sellers.
- An extension is a permanent maintenance tax against an adversarial, changing DOM, plus
  Chrome Web Store review overhead, and it sits in the grey area of Vinted's terms. The Pro
  API is sanctioned, contractual, and versioned.
- Consciously accepted trade-off: **we cannot serve non-Pro private sellers on Vinted at
  all.** That is a real segment cut. It is the right cut — those sellers are, by Vinted's own
  rules, not supposed to be selling commercially anyway, and building brittle automation to
  serve people in breach of a platform's terms is a bad foundation for a business whose
  entire pitch is trustworthiness.

## Revised risk assessment

The brief said: *"Vinted will break. Repeatedly. Budget ongoing time, not a one-off build."*

That was correct for the extension route and remains correct for it. On the Pro API the risk
profile changes shape rather than disappearing:

| Risk | Before (extension) | Now (Pro API) |
|---|---|---|
| Breakage | Frequent, unannounced, adversarial | Versioned API; normal deprecation risk |
| ToS standing | Grey area | Sanctioned |
| Access | Anyone with an account | **Gated by allowlist — new risk** |
| Seller onboarding | Install extension | **Must register as Pro, accept 14-day returns — new risk** |
| Reach | All Vinted sellers | Pro sellers only |

Net: the *technical* maintenance tax drops a lot. Two *commercial* risks appear in its
place, and they are outside our control in a way DOM breakage was not. Overall this is a
significant improvement, but "dead on arrival without Vinted" is now more likely to be
caused by allowlist refusal or seller unwillingness to go Pro than by broken automation.

## Open questions — none answerable by reading, all cheap to answer

1. **Can Eden Relics get allowlisted, and how long does it take?** Teodora registering the
   shop as Vinted Pro and applying is the fastest possible test, and it doubles as beta
   tester zero (brief §6). **Do this first — nothing else should be built until the answer
   is known.**
2. **Is there a vendor/partner route** so sellers are not each individually gated? Ask
   Vinted Pro support directly.
3. **Do Pro sellers pay commission in the UK?** Vinted's page says free; third-party sources
   disagree. Confirm from the account itself.
4. **Will real vintage sellers accept Pro status and a mandatory 14-day returns window?**
   The single most important commercial question here. Ask 5 sellers before designing
   anything.
5. **What does the Items API actually accept?** Photos, measurements, condition, brand, size
   — the endpoint reference needs reading once allowlisted; the sub-pages were not publicly
   reachable during this spike.
6. **Rate limits** are not documented. Establish empirically before designing sync.

## Recommendation

1. **Register Eden Relics as Vinted Pro and apply for Integrations access this week.** It is
   free, it is the gate on everything else, and the elapsed time is the schedule risk.
2. **Do not write any Vinted code until allowlisted.** The endpoint contract cannot be read
   without access, and building against assumptions is how this spike's value gets thrown
   away.
3. **Ask the 14-day returns question of real sellers in parallel.** It gates adoption
   regardless of what the API does.
4. **Drop the browser extension from v1 scope**, and revisit only if allowlisting proves
   unobtainable.
5. Keep the second brief §4.1 spike — measurement accuracy — as the next one to run, since
   it is independent of all of the above and can proceed immediately.

## Sources

- [Vinted Pro Integrations — API Documentation](https://pro-docs.svc.vinted.com/)
- [Vinted Pro Integrations Portal](https://pro-portal.svc.vinted.com/)
- [Vinted UK — Selling with Vinted for Professionals](https://www.vinted.co.uk/pro)
- [Vinted UK — Commercial selling](https://www.vinted.co.uk/help/4/1120-commercial-selling)
- [Vinted UK — Registering as a Pro seller](https://www.vinted.co.uk/help/906-registering-as-a-pro-seller)
- [Zipsale — Connecting to Vinted via Chrome extension](https://zipsalehelp.zendesk.com/hc/en-gb/articles/4404862902801-Connecting-to-Vinted-Zipsale-Google-Chrome-extension)
- [Vendoo Crosslist Extension — Chrome Web Store](https://chromewebstore.google.com/detail/vendoo-crosslist-extensio/mnampbajndaipakjhcbbaihllmghlcdf)
- [Best Cross Listing Apps for UK Sellers — Crosslist](https://crosslist.com/blog/best-cross-listing-apps-uk)
- [API Vinted Pro — vint-aide community thread](https://www.vint-aide.com/t/api-vinted-pro-enfin-une-vraie-api-officielle/2108?tl=en)
