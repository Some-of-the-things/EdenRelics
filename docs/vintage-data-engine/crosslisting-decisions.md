# Crosslisting brief §9 — answers

**Decided by Peter, 14 August 2026.** The crosslisting brief closes with five open questions addressed
to engineering. These are the answers, with the reasoning, so they are not reopened by default.

§2 of the brief already settles the platform-access landscape (Vinted access: assume refused; every
competitor ships a browser extension). Nothing here revisits that.

---

## 1. One extension, with per-platform modules

**Not one extension per marketplace.** A single Chrome Web Store listing and one review cycle. Session
handling, messaging, rate limiting and jitter are written once; the DOM selectors that actually break
live in isolated per-platform modules, so a Vinted redesign cannot take Depop down with it.

The brief calls Vinted integration "a permanent maintenance tax… roughly monthly fixes, forever". Two
extensions would mean paying that tax twice on the plumbing as well as on the selectors, which is the
half that doesn't need to be duplicated.

**Built** — see `extension/` and its README. Three invariants are enforced rather than documented:
it never submits a form (a test reads the marketplace-side files and fails if a click appears), it
refuses any platform whose field mapping is unresearched on *either* side, and every failure carries
the paste fallback. Both platform tables ship `unresearched`, so nothing fills a form until §6 lands.

## 2. Vinted sales are polled while the browser is open, and we say so

There is no server-side route, so the extension checks the seller's Vinted session on a jittered
interval — human-paced, per brief §4.

**The honest characterisation goes in onboarding, not the ToS:** a Vinted sale may not propagate until
they next open Vinted. That is the 3am double-sale the brief names as the thing sellers abandon a tool
over, and a seller who was told about it up front has been dealt with fairly. One they discover
themselves has not.

Sale detection on Etsy and eBay runs server-side and continuously, and propagates outward — so a sale
*there* still triggers delist attempts everywhere without the seller's machine being on.

The polling machinery is built and the disclosure is on the onboarding page and in the popup, but the
per-platform request is unresearched and returns nothing rather than guessing: guessing which request
lists sold items would be guessing about money, and a delist on a wrong guess is worse than none.

## 3. Images: full-resolution originals, kept indefinitely

R2 is roughly $0.015/GB-month with no egress fees. At about five images of ~2MB per listing that is
approximately **$1.50/month at 10k listings, $15 at 100k, $150 at 1M** — small enough that resolution
is not worth trading for it.

The archive is the moat, and detail thrown away now cannot be recovered once the garment has shipped.
This is already how the capture pipeline works: `EvidenceRecord.ImageKey` holds the original bytes
verbatim and `DisplayImageKey` a disposable web-sized derivative that can be rebuilt at any time. The
decision is therefore a confirmation, not a change — but it is the decision that keeps it that way.

## 4. We never hold a seller's marketplace credentials

Settled by the brief's own strong preference. The extension operates inside the seller's existing
logged-in session; we never ask for, transmit or store a marketplace password. It is also the only
answer consistent with a brand whose proposition is trustworthiness.

## 5. Instrumentation lives in the tool's own database

Per-seller and joinable to a garment. The first-party site analytics pipeline is cookieless and
page-view shaped, so it cannot attribute an event to a seller or join it to a piece — which is most of
what these numbers are for.

**Built and shipped alongside this document** (see `seller-tool/Api/Instrumentation.cs`), because brief
§10 is right that retrofitting loses the first months, and the first months are the beta the gate is
judged on.

What is recorded, and why each one:

| Number | How it is derived |
|---|---|
| Listings created / published | `GarmentCreated` (server-recorded), `ListingPublished` |
| Time per listing | Median of `ListingPublished.DurationMs`. Median, not mean — one draft left open over lunch would otherwise dominate |
| Measurement acceptance rate | `MeasurementAccepted` over accepted + adjusted + rejected. An adjusted measurement counts *against* acceptance: a number the seller had to correct did not save them the tape measure |
| Extension failure rate per platform | `ExtensionPublishFailed` over `…Attempted`, grouped by platform, with the commonest failure reasons — which is what tells you what to fix after a marketplace redesign |
| **Flags raised vs. how often the seller was actually wrong** | `DatingFlagRaised` (recorded by the server, never reported by a client) against `DatingFlagUpheld` / `DatingFlagDismissed`, measured over *resolved* flags only |

Two rules the implementation enforces rather than trusts:

- **The server records the flag itself.** A client that forgets to report a flag would make the
  headline metric look better than it is, and the headline metric is the one that must not be
  flattering. `POST /events` refuses server-owned kinds outright.
- **A rate with no data is `null`, never `0`.** "Nobody has measured anything" and "every measurement
  was rejected" are opposite readings and must not render identically.

Dismissals are a first-class answer, not a nuisance: a rule that gets dismissed often is how a bad
rule gets found, and the seller-facing UI asks the question once, while the garment is in front of
them.

## 6. Image provenance is recorded, never inferred

**Decided 18 August 2026**, from Teodora's v1 reframe (`dating-tool-v1-reframe.md`).

v1's only user is Teodora, logging every garment through the shop. That makes the back catalogue
part of the archive: months of label photos already sitting in a camera roll, for garments long
since sold. So capture had to grow three fields that cannot be reconstructed later.

| Field | Why it cannot wait |
|---|---|
| `Provenance` (LiveCapture / HistoricalUpload) | "Do not let unflagged historical images become training ground truth." Unflagged now means the properly-shot set can never be separated from the rough one |
| `PhotographedAtLocal` (EXIF) | The upload date is meaningless for a back-catalogue photo; the capture date locates when that garment passed through the shop. Lost the moment a file is re-encoded |
| `ZipOriginality` (Original / Replaced / **Unsure**) | A replaced zip logged unmarked dates the repair, not the garment |

Three consequences worth stating, because each is a place the obvious implementation is wrong:

- **The capture standard does not apply to the back catalogue.** Those photographs already exist,
  the garments are gone, and they cannot be retaken. Enforcing the resolution floor would reject
  most of the archive this is meant to seed. They are kept, flagged, and excluded from anything
  needing standard-quality input — including the completeness check, which now counts live
  captures only.
- **A zip is refused rather than defaulted.** Every default is wrong: Original silently dates
  repairs as manufacture, Replaced discards good evidence. "Unsure" is always offered, which is
  exactly why blank is not acceptable.
- **`PhotographedAtLocal` is not UTC and is not named as if it were.** EXIF carries no timezone,
  so it is the camera's wall clock, stored in a column without one.

Bulk upload defaults to `HistoricalUpload` and single capture to `LiveCapture` — each endpoint
defaults to what it is for, and getting that backwards would mark the back catalogue as
standard-quality, which is the one thing the flag exists to prevent.

---

## Still open

Not answered here, because they are not engineering's to answer:

- Field mapping for eBay, Vinted and Depop (brief §6) — blocking publish today, and now the single
  thing standing between the extension and doing anything at all. It is the only item on this list
  that no amount of engineering can move.
- The house-copy format spec (§4.6 of the engineering brief).
- The measurement spike against ~10 real garments (the addendum), which is what would let any
  reference type stop being `Unvalidated`.
- Solicitor review of the terms (§8).
