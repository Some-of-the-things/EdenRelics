# Eden Relics Marketplace — Legal Documents (First Drafts)

> **⚠️ DRAFT — NOT LEGAL ADVICE.** Every document in this folder is an **unreviewed first draft** produced to give a qualified adviser a starting point and thereby reduce cost. **None of these documents may be published, relied on, or put into use until they have been reviewed and approved by (a) a solicitor qualified in England & Wales and (b) the company's accountant / tax adviser.** Nothing here is legal or tax advice.

---

## 1. Purpose of this folder

Eden Relics is pivoting from a **single-seller vintage shop** to a **curated multi-seller marketplace**. These drafts are the contractual and policy scaffolding that pivot needs. They are written to be *specific to this business* — one-of-one vintage womenswear, Stripe Connect payments, curated (vetted) third-party sellers — rather than generic marketplace boilerplate, so that professional review is faster and cheaper.

They are **starting points**, not finished terms. Treat every `[PLACEHOLDER]`, every `> ⚠️ Review note:` call-out, and the model assumption below as an explicit hand-off to the professionals.

---

## 2. THE CORE MODEL ASSUMPTION (must be confirmed before use)

Every document in this folder is written on the following assumption, which **must be confirmed by the company's solicitor and accountant** before any document is used:

> **Eden Relics acts as a DISCLOSED AGENT / facilitator.**
> The **contract of sale for each item is between the SELLER and the BUYER.** Eden Relics provides the venue (the marketplace website), curation (vetting sellers and listings), and payment facilitation (via Stripe Connect). **The seller is the merchant / seller of record and bears the primary consumer-law liability** for the goods they sell (satisfactory quality, description, returns, etc.). Eden Relics is **not** the seller of the goods and does not take title to them.

### 2.1 The open fork the professionals must resolve

There are two fundamentally different legal models for a marketplace, and **the choice changes these documents materially**:

| | **Model A — Agent / Facilitator** *(assumed here)* | **Model B — Reseller / Principal** |
|---|---|---|
| Who sells to the buyer? | The third-party seller | Eden Relics |
| Sale contract | Seller ↔ Buyer | Eden Relics ↔ Buyer |
| Primary consumer-law liability (CRA 2015 quality/description, returns) | Seller | Eden Relics |
| VAT on the item price | Seller's concern (if seller is VAT-registered); platform generally accounts for VAT only on its **commission** | Eden Relics accounts for VAT on the **full sale price** |
| Stripe Connect flow | Fits "separate charges and transfers" cleanly | Also possible, but liability sits with platform |

> **⚠️ Review note (whole-project blocker):** The agent-vs-reseller decision is the single most important legal/tax question for this pivot. It affects **consumer liability, VAT accounting, invoicing, and the wording of almost every clause** in this folder. The solicitor **and** the accountant must confirm Model A is correct — and confirm it is *operated* consistently with Model A (e.g. Eden Relics must not describe itself as "selling" the goods, must not set final prices in a way that makes it the principal, must present the seller's identity to the buyer, etc.). If the business in practice looks like a reseller, HMRC and consumer regulators may treat it as one regardless of what these documents say. See also the VAT note in §2.2.

### 2.2 The VAT fork

> **⚠️ Review note:** Under Model A, VAT treatment depends on (i) whether Eden Relics is VAT-registered, (ii) whether each seller is VAT-registered, (iii) the VAT margin scheme for second-hand goods, and (iv) any **deemed-supplier / online-marketplace VAT rules** (which can force a platform to account for VAT on certain sales, especially imports or overseas sellers). The accountant must map all of this **before launch**, and again **before any international selling is enabled**, because cross-border sales bring the UK/EU marketplace-VAT and IOSS rules into scope. Do not treat the VAT position as settled by these drafts.

---

## 3. Document index

| # | File | What it is | Primary audience |
|---|------|-----------|------------------|
| 0 | `00-README.md` | This index + model assumption + placeholders | Internal / advisers |
| 1 | `buyer-terms-of-sale.md` | The purchase contract for buyers | Buyers |
| 2 | `seller-agreement.md` | The contract between the platform and each seller | Sellers |
| 3 | `marketplace-terms-of-use.md` | General website & account terms | All users |
| 4 | `returns-and-refunds-policy.md` | Buyer-facing returns / cancellation rights | Buyers |
| 5 | `acceptable-use-and-prohibited-items.md` | What may / may not be listed and done | Sellers (+ buyers) |
| 6 | `privacy-addendum.md` | Marketplace data flows layered on the existing privacy policy | Buyers & sellers |
| 7 | `commission-and-fees.md` | The fee schedule and worked example | Sellers |
| 8 | `complaints-and-disputes.md` | How disputes are handled and escalated | Buyers & sellers |

These are designed to **cross-reference** each other. The Buyer Terms of Sale, Returns Policy, and Prohibited Items list are buyer/seller-facing and should be linked from the site; the Seller Agreement, Commission & Fees, and this README are seller/internal-facing.

---

## 4. Placeholders to fill in

Search all documents for these tokens and replace them before use. **Do not publish with placeholders remaining.**

### Company / entity
- `[COMPANY LEGAL NAME]` — e.g. Eden Relics Ltd (confirm exact registered name)
- `[COMPANY NUMBER]` — Companies House registration number
- `[REGISTERED OFFICE ADDRESS]` — full registered address
- `[TRADING ADDRESS]` — if different from registered office
- `[VAT NUMBER]` — if/when VAT-registered (and confirm whether to display it)
- `[ICO REGISTRATION NUMBER]` — data-protection registration with the Information Commissioner's Office

### Contact
- `[CONTACT EMAIL]` — general / legal contact (e.g. hello@edenrelics.co.uk)
- `[SUPPORT EMAIL]` — buyer/seller support
- `[PRIVACY / DPO EMAIL]` — data-protection contact
- `[COMPLAINTS EMAIL]` — complaints intake
- `[WEBSITE URL]` — e.g. https://edenrelics.co.uk

### Commercial
- `[COMMISSION RATE]` — platform commission, e.g. `[X]%` of the item price (excl. or incl. delivery — decide which)
- `[LISTING FEE]` — if any (else "no listing fee")
- `[PAYMENT PROVIDER]` — Stripe / Stripe Connect (Express)
- `[PAYOUT SCHEDULE]` — e.g. paid out on Stripe's standard rolling schedule / after buyer's cancellation window closes
- `[RETURNS WINDOW]` — statutory 14-day cancellation + any voluntary extension
- `[SELLER RETURN ADDRESS POLICY]` — how return addresses are provided

### Policy / governance
- `[GOVERNING LAW]` — England & Wales (assumed throughout)
- `[EFFECTIVE DATE]` / `[LAST UPDATED]` — version dates on each doc
- `[ADR PROVIDER]` — any Alternative Dispute Resolution / ombudsman scheme signposted (see doc 8)

---

## 5. How to use these drafts with your advisers

1. **Send the whole folder** to the England & Wales solicitor and the accountant together — the documents interlock.
2. Ask the solicitor to confirm **Model A vs Model B** first (§2.1). Everything else depends on it.
3. Ask the accountant to confirm the **VAT position** (§2.2), including the margin scheme and any deemed-supplier rules.
4. Work through every `> ⚠️ Review note:` — each marks a decision or a high-risk clause.
5. Fill every `[PLACEHOLDER]`.
6. Have the solicitor confirm the **consumer-law information duties** (pre-contract information, cancellation form, complaints signposting) are met by the final live pages, not just these drafts.
7. Version and date each document, and keep a record of what buyers/sellers agreed to and when.

---

*Prepared as an internal first draft. Not legal advice. Requires professional review before use.*
