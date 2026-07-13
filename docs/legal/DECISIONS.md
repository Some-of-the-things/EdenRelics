# Marketplace Legal — Business Decisions Log

> Decisions taken by Eden Relics on 2026-07-13 to accompany the drafts in this folder when they go
> to the solicitor + accountant. These are **business decisions**; the professionals formalise the
> wording, confirm the legal/tax analysis, and resolve the ⚠️ items below. Not legal advice.

## Decisions made

| # | Decision | Choice |
|---|----------|--------|
| 1 | **Legal model** | **Agent / facilitator** (Model A). Sale is seller→buyer; Eden Relics provides venue + curation + Stripe payment facilitation; sellers bear goods liability + their own VAT; platform charges VAT only on its commission. |
| 2 | **Platform commission** | **15%** of the item price. |
| 3 | **Stripe fees** | **Platform absorbs** Stripe's processing fees (~1.5% + 20p) out of its 15% commission — sellers see one simple deduction. |
| 4 | **Payout timing** | **Held until the 14-day statutory cancellation window closes**, then released to the seller's connected account — so a statutory refund is always covered. |
| 5 | **Restricted items** | **Ban** real fur, ivory, and CITES-restricted materials (incl. tortoiseshell/exotic skins). Ivory per the Ivory Act 2018. |
| 6 | **Returns window** | **14-day statutory** distance-selling cancellation **+ a voluntary 30-day** returns window as a trust signal. |

### Worked commission example (15%, platform absorbs Stripe)
Item sells for £100 (+ delivery handled separately). Buyer pays Eden Relics via Stripe. Platform
commission = £15. Stripe fee (~£1.70) comes **out of** the platform's £15, leaving ~£13.30 platform
margin. Seller receives **£85**, transferred after the 14-day window. *(Confirm whether commission is
on item price only or item + delivery — see ⚠️ below.)*

## Operate-consistently note (agent model)
For the agent model to hold up (HMRC + consumer regulators look at substance, not labels): present the
seller's identity to the buyer, let sellers set their own prices, and don't describe Eden Relics as
"selling" the goods. Heavy curation is fine but mustn't tip into acting as principal.

## ⚠️ Still needs the professionals (solicitor / accountant)
- **VAT** (accountant): Eden Relics' registration status; the second-hand **margin scheme**; and any
  **deemed-supplier / online-marketplace VAT** rules — re-check before enabling international sales.
- **Payout hold vs the voluntary 30-day window** (accountant + solicitor): the hold covers the
  **statutory 14 days**. A goodwill return in days **15–30** happens *after* the seller is paid, so
  it's recouped from the seller's balance / a small rolling reserve. Confirm the mechanism (extend the
  hold vs reserve vs clawback from future payouts). **Recommended: 14-day hold + recover later returns
  from the seller's balance/reserve** — flag for sign-off.
- **Commission base**: item price only, or item + delivery? (Decide, then fix in `commission-and-fees.md`.)
- **Data-controller roles**: platform / seller / Stripe (independent vs joint controllers) — solicitor.
- **Cancellation hygiene exception**: the sealed-goods exception is narrow for clothing — don't
  over-apply it. Solicitor to confirm the final returns wording.
- **ADR provider**: pick an Alternative Dispute Resolution scheme to signpost (post-Brexit position).

## Company details still to fill (factual placeholders across the docs)
`[COMPANY LEGAL NAME]` · `[COMPANY NUMBER]` · `[REGISTERED OFFICE ADDRESS]` · `[VAT NUMBER]` (+ reg.
status) · `[ICO REGISTRATION NUMBER]` · `[CONTACT/SUPPORT/PRIVACY/COMPLAINTS EMAIL]` · `[WEBSITE URL]`
· `[EFFECTIVE DATE]`.
